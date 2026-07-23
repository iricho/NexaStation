using Microsoft.Extensions.DependencyInjection;
using NexaStation.UI.DependencyInjection;
using NexaStation.UI.Themes;
using NexaStation.UI.Themes.Nexa.DependencyInjection;
using NexaStation.UI.Themes.Crossbar.DependencyInjection;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using NexaStation.App.ViewModels;
using NexaStation.App.Services;
using NexaStation.Core;
using NexaStation.Infrastructure;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Providers.Steam.DependencyInjection;
using NexaStation.Providers.Emulation.DependencyInjection;
using NexaStation.Providers.WindowsApps.DependencyInjection;
using NexaStation.DesktopControl.DependencyInjection;
using NexaStation.Domain;
using NexaStation.Providers.Gallery.DependencyInjection;
using NexaStation.Gallery.DependencyInjection;
using NexaStation.Media.DependencyInjection;
using NexaStation.Media.Hub;
using NexaStation.QuickMenu.DependencyInjection;
using NexaStation.SystemIntegration.DependencyInjection;
using NexaStation.SystemIntegration.Services;
using NexaStation.SystemIntegration.Windows;
using NexaStation.QuickMenu.Services;
using Microsoft.UI.Windowing;
using NexaStation.Customization.DependencyInjection;
using NexaStation.Customization.Services;
using NexaStation.Presentation.DependencyInjection;
using NexaStation.Infrastructure.Artwork;
using NexaStation.Infrastructure.DiscoverApps;
using NexaStation.Onboarding;
using NexaStation.Recovery;
using NexaStation.Release;
using NexaStation.Customization.Models;
using NexaStation.Compatibility.DependencyInjection;
using NexaStation.Compatibility.Profiles;
using NexaStation.Feedback.DependencyInjection;
using NexaStation.Quality.CrashFingerprinting;
using NexaStation.Quality.DependencyInjection;
using NexaStation.Quality.Performance;
using NexaStation.Quality.Regression;
using NexaStation.Quality.Navigation;
using NexaStation.Input.DependencyInjection;
using NexaStation.Input.Glyphs;
using NexaStation.Input.Mapping;
using NexaStation.Interaction.DependencyInjection;
using NexaStation.MediaPlayer.DependencyInjection;
using NexaStation.MediaPlayer.Playback;
using NexaStation.MediaPlayer.ExternalPlayers;
using NexaStation.EmulatorDiscovery.DependencyInjection;
using NexaStation.TextInput.DependencyInjection;
using NexaStation.TextInput.Keyboard;
using NexaStation.TextInput.Layouts;
using NexaStation.TextInput.Sessions;
using NexaStation.TextInput.Suggestions;
using NexaStation.TextInput.Validation;
using NexaStation.Display.Calibration;
using NexaStation.Display.DependencyInjection;
using NexaStation.Display.Monitors;
using NexaStation.Profiles.DependencyInjection;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;
using NexaStation.Infrastructure.Search;
using NexaStation.Infrastructure.Metadata;
using NexaStation.Infrastructure.Launchers;
using NexaStation.Infrastructure.Activity;
using NexaStation.Infrastructure.Capture;
using NexaStation.Infrastructure.Ecosystem;
using NexaStation.Infrastructure.RemotePlay;
using Velopack.Locators;

namespace NexaStation.App;

public sealed partial class App : Application, IDisposable
{
    private static readonly string[] DefaultQuickMenuSectionOrder=["QuickControls","Audio","MediaControls","Controllers","Network","System","Power","Notifications"];
    private ServiceProvider? _services;
    private MainWindow? _window;
    private OnboardingWindow? _onboardingWindow;
    private RecoveryWindow? _recoveryWindow;
    private Window? _storageFailureWindow;
    private IAppLogger? _logger;
    private AppInstanceCoordinator? _singleInstance;
    private IStartupHealthService? _startupHealth;
    private StartupStage _startupStage = StartupStage.Starting;
    private bool _safeMode;
    private readonly ApplicationPackageLocation _packageLocation;

    public App()
    {
        _packageLocation = CapturePackageLocation();
        InitializeComponent();
        UnhandledException += OnUiUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Stopwatch startupTimer = Stopwatch.StartNew();
        NexaStationPaths paths = new(new NexaStationPathOptions(PackageLocation: _packageLocation));
        try
        {
            paths.EnsureCreated();
        }
        catch (IOException exception)
        {
            ShowStorageFailure(paths, exception);
            return;
        }
        string bootstrapLog = Path.Combine(paths.State, "bootstrap.log");
        WriteBootstrap(bootstrapLog, "Launch callback entered.");
        AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();
        IActivationRouter activationRouter = new ActivationRouter();
        _singleInstance = new AppInstanceCoordinator(activationRouter);
        WriteBootstrap(bootstrapLog, "Registering the single-instance coordinator.");
        SingleInstanceResult instanceResult = await _singleInstance.RegisterAsync("NexaStation.Main", activationArguments, TimeSpan.FromSeconds(8));
        WriteBootstrap(bootstrapLog, $"Single-instance registration completed. Primary={instanceResult.IsPrimary}; Redirected={instanceResult.RedirectCompleted}.");
        if (!instanceResult.IsPrimary)
        {
            Exit();
            return;
        }

        _singleInstance.ActivationReceived += OnExistingInstanceActivated;
        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        bool isUpgrade = File.Exists(paths.Database) && !File.Exists(paths.OnboardingState);

        ServiceCollection services = new();
        services.AddSingleton<INexaStationPaths>(paths);
        services.AddSingleton<ISensitiveDataRedactor, SensitiveDataRedactor>();
        services.AddSingleton<IDatabaseHealthService, DatabaseHealthService>();
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
        services.AddSingleton<IStartupHealthService, StartupHealthService>();
        services.AddSingleton<ICrashReportService, CrashReportService>();
        services.AddSingleton<IDiagnosticsExportService, DiagnosticsExportService>();
        services.AddSingleton<IOnboardingStateStore>(_ => new OnboardingStateStore(paths.OnboardingState));
        services.AddSingleton<OnboardingFlowService>();
        services.AddSingleton<OnboardingSetupCoordinator>();
        services.AddSingleton<IStartupTaskService, PackagedStartupTaskService>();
        services.AddSingleton<IActivationRouter>(activationRouter);
        services.AddSingleton(ReleaseInfo.FromAssembly(typeof(App).Assembly, ReleaseChannel.Development));
        services.AddSingleton<IAppLogger, FileAppLogger>();
        services.AddSingleton<ISelectionStateService, SelectionStateService>();
        services.AddSingleton<IDashboardStateService, DashboardStateService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddAdaptiveInput();
        services.AddNexaInteraction();
        services.AddNexaDefaultTheme();
        services.AddNexaCrossbarTheme();
        services.AddNexaUiShared();
        services.AddSingleton<IThemeStateStore,SettingsThemeStateStore>();
        services.AddSingleton<AppThemeRuntimeContext>();
        services.AddSingleton<IThemeRuntimeContext>(provider=>provider.GetRequiredService<AppThemeRuntimeContext>());
        services.AddSingleton<IInputService, WindowsGamepadInputService>();
        services.AddSingleton(provider => new SqliteConnectionFactory(provider.GetRequiredService<INexaStationPaths>()));
        services.AddSingleton<ISqliteConnectionFactory>(provider => provider.GetRequiredService<SqliteConnectionFactory>());
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<SqliteCompatibilityStore>();
        services.AddSingleton<ICompatibilityProfileRepository>(provider => provider.GetRequiredService<SqliteCompatibilityStore>());
        services.AddSingleton<ICompatibilityReportRepository>(provider => provider.GetRequiredService<SqliteCompatibilityStore>());
        services.AddSingleton<SqliteQualityStore>();
        services.AddSingleton<ICrashFingerprintRepository>(provider => provider.GetRequiredService<SqliteQualityStore>());
        services.AddSingleton<IPerformanceBaselineRepository>(provider => provider.GetRequiredService<SqliteQualityStore>());
        services.AddSingleton<IRegressionCaseRepository>(provider => provider.GetRequiredService<SqliteQualityStore>());
        services.AddSingleton<ILibraryRepository, SqliteLibraryRepository>();
        services.AddSingleton<IPlaySessionRepository, SqlitePlaySessionRepository>();
        services.AddSingleton<IContentLifecycleService, SqliteContentLifecycleService>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IApplicationStateRepository, SqliteApplicationStateRepository>();
        services.AddSingleton<IApplicationStateService, ApplicationStateService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<SqlitePhase12Store>();
        services.AddSingleton<IActivityTimelineService>(provider=>provider.GetRequiredService<SqlitePhase12Store>());
        services.AddSingleton<IActivityRetentionService>(provider=>provider.GetRequiredService<SqlitePhase12Store>());
        services.AddSingleton<ICapturedMediaStore>(provider=>provider.GetRequiredService<SqlitePhase12Store>());
        services.AddSingleton<IRemotePlayProfileStore>(provider=>provider.GetRequiredService<SqlitePhase12Store>());
        services.AddSingleton<IActivityExportService,LocalActivityExportService>();
        services.AddSingleton<IActivityPrivacyService,ProfileActivityPrivacyService>();
        services.AddSingleton<IPresenceProvider,UnavailableDiscordPresenceProvider>();
        services.AddSingleton<IPresenceCoordinator,SettingsPresenceCoordinator>();
        services.AddSingleton<ICaptureConsentService,SessionCaptureConsentService>();
        services.AddSingleton<IScreenshotCapturePlatform,WindowsGdiScreenshotPlatform>();
        services.AddSingleton<IScreenshotCaptureService,UserInitiatedScreenshotCaptureService>();
        services.AddSingleton<IGlobalHotkeyService>(_=>new WindowsGlobalHotkeyService(()=>_window is null?0:WinRT.Interop.WindowNative.GetWindowHandle(_window)));
        services.AddSingleton<ISnippingToolService,WindowsSnippingToolService>();
        services.AddSingleton<IRecordingFolderMonitor,RecordingFolderMonitor>();
        services.AddSingleton<IRemotePlayLaunchService,RemotePlayLaunchService>();
        services.AddSingleton<IEcosystemCapabilityService,EcosystemCapabilityService>();
        services.AddSingleton<IWindowsShortcutResolver, WindowsShortcutResolver>();
        services.AddSingleton<IProviderPersistence, SqliteProviderPersistence>();
        services.AddSingleton<IArtworkResolver,UnifiedArtworkResolver>();
        services.AddSingleton<IGameIdentityResolver,GameIdentityResolver>();
        services.AddSingleton<IGameMatchScorer,DeterministicGameMatchScorer>();
        services.AddSingleton<IProviderCredentialStore,WindowsCredentialManagerStore>();
        services.AddSingleton<IProviderRateLimitCoordinator,ProviderRateLimitCoordinator>();
        services.AddSingleton<IRemoteArtworkDownloader,RemoteArtworkDownloader>();
        services.AddSingleton<IArtworkAssignmentStore,SqliteArtworkAssignmentStore>();
        services.AddSingleton<IArtworkPresenceService,ArtworkPresenceService>();
        services.AddSingleton<IArtworkJobService,SqliteArtworkJobService>();
        services.AddSingleton<IArtworkJobProcessor,ArtworkJobProcessor>();
        services.AddSingleton<IArtworkJobPreflightService,ArtworkJobPreflightService>();
        services.AddSingleton<IArtworkProviderConnectionTester,ArtworkProviderConnectionTester>();
        services.AddSingleton<IArtworkReviewService,ArtworkReviewService>();
        services.AddSingleton<IMetadataProvenanceStore,SqliteMetadataProvenanceStore>();
        services.AddSingleton(_=>new HttpClient{Timeout=TimeSpan.FromSeconds(30)});
        services.AddSingleton<LocalArtworkProvider>(provider=>new LocalArtworkProvider([Path.Combine(paths.Data,"Artwork","Import")],provider.GetRequiredService<IRemoteArtworkDownloader>()));
        services.AddSingleton<IArtworkProvider>(provider=>provider.GetRequiredService<LocalArtworkProvider>());
        services.AddSingleton<IArtworkProvider,SteamPublicArtworkProvider>();
        services.AddSingleton<IArtworkProvider,Ps2CoverArtworkProvider>();
        services.AddSingleton<IArtworkProvider,ScreenScraperArtworkProvider>();
        services.AddSingleton<IArtworkProvider,SteamGridDbArtworkProvider>();
        services.AddSingleton<TheGamesDbProvider>();
        services.AddSingleton<IArtworkProvider>(provider=>provider.GetRequiredService<TheGamesDbProvider>());
        services.AddSingleton<IGameMetadataProvider>(provider=>provider.GetRequiredService<TheGamesDbProvider>());
        services.AddSingleton<IDiscoverAppCatalogue,EmbeddedDiscoverAppCatalogue>();
        services.AddSingleton<IDiscoverAppStateService,DiscoverAppStateService>();
        services.AddSingleton<IStoreNavigationService,StoreNavigationService>();
        services.AddSingleton<IExternalUriNavigationService,ExternalUriNavigationService>();
        services.AddSingleton<IEmulationPersistence, SqliteEmulationPersistence>();
        services.AddSingleton<SqliteWindowsApplicationPersistence>();
        services.AddSingleton<IWindowsApplicationPersistence>(provider => provider.GetRequiredService<SqliteWindowsApplicationPersistence>());
        services.AddSingleton<IDesktopControlProfileProvider>(provider => provider.GetRequiredService<SqliteWindowsApplicationPersistence>());
        services.AddSingleton<IGalleryPersistence, SqliteGalleryPersistence>();
        services.AddSingleton<IMediaPersistence,SqliteMediaPersistence>();
        services.AddSingleton<ISystemActionHistory,SqliteSystemActionHistory>();
        services.AddSingleton<ICustomizationStore,SqliteCustomizationStore>();
        services.AddNexaTextInput();
        services.AddNexaDisplay();
        services.AddFrontendProfiles();
        services.AddSingleton<SqlitePhase10Store>();
        services.AddSingleton<IInputProfileStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IControllerPreferenceStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<ITextInputSettingsStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IDisplayPreferenceStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IDisplayCalibrationStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IFrontendProfileStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IGuestActivityStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IPinCredentialStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<IContentRestrictionStore>(provider=>provider.GetRequiredService<SqlitePhase10Store>());
        services.AddSingleton<ILocalSuggestionSource,SqliteLocalSuggestionSource>();
        services.AddSingleton<ITextInputPresenter>(provider=>new WinUiOnScreenKeyboardPresenter(()=>_window,provider.GetRequiredService<IKeyboardLayoutCatalog>(),provider.GetRequiredService<ILocalSuggestionService>(),provider.GetRequiredService<TextInputValidator>()));
        services.AddSingleton<IDisplayPlatformAdapter>(_=>new WinUiDisplayPlatformAdapter(()=>_window));
        services.AddSteamProvider();
        services.AddEmulationProvider();
        services.AddWindowsApplicationProviders();
        services.AddSingleton<ILibraryProvider,EpicLibraryProvider>();
        services.AddSingleton<ILibraryProvider>(_=>new RegistryLauncherProvider(BuiltInLauncherProfiles.Gog));
        services.AddSingleton<ILibraryProvider>(_=>new RegistryLauncherProvider(BuiltInLauncherProfiles.Ubisoft));
        services.AddSingleton<ILibraryProvider>(_=>new RegistryLauncherProvider(BuiltInLauncherProfiles.Ea));
        services.AddSingleton<ILibraryProvider>(_=>new RegistryLauncherProvider(BuiltInLauncherProfiles.BattleNet));
        services.AddDesktopControl();
        services.AddGalleryProvider();
        services.AddGallery();
        services.AddMedia();
        services.AddNexaMediaPlayer();
        services.AddEmulatorDiscovery();
        services.AddSingleton<SqlitePhase13Store>();
        services.AddSingleton<IMediaPlaybackProgressStore>(provider=>provider.GetRequiredService<SqlitePhase13Store>());
        services.AddSingleton<IMediaPlayerPreferencesStore>(provider=>provider.GetRequiredService<SqlitePhase13Store>());
        services.AddSingleton<IExternalMediaPlayerRegistrationStore>(provider=>provider.GetRequiredService<SqlitePhase13Store>());
        services.AddSystemIntegration();
        services.AddQuickMenu();
        services.AddCustomization();
        services.AddCompatibility();
        services.AddQualityServices();
        services.AddFeedbackServices();
        services.AddSingleton<IUiSoundPlayer,OriginalUiSoundPlayer>();
        services.AddPresentation();
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        services.AddSingleton<IProviderImportService, ProviderImportService>();
        services.AddSingleton<IItemActionProvider, DefaultItemActionProvider>();
        services.AddSingleton<IItemContextMenuService, ItemContextMenuService>();
        services.AddSingleton<LaunchCoordinator>();
        services.AddSingleton<ILaunchCoordinator>(provider => provider.GetRequiredService<LaunchCoordinator>());
        services.AddSingleton(provider => new WindowFullscreenService(
            () => WinRT.Interop.WindowNative.GetWindowHandle(_window!),
            () => _window!.AppWindow,
            dispatcherQueue,
            provider.GetRequiredService<IInputService>(),
            provider.GetRequiredService<IAppLogger>()));
        services.AddSingleton<IFrontendWindowService>(provider => provider.GetRequiredService<WindowFullscreenService>());
        services.AddSingleton<INexaStationWindowActions>(_=>new NexaStationWindowActions(MinimizeAsync,MinimizeAsync,RestoreAsync,RestartAsync));
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<GalleryDashboardViewModel>();
        services.AddSingleton<MediaHubViewModel>();
        services.AddSingleton<CustomizationViewModel>();
        services.AddSingleton<DiscoverAppsViewModel>();
        services.AddSingleton<MainWindow>();

        WriteBootstrap(bootstrapLog, "Building the application service provider.");
        _services = services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true,ValidateOnBuild=false});
        WriteBootstrap(bootstrapLog, "Application service provider built.");
        ValidateCriticalServices(_services, bootstrapLog);
        WriteBootstrap(bootstrapLog, "Critical startup services resolved.");
        _logger = _services.GetRequiredService<IAppLogger>();
        _startupHealth = _services.GetRequiredService<IStartupHealthService>();
        StartupDecision startupDecision = await _startupHealth.BeginStartupAsync();
        _safeMode = startupDecision.ShouldEnterSafeMode;
        _services.GetRequiredService<AppThemeRuntimeContext>().IsSafeMode=_safeMode;
        _startupStage = StartupStage.PathsReady;
        await _startupHealth.MarkStageAsync(_startupStage);
        _logger.Information("ApplicationStartup", "NexaStation is starting.");
        if (_safeMode)
        {
            _logger.Information("SafeModeEntered", startupDecision.Reason ?? "NexaStation entered safe mode by request.", new Dictionary<string, object?> { ["RecentFailureCount"] = startupDecision.FailureCount });
        }

        try
        {
            await _services.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            _startupStage = StartupStage.DatabaseReady;
            await _startupHealth.MarkStageAsync(_startupStage);
            await _services.GetRequiredService<IPlaySessionRepository>().RepairIncompleteSessionsAsync(DateTime.UtcNow);
            ISettingsService settings = _services.GetRequiredService<ISettingsService>();
            await EnsureDefaultAsync(settings, "FullscreenEnabled", true);
            await EnsureDefaultAsync(settings, "AnimationsEnabled", true);
            await EnsureDefaultAsync(settings, "InterfaceScale", 1.0);
            bool navigationAuditEnabled = await settings.GetAsync("Quality.NavigationAuditEnabled", false);
            _services.GetRequiredService<INavigationAuditService>().Enabled = navigationAuditEnabled;
            await EnsureDefaultAsync(settings, "ConfirmBeforeRemove", true);
            await EnsureDefaultAsync(settings, "MaximumRecentItems", 12);
            await EnsureDefaultAsync(settings, "MaximumArtworkMemoryCacheMb", 64);
            await EnsureDefaultAsync(settings, "Providers.Steam.EnabledLibrarySourceIds", Array.Empty<string>());
            await EnsureDefaultAsync(settings, "Providers.Steam.RefreshOnStartup", false);
            await EnsureDefaultAsync(settings, "Providers.WindowsApps.ScanCurrentUser", true);
            await EnsureDefaultAsync(settings, "Providers.WindowsApps.ScanCommon", true);
            await EnsureDefaultAsync(settings, "Providers.WindowsApps.IncludeAppsFolder", true);
            await EnsureDefaultAsync(settings, "Providers.WindowsApps.DetectPwas", true);
            await EnsureDefaultAsync(settings, "Providers.WindowsApps.RefreshOnStartup", false);
            await EnsureDefaultAsync(settings, "MaximumGalleryThumbnailCacheMb", 2048);
            await EnsureDefaultAsync(settings, "MaximumGalleryMemoryCacheMb", 128);
            await EnsureDefaultAsync(settings, "GalleryThumbnailQuality", "Balanced");
            await EnsureDefaultAsync(settings, "GenerateVideoPreviews", true);
            await EnsureDefaultAsync(settings, "Gallery.ShowOnHome", true);
            await EnsureDefaultAsync(settings,"Media.PreferredVideoPlayer",string.Empty);
            await EnsureDefaultAsync(settings,"Media.PreferredImageViewer",string.Empty);
            await EnsureDefaultAsync(settings,"Media.RecentLimit",30);
            await EnsureDefaultAsync(settings,"Media.ShowOnHome",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Audio.ShowVolume",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Audio.FineVolumeStep",0.01);
            await EnsureDefaultAsync(settings,"QuickMenu.Audio.PreferredOutputDisplay",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Enabled",true);
            await EnsureDefaultAsync(settings,"QuickMenu.OpenShortcut","Ctrl+Q");
            await EnsureDefaultAsync(settings,"QuickMenu.SectionOrder",DefaultQuickMenuSectionOrder);
            await EnsureDefaultAsync(settings,"QuickMenu.ShowControllerStatus",true);
            await EnsureDefaultAsync(settings,"QuickMenu.ShowNetworkStatus",true);
            await EnsureDefaultAsync(settings,"QuickMenu.ShowClock",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Power.ShowSleep",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Power.ShowSignOut",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Power.ShowRestart",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Power.ShowShutDown",true);
            await EnsureDefaultAsync(settings,"QuickMenu.Power.HoldDurationMilliseconds",1500);
            await EnsureDefaultAsync(settings,"Interface.LayoutId","modern-dashboard");
            await EnsureDefaultAsync(settings,"Interface.ThemeId","nexa-dark");
            await EnsureDefaultAsync(settings,"Interface.SoundPackId","nexa-soft");
            await EnsureDefaultAsync(settings,"Interface.SoundVolume",.6);
            await EnsureDefaultAsync(settings,"Applications.AllowDirectInstallationWithWinget",false);
            await EnsureDefaultAsync(settings,"DiscoverApps.HiddenIds",Array.Empty<string>());
            await EnsureDefaultAsync(settings,"Artwork.Directory",string.Empty);
            await EnsureDefaultAsync(settings,"Startup.Enabled",false);
            await EnsureDefaultAsync(settings,"Privacy.DiagnosticsExportIncludesLogs",true);
            await EnsureDefaultAsync(settings,"Updates.AutomaticChecksEnabled",false);
            await EnsureDefaultAsync(settings,"Updates.Channel","Development");
            await EnsureDefaultAsync(settings,"Input.PromptStyle",PromptStyle.Automatic.ToString());
            await EnsureDefaultAsync(settings,"Input.NintendoConvention",NintendoConfirmConvention.PhysicalLabels.ToString());
            await EnsureDefaultAsync(settings,"Display.ReduceEffectsOnBattery",false);
            await EnsureDefaultAsync(settings,"UI.ActiveThemeId","nexa.default");
            await EnsureDefaultAsync(settings,"UI.LastHealthyThemeId","nexa.default");
            await EnsureDefaultAsync(settings,"UI.ThemeVersion","1.0.0");
            await EnsureDefaultAsync(settings,"UI.LastSettingsCategoryId","personalization");
            await EnsureDefaultAsync(settings,"UI.SettingsSearchHistoryEnabled",false);
            await EnsureDefaultAsync(settings,"UI.ThemeSpecificSettings",new Dictionary<string,object>{{"nexa.default",new{}},{"nexa.crossbar",new{BackgroundMode="AccentWave",SecondaryNavigation="Automatic"}}});
            await _services.GetRequiredService<IThemeManager>().InitializeAsync();
            await InitializePhase10ServicesAsync(settings);
        }
        catch (Exception exception)
        {
            _logger.LogError("DatabaseStartupFailed", "The local library database could not be initialized.", exception);
            await RecordStartupFailureAsync("DatabaseStartupFailed", exception, StartupStage.DatabaseReady);
            ShowRecovery($"The local library could not be opened safely. {exception.Message}");
            return;
        }

        _startupStage = StartupStage.ServicesReady;
        await _startupHealth.MarkStageAsync(_startupStage);

        OnboardingFlowService onboardingFlow = _services.GetRequiredService<OnboardingFlowService>();
        OnboardingLoadResult onboardingLoad = await onboardingFlow.GetStateAsync(isUpgrade);
        if (onboardingLoad.State.Status is OnboardingStatus.NeverStarted or OnboardingStatus.InProgress)
        {
            OnboardingState onboardingState = await onboardingFlow.StartOrResumeAsync(isUpgrade);
            if (isUpgrade && onboardingState.CurrentStep == OnboardingStep.Welcome && onboardingState.Preferences.AppearanceDecision == OnboardingDecisionState.Unanswered)
            {
                OnboardingPreferences seeded = await _services.GetRequiredService<OnboardingSetupCoordinator>().SeedUpgradePreferencesAsync(onboardingState.Preferences);
                onboardingState = await onboardingFlow.SavePreferencesAsync(onboardingState, seeded);
            }
            bool shouldContinue = await RunOnboardingAsync(onboardingFlow, onboardingState, onboardingLoad.RecoveryMessage);
            if (!shouldContinue)
            {
                await _startupHealth.MarkCleanShutdownAsync();
                Exit();
                return;
            }

        }

        _window = _services.GetRequiredService<MainWindow>();
        _window.Closed += OnWindowClosed;
        _window.Activate();
        _logger.Information("MainWindowCreated", "The main window was created and activated.");
        if (!await _window.ShowPrivateAlphaNoticeIfNeededAsync())
        {
            await _startupHealth.MarkCleanShutdownAsync();
            Exit();
            return;
        }

        _services.GetRequiredService<IInputService>().Start();
        await _window.ConfigureCaptureHotkeyAsync(_safeMode);
        if (!_safeMode)
            _ = _services.GetRequiredService<IArtworkJobProcessor>().ResumeInterruptedAsync();
        await _services.GetRequiredService<DashboardViewModel>().InitializeAsync();
        await _services.GetRequiredService<GalleryDashboardViewModel>().InitializeAsync();
        await _services.GetRequiredService<MediaHubViewModel>().RefreshAsync();
        await _services.GetRequiredService<CustomizationViewModel>().InitializeAsync();
        await _services.GetRequiredService<DiscoverAppsViewModel>().InitializeAsync();
        CustomizationViewModel customization = _services.GetRequiredService<CustomizationViewModel>();
        await customization.ApplyThemeAsync(customization.CurrentThemeId);
        _services.GetRequiredService<ITimeService>().StartUpdates();
        _window.RefreshSectionSurface();
        if (!_safeMode)
        {
            await _services.GetRequiredService<IGallerySourceWatcherCoordinator>().StartAsync(
                async (sourceId, token) =>
                {
                    await _services.GetRequiredService<IGalleryLibraryService>().RefreshSourceAsync(sourceId, token);
                    await _services.GetRequiredService<GalleryDashboardViewModel>().RefreshAsync(cancellationToken: token);
                });
        }
        _ = _services.GetRequiredService<IActivityRetentionService>().ApplyAsync();
        if(!_safeMode && await _services.GetRequiredService<ISettingsService>().GetAsync("Providers.Steam.RefreshOnStartup",false))
        {
            try{await _services.GetRequiredService<IProviderImportService>().RefreshAsync("steam");await _services.GetRequiredService<DashboardViewModel>().RefreshAsync();}
            catch(Exception exception){_logger.LogError("SteamStartupRefreshFailed","The optional Steam startup refresh failed.",exception);}
        }
        if(!_safeMode && await _services.GetRequiredService<ISettingsService>().GetAsync("Providers.WindowsApps.RefreshOnStartup",false))
        {
            try
            {
                ISettingsService settings=_services.GetRequiredService<ISettingsService>();WindowsApplicationScanResult scan=await _services.GetRequiredService<IWindowsApplicationLibraryService>().ScanAllAsync(new(await settings.GetAsync("Providers.WindowsApps.ScanCurrentUser",true),await settings.GetAsync("Providers.WindowsApps.ScanCommon",true),true),await settings.GetAsync("Providers.WindowsApps.IncludeAppsFolder",true),await settings.GetAsync("Providers.WindowsApps.DetectPwas",true));await _services.GetRequiredService<IWindowsApplicationPersistence>().ApplyRefreshAsync(scan);await _services.GetRequiredService<DashboardViewModel>().RefreshAsync();
            }
            catch(Exception exception){_logger.LogError("WindowsAppsStartupRefreshFailed","The optional Windows application startup refresh failed.",exception);}
        }
        startupTimer.Stop();
        _logger.Information("DashboardReady", "The dashboard displayed persisted local items.", new Dictionary<string, object?> { ["ElapsedMilliseconds"] = startupTimer.Elapsed.TotalMilliseconds });
        _startupStage = StartupStage.DashboardReady;
        await _startupHealth.MarkStageAsync(_startupStage);
        await _services.GetRequiredService<IFrontendWindowService>().RestoreFullscreenAsync();
        if (instanceResult.InitialRoute is { Kind: ActivationRouteKind.Section, Section: not null })
        {
            _window.NavigateToSection(instanceResult.InitialRoute.Section);
        }
    }

    private static ApplicationPackageLocation CapturePackageLocation()
    {
        if (!VelopackLocator.IsCurrentSet)
        {
            return new(false, false, null, AppContext.BaseDirectory);
        }

        IVelopackLocator locator = VelopackLocator.Current;
        return new(true, locator.IsPortable, locator.RootAppDir, locator.AppContentDir);
    }

    private async Task<bool> RunOnboardingAsync(OnboardingFlowService flow, OnboardingState state, string? recoveryNotice)
    {
        if (_services is null)
        {
            return false;
        }

        string bootstrapLog = Path.Combine(_services.GetRequiredService<INexaStationPaths>().State, "bootstrap.log");
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteBootstrap(bootstrapLog, "Constructing the onboarding window.");
        IInputService onboardingInput = _services.GetRequiredService<IInputService>();
        onboardingInput.Start();
        _onboardingWindow = new(flow, _services.GetRequiredService<OnboardingSetupCoordinator>(), state, onboardingInput, _services.GetRequiredService<IInputGlyphService>(), recoveryNotice);
        WriteBootstrap(bootstrapLog, "Onboarding window constructed.");
        _onboardingWindow.SetupFinished += (_, _) => completion.TrySetResult(true);
        _onboardingWindow.Closed += (_, _) => completion.TrySetResult(false);
        _onboardingWindow.Activate();
        WriteBootstrap(bootstrapLog, "Onboarding window activated.");
        _onboardingWindow.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        WriteBootstrap(bootstrapLog, "Onboarding window entered full screen.");
        if (_startupHealth is not null)
        {
            _startupStage = StartupStage.OnboardingReady;
            await _startupHealth.MarkStageAsync(_startupStage);
        }
        bool result = await completion.Task;
        _onboardingWindow = null;
        return result;
    }

    private async Task ApplyOnboardingPreferencesAsync(OnboardingPreferences preferences)
    {
        if (_services is null)
        {
            return;
        }

        string layoutId = preferences.Layout switch
        {
            "Classic" => "classic-crossbar",
            "Compact" => "compact-handheld",
            _ => "modern-dashboard",
        };
        VisualPerformancePreset performance = preferences.PerformanceProfile switch
        {
            "Quality" => VisualPerformancePreset.Cinematic,
            "Efficient" => VisualPerformancePreset.Minimal,
            _ => VisualPerformancePreset.Balanced,
        };
        await _services.GetRequiredService<IInterfaceLayoutService>().ApplyAsync(layoutId);
        await _services.GetRequiredService<IPerformancePresetService>().ApplyAsync(performance);
        ISettingsService settings = _services.GetRequiredService<ISettingsService>();
        await settings.SetAsync("Startup.Enabled", preferences.StartWithWindows);
        await settings.SetAsync("Privacy.DiagnosticsExportIncludesLogs", preferences.DiagnosticsConsent);
        await settings.SetAsync("Onboarding.Pending.SteamImport", preferences.SteamImportRequested);
        await settings.SetAsync("Onboarding.Pending.EmulationSetup", preferences.EmulationSetupRequested);
        await settings.SetAsync("Onboarding.Pending.WindowsAppScan", preferences.WindowsAppScanRequested);
        await settings.SetAsync("Onboarding.Pending.GallerySetup", preferences.GallerySetupRequested);
        await settings.SetAsync("Gallery.PicturesProposalState", preferences.IncludePicturesFolder ? "Accepted" : "Declined");
        if (preferences.IncludePicturesFolder)
        {
            string pictures=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);if(!string.IsNullOrWhiteSpace(pictures)&&Directory.Exists(pictures))
            {
                IGalleryPersistence persistence=_services.GetRequiredService<IGalleryPersistence>();IReadOnlyList<GallerySource> sources=await persistence.GetSourcesAsync();string normalized=Path.TrimEndingDirectorySeparator(Path.GetFullPath(pictures));
                if(!sources.Any(source=>string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.FolderPath)),normalized,StringComparison.OrdinalIgnoreCase)))
                {
                    DateTime now=DateTime.UtcNow;await _services.GetRequiredService<IGalleryLibraryService>().AddSourceAsync(new(){Id=Guid.NewGuid().ToString("N"),Name="Pictures",FolderPath=pictures,NormalizedFolderPath=normalized,SourceKind=GallerySourceKind.Photos,IncludeSubfolders=preferences.PicturesIncludeSubfolders,Enabled=true,WatchForChanges=true,IncludeImages=true,IncludeVideos=true,PrivacyLevel=preferences.PicturesPrivate?GalleryPrivacyLevel.Private:GalleryPrivacyLevel.Normal,ShowOnHome=preferences.ShowPicturesOnHome,CreatedAtUtc=now,UpdatedAtUtc=now},true,true);
                }
            }
        }
    }

    private void ShowRecovery(string problem)
    {
        if (_services is null || _startupHealth is null)
        {
            Exit();
            return;
        }

        ReleaseInfo release = _services.GetRequiredService<ReleaseInfo>();
        _recoveryWindow = new(
            problem,
            _services.GetRequiredService<INexaStationPaths>(),
            _services.GetRequiredService<IDatabaseBackupService>(),
            _services.GetRequiredService<IDiagnosticsExportService>(),
            _startupHealth,
            release.InformationalVersion,
            _services.GetRequiredService<IInputService>(),
            _services.GetRequiredService<IInputGlyphService>());
        _services.GetRequiredService<IInputService>().Start();
        _recoveryWindow.RestartRequested += async (_, _) => await RestartRecoveryAsync();
        _recoveryWindow.Closed += (_, _) =>
        {
            Dispose();
            Exit();
        };
        _recoveryWindow.Activate();
    }

    private async Task RecordStartupFailureAsync(string eventName, Exception exception, StartupStage stage)
    {
        if (_services is null || _startupHealth is null)
        {
            return;
        }

        try
        {
            await _startupHealth.MarkFailureAsync(stage, exception.Message);
            ReleaseInfo release = _services.GetRequiredService<ReleaseInfo>();
            await _services.GetRequiredService<ICrashReportService>().WriteAsync(eventName, exception, release.InformationalVersion, stage, _safeMode);
            await _services.GetRequiredService<ICrashFingerprintTracker>().RecordAsync(exception, stage, release.Version.ToString());
        }
        catch (Exception reportingException)
        {
            _logger?.LogError("CrashReportWriteFailed", "NexaStation could not persist its local crash report.", reportingException);
        }
    }

    private async Task RestartRecoveryAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            throw new InvalidOperationException("The application restart path is unavailable.");
        }

        Process? replacement = Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false });
        if (replacement is null)
        {
            throw new InvalidOperationException("Windows did not start the replacement NexaStation process.");
        }

        await Task.Yield();
        _recoveryWindow?.Close();
    }

    private void OnExistingInstanceActivated(object? sender, ActivationRoute route)
    {
        _window?.DispatcherQueue.TryEnqueue(async () =>
        {
            if (_services is null)
            {
                return;
            }

            await _services.GetRequiredService<IFrontendWindowService>().BringToForegroundAsync();
            if (route is { Kind: ActivationRouteKind.Section, Section: not null })
            {
                _window?.NavigateToSection(route.Section);
            }
        });
    }

    private async void OnUiUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        _logger?.LogError("UiUnhandledException", "An unexpected UI-thread exception occurred.", args.Exception);
        if (args.Exception is OperationCanceledException)
        {
            args.Handled = true;
            return;
        }

        await RecordStartupFailureAsync("UiUnhandledException", args.Exception, _startupStage);
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        _logger?.LogError(
            "AppDomainUnhandledException",
            "An unhandled application-domain exception occurred.",
            args.ExceptionObject as Exception,
            new Dictionary<string, object?> { ["IsTerminating"] = args.IsTerminating });
        if (args.ExceptionObject is Exception exception)
        {
            _ = RecordStartupFailureAsync("AppDomainUnhandledException", exception, _startupStage);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        _logger?.LogError("UnobservedTaskException", "An unobserved task exception occurred.", args.Exception);
        args.SetObserved();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _logger?.Information("ApplicationShutdown", "NexaStation is shutting down.");
        if (_startupHealth is not null)
        {
            await _startupHealth.MarkCleanShutdownAsync();
        }
        _services?.GetService<IInputService>()?.Stop();
        _services?.GetService<ITimeService>()?.StopUpdates();
        Dispose();
    }

    public void Dispose()
    {
        UnhandledException -= OnUiUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _services?.Dispose();
        _services = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
    }

    private async Task InitializePhase10ServicesAsync(ISettingsService settings)
    {
        ServiceProvider services = _services ?? throw new InvalidOperationException("Application services must be built before Phase 10 initialization.");
        IInputProfileStore profileStore = services.GetRequiredService<IInputProfileStore>();
        IReadOnlyList<InputProfile> profiles = await profileStore.GetProfilesAsync();
        InputProfile? active = profiles.Where(profile=>profile.Enabled&&!profile.BuiltIn).OrderByDescending(profile=>profile.UpdatedAtUtc).FirstOrDefault()
            ?? profiles.FirstOrDefault(profile=>profile.Id==DefaultInputBindings.BuiltInProfileId);
        IReadOnlyList<InputBinding> bindings = active is null ? DefaultInputBindings.Create() : await profileStore.GetBindingsAsync(active.Id);
        if (bindings.Count == 0) bindings = DefaultInputBindings.Create();
        services.GetRequiredService<InputBindingService>().Apply(bindings);
        string promptValue = await settings.GetAsync("Input.PromptStyle", PromptStyle.Automatic.ToString());
        string conventionValue = await settings.GetAsync("Input.NintendoConvention", NintendoConfirmConvention.PhysicalLabels.ToString());
        PromptStyle prompt = Enum.TryParse(promptValue, true, out PromptStyle parsedPrompt) ? parsedPrompt : PromptStyle.Automatic;
        NintendoConfirmConvention convention = Enum.TryParse(conventionValue, true, out NintendoConfirmConvention parsedConvention) ? parsedConvention : NintendoConfirmConvention.PhysicalLabels;
        services.GetRequiredService<IInputPromptPreferenceService>().Apply(prompt, convention);
        _logger?.Information("Phase10ServicesInitialized", "Adaptive input, text entry, local profiles, and display preferences were initialized.", new Dictionary<string, object?> { ["InputProfile"] = active?.Id ?? DefaultInputBindings.BuiltInProfileId, ["BindingCount"] = bindings.Count, ["PromptStyle"] = prompt.ToString() });
    }

    private static async Task EnsureDefaultAsync<T>(ISettingsService settings, string key, T value)
    {
        T current = await settings.GetAsync(key, value);
        await settings.SetAsync(key, current);
    }

    private static void WriteBootstrap(string path, string message)
    {
        try
        {
            const long maxBootstrapLogBytes = 256 * 1024;
            if (File.Exists(path) && new FileInfo(path).Length >= maxBootstrapLogBytes)
            {
                File.Move(path, path + ".previous", overwrite: true);
            }

            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"NexaStation bootstrap logging failed: {exception.Message}");
        }
    }

    private void ShowStorageFailure(NexaStationPaths paths, Exception exception)
    {
        Microsoft.UI.Xaml.Controls.StackPanel panel = new()
        {
            Padding = new Thickness(32),
            Spacing = 14,
        };
        panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = "NexaStation cannot open its data folder",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = exception.Message,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = $"Data folder: {paths.Root}",
            IsTextSelectionEnabled = true,
            Opacity = .72,
            TextWrapping = TextWrapping.Wrap,
        });

        _storageFailureWindow = new Window
        {
            Title = "NexaStation storage error",
            Content = panel,
        };
        _storageFailureWindow.Activate();
    }

    private static void ValidateCriticalServices(IServiceProvider services, string bootstrapLog)
    {
        _ = services.GetRequiredService<INexaStationPaths>();
        WriteBootstrap(bootstrapLog, "Resolved INexaStationPaths.");
        _ = services.GetRequiredService<ISensitiveDataRedactor>();
        WriteBootstrap(bootstrapLog, "Resolved ISensitiveDataRedactor.");
        _ = services.GetRequiredService<IDatabaseHealthService>();
        WriteBootstrap(bootstrapLog, "Resolved IDatabaseHealthService.");
        _ = services.GetRequiredService<IDatabaseBackupService>();
        WriteBootstrap(bootstrapLog, "Resolved IDatabaseBackupService.");
        _ = services.GetRequiredService<IStartupHealthService>();
        WriteBootstrap(bootstrapLog, "Resolved IStartupHealthService.");
        _ = services.GetRequiredService<ICrashReportService>();
        WriteBootstrap(bootstrapLog, "Resolved ICrashReportService.");
        _ = services.GetRequiredService<IDiagnosticsExportService>();
        WriteBootstrap(bootstrapLog, "Resolved IDiagnosticsExportService.");
        _ = services.GetRequiredService<IOnboardingStateStore>();
        WriteBootstrap(bootstrapLog, "Resolved IOnboardingStateStore.");
        _ = services.GetRequiredService<OnboardingFlowService>();
        WriteBootstrap(bootstrapLog, "Resolved OnboardingFlowService.");
        _ = services.GetRequiredService<IStartupTaskService>();
        WriteBootstrap(bootstrapLog, "Resolved IStartupTaskService.");
        _ = services.GetRequiredService<ReleaseInfo>();
        WriteBootstrap(bootstrapLog, "Resolved ReleaseInfo.");
        _ = services.GetRequiredService<IAppLogger>();
        WriteBootstrap(bootstrapLog, "Resolved IAppLogger.");
        _ = services.GetRequiredService<ISqliteConnectionFactory>();
        WriteBootstrap(bootstrapLog, "Resolved ISqliteConnectionFactory.");
        _ = services.GetRequiredService<IDatabaseInitializer>();
        WriteBootstrap(bootstrapLog, "Resolved IDatabaseInitializer.");
        _ = services.GetRequiredService<ICompatibilityProfileRepository>();
        WriteBootstrap(bootstrapLog, "Resolved ICompatibilityProfileRepository.");
        _ = services.GetRequiredService<ICrashFingerprintTracker>();
        WriteBootstrap(bootstrapLog, "Resolved ICrashFingerprintTracker.");
    }

    private Task MinimizeAsync()=>OnWindowThreadAsync(()=>{if(_window?.AppWindow.Presenter is OverlappedPresenter presenter)presenter.Minimize();});
    private Task RestoreAsync()=>OnWindowThreadAsync(()=>{if(_window?.AppWindow.Presenter is OverlappedPresenter presenter)presenter.Restore();_window?.Activate();});
    private async Task RestartAsync()
    {
        if(_services is null||string.IsNullOrWhiteSpace(Environment.ProcessPath))throw new InvalidOperationException("The application restart path is unavailable.");
        ISettingsService settings=_services.GetRequiredService<ISettingsService>();DateTime last=await settings.GetAsync("QuickMenu.LastRestartAttemptUtc",DateTime.MinValue);if(DateTime.UtcNow-last<TimeSpan.FromSeconds(10))throw new InvalidOperationException("A restart was already requested recently.");await settings.SetAsync("QuickMenu.LastRestartAttemptUtc",DateTime.UtcNow);
        Process? replacement=Process.Start(new ProcessStartInfo(Environment.ProcessPath){UseShellExecute=false});if(replacement is null)throw new InvalidOperationException("Windows did not start the replacement NexaStation process.");await OnWindowThreadAsync(()=>_window?.Close());
    }
    private Task OnWindowThreadAsync(Action action){TaskCompletionSource completion=new(TaskCreationOptions.RunContinuationsAsynchronously);if(_window is null){completion.SetException(new InvalidOperationException("The NexaStation window is unavailable."));return completion.Task;}if(!_window.DispatcherQueue.TryEnqueue(()=>{try{action();completion.SetResult();}catch(Exception ex){completion.SetException(ex);}}))completion.SetException(new InvalidOperationException("The window dispatcher is shutting down."));return completion.Task;}
}
