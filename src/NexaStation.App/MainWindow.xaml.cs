using System.Diagnostics;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NexaStation.App.ViewModels;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Gallery.Albums;
using NexaStation.Gallery.Viewing;
using Windows.Storage;
using Windows.System;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using NexaStation.QuickMenu.Models;
using NexaStation.QuickMenu.Safety;
using NexaStation.QuickMenu.Services;
using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;
using NexaStation.Media.Hub;
using NexaStation.Customization.Models;
using NexaStation.Customization.Layouts;
using NexaStation.Customization.Services;
using NexaStation.Customization.Themes;
using NexaStation.Presentation.LayoutContracts;
using NexaStation.Presentation.Dashboard;
using Windows.UI;
using NexaStation.Recovery;
using NexaStation.Release;
using NexaStation.Compatibility.Feeds;
using NexaStation.Compatibility.Matching;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;
using NexaStation.Compatibility.Rules;
using NexaStation.Feedback.IssuePreparation;
using NexaStation.Feedback.ReproductionBundles;
using NexaStation.Quality.Navigation;
using Windows.ApplicationModel.DataTransfer;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.Input.Glyphs;
using NexaStation.Input.Mapping;
using NexaStation.TextInput.Sessions;
using NexaStation.TextInput.Suggestions;
using NexaStation.Display.Calibration;
using NexaStation.Display.Monitors;
using NexaStation.Display.Scaling;
using NexaStation.Display.Handheld;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;
using NexaStation.Onboarding;
using NexaStation.Infrastructure.RemotePlay;
using NexaStation.App.Controls;
using NexaStation.Interaction.ActionTargets;
using NexaStation.MediaPlayer.Playback;
using NexaStation.MediaPlayer.ExternalPlayers;
using NexaStation.EmulatorDiscovery.Discovery;
using NexaStation.UI.Settings;
using NexaStation.UI.Themes;

namespace NexaStation.App;

public sealed partial class MainWindow : Window,IDisposable
{
    private string _localUserName = string.IsNullOrWhiteSpace(Environment.UserName) ? "Owner" : Environment.UserName.Trim();
    private string _ownerAvatarKey = "owner";
    private readonly IInputService _inputService;
    private readonly WindowFullscreenService _windowService;
    private readonly IAppLogger _logger;
    private readonly ILibraryRepository _library;
    private readonly IDatabaseInitializer _databaseInitializer;
    private readonly ISqliteConnectionFactory _connectionFactory;
    private readonly IProviderRegistry _providers;
    private readonly IProviderImportService _providerImports;
    private readonly IProviderPersistence _providerPersistence;
    private readonly ISettingsService _settings;
    private readonly IThemeManager _themeManager;
    private readonly ISettingsCatalog _settingsCatalog;
    private readonly ISettingsSearchService _settingsSearch;
    private readonly IEmulationPersistence _emulation;
    private readonly IEmulationLibraryService _emulationLibrary;
    private readonly IEmulatorAdapterRegistry _emulatorAdapters;
    private readonly IRetroArchCoreDiscovery _retroArchCores;
    private readonly IWindowsApplicationLibraryService _windowsApplications;
    private readonly IWindowsApplicationPersistence _windowsPersistence;
    private readonly IGalleryPersistence _galleryPersistence;
    private readonly IGalleryLibraryService _galleryLibrary;
    private readonly IGalleryThumbnailService _galleryThumbnails;
    private readonly IGalleryMediaLauncher _galleryLauncher;
    private readonly IGalleryBackgroundService _galleryBackgrounds;
    private readonly ISlideshowService _slideshow;
    private readonly GalleryViewerSession _galleryViewer;
    private readonly GalleryAlbumService _galleryAlbums;
    private readonly IGallerySourceWatcherCoordinator _galleryWatchers;
    private readonly QuickMenuService _quickMenu;
    private readonly HoldSafetyCoordinator _holdSafety;
    private readonly ISystemVolumeService _volume;
    private readonly IAudioOutputDeviceService _audioOutputs;
    private readonly ISystemMediaControlService _systemMedia;
    private readonly IControllerStatusService _controllerStatus;
    private readonly INexaStationWindowActions _windowActions;
    private readonly ISystemPowerService _power;
    private readonly ISystemActionHistory _systemActionHistory;
    private readonly ILocalNotificationService _notifications;
    private readonly IMediaAssociationService _mediaAssociations;
    private readonly ITimeService _time;
    private readonly IInterfaceLayoutService _layouts;
    private readonly IDashboardConfigurationService _dashboardConfiguration;
    private readonly IPinService _pins;
    private readonly ICollectionService _collections;
    private readonly ISmartCollectionQueryService _smartCollections;
    private readonly IContentLifecycleService _contentLifecycle;
    private readonly IThemeService _themes;
    private readonly IThemeAssetService _themeAssets;
    private readonly IUiSoundService _sounds;
    private readonly IPerformancePresetService _performance;
    private readonly IInterfaceConfigurationService _interfaceConfiguration;
    private readonly ISemanticFocusService _semanticFocus;
    private readonly IInterfacePresentationService _presentation;
    private readonly IHomeDashboardDensityPolicy _homeDensityPolicy;
    private readonly INexaStationPaths _paths;
    private readonly IDatabaseBackupService _databaseBackups;
    private readonly IDiagnosticsExportService _diagnosticsExport;
    private readonly IStartupTaskService _startupTask;
    private readonly ReleaseInfo _releaseInfo;
    private readonly ICompatibilityProfileResolver _compatibilityResolver;
    private readonly ICompatibilityProfileRepository _compatibilityProfiles;
    private readonly ICompatibilityReportRepository _compatibilityReports;
    private readonly CompatibilityFeedSettings _compatibilityFeedSettings;
    private readonly IReproductionBundleService _reproductionBundles;
    private readonly IGitHubIssuePreparationService _issuePreparation;
    private readonly INavigationAuditService _navigationAudit;
    private readonly IActiveInputModeService _activeInputMode;
    private readonly IInputGlyphService _inputGlyphs;
    private readonly IInputPromptPreferenceService _promptPreferences;
    private readonly IInputBindingResolver _inputBindings;
    private readonly InputBindingService _bindingService;
    private readonly InputMappingService _inputMappings;
    private readonly IInputProfileStore _inputProfiles;
    private readonly IControllerPreferenceStore _controllerPreferences;
    private readonly IInputContextService _inputContext;
    private readonly IOnScreenKeyboardService _onScreenKeyboard;
    private readonly IDisplaySelectionService _displaySelection;
    private readonly IDisplayPreferenceStore _displayPreferences;
    private readonly IDisplayCalibrationStore _displayCalibrations;
    private readonly SafeAreaCalibrationService _safeAreaCalibration;
    private readonly IGuestModeService _guestMode;
    private readonly IFrontendProfileStore _frontendProfiles;
    private readonly OnboardingSetupCoordinator _onboardingCoordinator;
    private OnboardingWindow? _setupAssistant;
    private readonly IOwnerPinService _ownerPin;
    private readonly IContentRestrictionStore _contentRestrictions;
    private readonly IItemContextMenuService _itemContextMenus;
    private readonly IReadOnlyList<IArtworkProvider> _artworkProviders;
    private readonly IProviderCredentialStore _providerCredentials;
    private readonly IArtworkJobService _artworkJobs;
    private readonly IArtworkJobProcessor _artworkJobProcessor;
    private readonly IArtworkJobPreflightService _artworkPreflight;
    private readonly IArtworkProviderConnectionTester _artworkProviderTester;
    private readonly IArtworkPresenceService _artworkPresence;
    private readonly IArtworkAssignmentStore _artworkAssignments;
    private readonly IArtworkReviewService _artworkReviews;
    private readonly IActivityTimelineService _activityTimeline;
    private readonly IActivityExportService _activityExport;
    private readonly IScreenshotCaptureService _screenshotCapture;
    private readonly ICaptureConsentService _captureConsent;
    private readonly IGlobalHotkeyService _globalHotkeys;
    private readonly ISnippingToolService _snippingTool;
    private readonly IRemotePlayProfileStore _remotePlayProfiles;
    private readonly IRemotePlayLaunchService _remotePlayLaunch;
    private readonly IEcosystemCapabilityService _ecosystemCapabilities;
    private readonly IActionTargetService _actionTargets;
    private readonly IPrimaryActionRouter _primaryActions;
    private readonly INexaMediaPlayerService _nativeMediaPlayer;
    private readonly IExternalMediaPlayerService _externalMediaPlayers;
    private readonly IEmulatorDiscoveryService _emulatorDiscovery;
    private readonly SemaphoreSlim _automaticLibraryGate=new(1,1);
    private GalleryMediaItem? _activeVideo;
    private GuestModeSettings _guestSettings = new();
    private double _interfaceScale = 1d;
    private Windows.Foundation.Point? _lastPointerPosition;
    private InterfaceLayoutType _activeLayout=InterfaceLayoutType.ModernDashboard;
    private Control? _quickMenuOrigin;
    private bool _updatingQuickVolume;
    private bool _holdInputActive;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _holdProgressTimer;
    private DateTime _holdProgressStartedUtc;
    private int _holdProgressDurationMilliseconds;
    private CancellationTokenSource? _viewerLoad;
    private IReadOnlyList<GalleryMediaItem> _viewerItems=[];
    private string? _galleryFocusId;
    private bool _xamlInitialized;
    private bool _isLoaded;
    private bool _disposed;
    private ProviderDiagnostic? _lastProviderDiagnostic;
    private CancellationTokenSource? _artworkJobMonitor;
    private ArtworkJobSnapshot? _activeArtworkJob;
    private Control? _contextMenuOrigin;
    private Control? _shelfFocusOrigin;
    private Control? _settingsFocusOrigin;
    private string _crossbarSecondaryId="continue";

    public MainWindow(
        DashboardViewModel viewModel,
        GalleryDashboardViewModel galleryViewModel,
        DiscoverAppsViewModel discoverAppsViewModel,
        IInputService inputService,
        WindowFullscreenService windowService,
        IAppLogger logger,
        ILibraryRepository library,
        IDatabaseInitializer databaseInitializer,
        ISqliteConnectionFactory connectionFactory,
        IProviderRegistry providers,
        IProviderImportService providerImports,
        IProviderPersistence providerPersistence,
        ISettingsService settings,
        IEmulationPersistence emulation,
        IEmulationLibraryService emulationLibrary,
        IEmulatorAdapterRegistry emulatorAdapters,
        IRetroArchCoreDiscovery retroArchCores,
        IWindowsApplicationLibraryService windowsApplications,
        IWindowsApplicationPersistence windowsPersistence,
        IGalleryPersistence galleryPersistence,
        IGalleryLibraryService galleryLibrary,
        IGalleryThumbnailService galleryThumbnails,
        IGalleryMediaLauncher galleryLauncher,
        IGalleryBackgroundService galleryBackgrounds,
        ISlideshowService slideshow,
        GalleryViewerSession galleryViewer,
        GalleryAlbumService galleryAlbums,
        IGallerySourceWatcherCoordinator galleryWatchers,
        MediaHubViewModel mediaViewModel,
        QuickMenuService quickMenu,
        HoldSafetyCoordinator holdSafety,
        ISystemVolumeService volume,
        IAudioOutputDeviceService audioOutputs,
        ISystemMediaControlService systemMedia,
        IControllerStatusService controllerStatus,
        INexaStationWindowActions windowActions,
        ISystemPowerService power,
        ISystemActionHistory systemActionHistory,
        ILocalNotificationService notifications,
        IMediaAssociationService mediaAssociations,
        ITimeService time,
        CustomizationViewModel customizationViewModel,
        IInterfaceLayoutService layouts,
        IDashboardConfigurationService dashboardConfiguration,
        IPinService pins,
        ICollectionService collections,
        ISmartCollectionQueryService smartCollections,
        IContentLifecycleService contentLifecycle,
        IThemeService themes,
        IThemeAssetService themeAssets,
        IUiSoundService sounds,
        IPerformancePresetService performance,
        IInterfaceConfigurationService interfaceConfiguration,
        ISemanticFocusService semanticFocus,
        IInterfacePresentationService presentation,
        IHomeDashboardDensityPolicy homeDensityPolicy,
        INexaStationPaths paths,
        IDatabaseBackupService databaseBackups,
        IDiagnosticsExportService diagnosticsExport,
        IStartupTaskService startupTask,
        ReleaseInfo releaseInfo,
        ICompatibilityProfileResolver compatibilityResolver,
        ICompatibilityProfileRepository compatibilityProfiles,
        ICompatibilityReportRepository compatibilityReports,
        CompatibilityFeedSettings compatibilityFeedSettings,
        IReproductionBundleService reproductionBundles,
        IGitHubIssuePreparationService issuePreparation,
        INavigationAuditService navigationAudit,
        IActiveInputModeService activeInputMode,
        IInputGlyphService inputGlyphs,
        IInputPromptPreferenceService promptPreferences,
        IInputBindingResolver inputBindings,
        InputBindingService bindingService,
        InputMappingService inputMappings,
        IInputProfileStore inputProfiles,
        IControllerPreferenceStore controllerPreferences,
        IInputContextService inputContext,
        IOnScreenKeyboardService onScreenKeyboard,
        IDisplaySelectionService displaySelection,
        IDisplayPreferenceStore displayPreferences,
        IDisplayCalibrationStore displayCalibrations,
        SafeAreaCalibrationService safeAreaCalibration,
        IGuestModeService guestMode,
        IFrontendProfileStore frontendProfiles,
        IOwnerPinService ownerPin,
        IContentRestrictionStore contentRestrictions,
        IItemContextMenuService itemContextMenus,
        IEnumerable<IArtworkProvider> artworkProviders,
        IProviderCredentialStore providerCredentials,
        IArtworkJobService artworkJobs,
        IArtworkJobProcessor artworkJobProcessor,
        IArtworkJobPreflightService artworkPreflight,
        IArtworkProviderConnectionTester artworkProviderTester,
        IArtworkPresenceService artworkPresence,
        IArtworkAssignmentStore artworkAssignments,
        IArtworkReviewService artworkReviews,
        IActivityTimelineService activityTimeline,
        IActivityExportService activityExport,
        IScreenshotCaptureService screenshotCapture,
        ICaptureConsentService captureConsent,
        IGlobalHotkeyService globalHotkeys,
        ISnippingToolService snippingTool,
        IRemotePlayProfileStore remotePlayProfiles,
        IRemotePlayLaunchService remotePlayLaunch,
        IEcosystemCapabilityService ecosystemCapabilities,
        IActionTargetService actionTargets,
        IPrimaryActionRouter primaryActions,
        IPrimaryActionRegistration primaryActionRegistration,
        INexaMediaPlayerService nativeMediaPlayer,
        IWindowsMediaPlayerAccessor nativeMediaEngine,
        IExternalMediaPlayerService externalMediaPlayers,
        IEmulatorDiscoveryService emulatorDiscovery,
        IThemeManager themeManager,
        ISettingsCatalog settingsCatalog,
        ISettingsSearchService settingsSearch,
        OnboardingSetupCoordinator onboardingCoordinator)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        GalleryViewModel = galleryViewModel ?? throw new ArgumentNullException(nameof(galleryViewModel));
        DiscoverAppsViewModel=discoverAppsViewModel??throw new ArgumentNullException(nameof(discoverAppsViewModel));
        MediaViewModel = mediaViewModel ?? throw new ArgumentNullException(nameof(mediaViewModel));
        CustomizationViewModel=customizationViewModel??throw new ArgumentNullException(nameof(customizationViewModel));
        _inputService = inputService;
        _windowService = windowService;
        _logger = logger;
        _library = library;
        _databaseInitializer = databaseInitializer;
        _connectionFactory = connectionFactory;
        _providers = providers;
        _providerImports = providerImports;
        _providerPersistence = providerPersistence;
        _settings = settings;
        _emulation = emulation;
        _emulationLibrary = emulationLibrary;
        _emulatorAdapters = emulatorAdapters;
        _retroArchCores = retroArchCores;
        _windowsApplications = windowsApplications;
        _windowsPersistence = windowsPersistence;
        _galleryPersistence=galleryPersistence;
        _galleryLibrary=galleryLibrary;
        _galleryThumbnails=galleryThumbnails;
        _galleryLauncher=galleryLauncher;
        _galleryBackgrounds=galleryBackgrounds;
        _slideshow=slideshow;
        _galleryViewer=galleryViewer;
        _galleryAlbums=galleryAlbums;
        _galleryWatchers=galleryWatchers;
        _quickMenu=quickMenu;_holdSafety=holdSafety;_volume=volume;_audioOutputs=audioOutputs;_systemMedia=systemMedia;_controllerStatus=controllerStatus;_windowActions=windowActions;_power=power;_systemActionHistory=systemActionHistory;_notifications=notifications;_mediaAssociations=mediaAssociations;_time=time;_layouts=layouts;_dashboardConfiguration=dashboardConfiguration;_pins=pins;_collections=collections;_smartCollections=smartCollections;_contentLifecycle=contentLifecycle;_themes=themes;_themeAssets=themeAssets;_sounds=sounds;_performance=performance;_interfaceConfiguration=interfaceConfiguration;_semanticFocus=semanticFocus;_presentation=presentation;_homeDensityPolicy=homeDensityPolicy;
        _paths=paths;_databaseBackups=databaseBackups;_diagnosticsExport=diagnosticsExport;_startupTask=startupTask;_releaseInfo=releaseInfo;
        _compatibilityResolver=compatibilityResolver;_compatibilityProfiles=compatibilityProfiles;_compatibilityReports=compatibilityReports;_compatibilityFeedSettings=compatibilityFeedSettings;_reproductionBundles=reproductionBundles;_issuePreparation=issuePreparation;_navigationAudit=navigationAudit;
        _activeInputMode=activeInputMode;_inputGlyphs=inputGlyphs;_promptPreferences=promptPreferences;_inputBindings=inputBindings;_bindingService=bindingService;_inputMappings=inputMappings;_inputProfiles=inputProfiles;_controllerPreferences=controllerPreferences;_inputContext=inputContext;_onScreenKeyboard=onScreenKeyboard;_displaySelection=displaySelection;_displayPreferences=displayPreferences;_displayCalibrations=displayCalibrations;_safeAreaCalibration=safeAreaCalibration;_guestMode=guestMode;_frontendProfiles=frontendProfiles;_ownerPin=ownerPin;_contentRestrictions=contentRestrictions;_itemContextMenus=itemContextMenus;_artworkProviders=artworkProviders.ToArray();_providerCredentials=providerCredentials;_artworkJobs=artworkJobs;_artworkJobProcessor=artworkJobProcessor;_artworkPreflight=artworkPreflight;_artworkProviderTester=artworkProviderTester;_artworkPresence=artworkPresence;_artworkAssignments=artworkAssignments;_artworkReviews=artworkReviews;
        _activityTimeline=activityTimeline;_activityExport=activityExport;_screenshotCapture=screenshotCapture;_captureConsent=captureConsent;_globalHotkeys=globalHotkeys;_snippingTool=snippingTool;_remotePlayProfiles=remotePlayProfiles;_remotePlayLaunch=remotePlayLaunch;_ecosystemCapabilities=ecosystemCapabilities;
        _actionTargets=actionTargets;_primaryActions=primaryActions;_nativeMediaPlayer=nativeMediaPlayer;_externalMediaPlayers=externalMediaPlayers;_emulatorDiscovery=emulatorDiscovery;_themeManager=themeManager;_settingsCatalog=settingsCatalog;_settingsSearch=settingsSearch;_onboardingCoordinator=onboardingCoordinator;
        RegisterPrimaryActionHandlers(primaryActionRegistration);
        RegisterQuickMenuActions();

        InitializeComponent();
        NativePlayerPage.Configure(_nativeMediaPlayer,nativeMediaEngine,_windowService,_inputGlyphs);
        NativePlayerPage.CloseRequested+=OnNativePlayerCloseRequested;
        NativePlayerPage.OpenWithRequested+=OnNativePlayerOpenWithRequested;
        NativePlayerPage.QueueMoveRequested+=OnNativePlayerQueueMoveRequested;
        _xamlInitialized = true;
        ControllerFooter.Configure(_inputGlyphs);CrossbarControllerFooter.Configure(_inputGlyphs);
        _inputService.ActionTriggered += OnInputActionTriggered;
        ViewModel.FocusRestoreRequested += OnFocusRestoreRequested;
        GalleryViewModel.FocusRestoreRequested += OnGalleryFocusRestoreRequested;
        _slideshow.ItemChanged += OnSlideshowItemChanged;
        _quickMenu.StateChanged += OnQuickMenuStateChanged;
        _controllerStatus.StatusChanged += OnControllerStatusChanged;
        _time.TimeChanged += OnTimeChanged;
        _notifications.Changed += OnNotificationsChanged;
        _presentation.PresentationChanged+=OnPresentationChanged;
        _themes.ThemeChanged+=OnThemeChanged;
        _guestMode.ProfileChanged+=OnFrontendProfileChanged;
        DiscoverAppsViewModel.StatusChanged+=OnDiscoverStatusChanged;
        DiscoverAppsViewModel.StreamingItems.CollectionChanged+=OnHomeCollectionChanged;
        _globalHotkeys.Pressed+=OnGlobalHotkeyPressed;
        GalleryViewModel.HomePreview.CollectionChanged+=OnHomeCollectionChanged;
        Activated += OnWindowActivated;
        Closed += OnClosed;
    }

    public DashboardViewModel ViewModel { get; }
    public GalleryDashboardViewModel GalleryViewModel { get; }
    public DiscoverAppsViewModel DiscoverAppsViewModel{get;}
    public MediaHubViewModel MediaViewModel { get; }
    public CustomizationViewModel CustomizationViewModel{get;}

    private void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DashboardSectionViewModel section })
        {
            if (_guestMode.IsGuest && !IsSectionAllowedForGuest(section.Section))
            {
                ViewModel.ReportStatus($"{section.Title} is unavailable in Guest Mode.");
                return;
            }
            ViewModel.SelectSection(section);
            UpdateSectionSurface();
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusSelectedSectionSurface);
            _ = _sounds.PlayAsync(UiSoundEvent.Navigate);
        }
    }

    private async void CrossbarPrimary_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not Button{Tag:string destination})return;
        if(destination=="Settings"){await OpenSettingsHomeAsync();return;}
        if(destination=="Power"){await CloseApplicationAsync();return;}
        if(!Enum.TryParse(destination,out NavigationSection section))return;
        if(_guestMode.IsGuest&&!IsSectionAllowedForGuest(section)){ViewModel.ReportStatus($"{destination} is unavailable in Guest Mode.");return;}
        _crossbarSecondaryId="continue";
        NavigateToSection(section);
    }

    private void CrossbarSecondary_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not Button{Tag:string filter})return;
        _crossbarSecondaryId=filter;
        RefreshCrossbarSurface();
        CrossbarQuickPicksList.Focus(FocusState.Programmatic);
        _=_sounds.PlayAsync(UiSoundEvent.Navigate);
    }

    private async void CrossbarFeatured_Click(object sender,RoutedEventArgs e)
    {
        if(CrossbarFeaturedCard.Card is { } card)await ShowDetailsAsync(card);
    }

    private void RefreshCrossbarSurface()
    {
        if(!_xamlInitialized)return;
        NavigationSection section=ViewModel.SelectedSection.Section;
        bool games=section==NavigationSection.Games;
        bool media=section==NavigationSection.Media;
        bool gallery=section==NavigationSection.Gallery;
        bool collections=section==NavigationSection.Collections;
        bool specialized=media||gallery||collections;
        CrossbarGamesRail.Visibility=games?Visibility.Visible:Visibility.Collapsed;
        CrossbarRailColumn.Width=new GridLength(games?390:0);
        CrossbarLibraryContent.Visibility=specialized?Visibility.Collapsed:Visibility.Visible;
        CrossbarMediaContent.Visibility=media?Visibility.Visible:Visibility.Collapsed;
        CrossbarGalleryContent.Visibility=gallery?Visibility.Visible:Visibility.Collapsed;
        CrossbarCollectionsContent.Visibility=collections?Visibility.Visible:Visibility.Collapsed;

        (Button Button,Border Indicator,NavigationSection Section)[] navigation=
        [
            (CrossbarHomeButton,CrossbarHomeIndicator,NavigationSection.Home),
            (CrossbarGamesButton,CrossbarGamesIndicator,NavigationSection.Games),
            (CrossbarAppsButton,CrossbarAppsIndicator,NavigationSection.Apps),
            (CrossbarMediaButton,CrossbarMediaIndicator,NavigationSection.Media),
            (CrossbarGalleryButton,CrossbarGalleryIndicator,NavigationSection.Gallery),
            (CrossbarLibraryButton,CrossbarLibraryIndicator,NavigationSection.Library),
        ];
        foreach((Button button,Border indicator,NavigationSection target) in navigation)
        {
            bool selected=target==section;
            button.Foreground=new SolidColorBrush(selected?Color.FromArgb(255,255,255,255):Color.FromArgb(255,129,144,172));
            button.Background=new SolidColorBrush(selected?Color.FromArgb(24,78,156,255):Color.FromArgb(0,0,0,0));
            indicator.Visibility=selected?Visibility.Visible:Visibility.Collapsed;
        }

        Button[] rails=[CrossbarContinueButton,CrossbarInstalledButton,CrossbarSteamButton,CrossbarEmulatorsButton,CrossbarFavoritesButton,CrossbarRecentButton];
        foreach(Button button in rails)
        {
            bool selected=string.Equals(button.Tag as string,_crossbarSecondaryId,StringComparison.Ordinal);
            button.Foreground=new SolidColorBrush(selected?Color.FromArgb(255,255,255,255):Color.FromArgb(255,154,169,195));
            button.Background=new SolidColorBrush(selected?Color.FromArgb(70,35,104,177):Color.FromArgb(0,0,0,0));
            button.BorderBrush=new SolidColorBrush(selected?Color.FromArgb(255,92,190,255):Color.FromArgb(0,0,0,0));
        }

        IReadOnlyList<CardViewModel> all=ViewModel.CurrentSectionItems;
        IReadOnlyList<CardViewModel> items=games?_crossbarSecondaryId switch
        {
            "continue"=>ViewModel.ContinueItems.Count>0?ViewModel.ContinueItems:all,
            "steam"=>all.Where(card=>card.Item.LaunchTargets.Any(target=>string.Equals(target.ProviderId,"steam",StringComparison.OrdinalIgnoreCase))).ToArray(),
            "emulators"=>all.Where(card=>card.Item.LaunchTargets.Any(target=>string.Equals(target.ProviderId,"emulation",StringComparison.OrdinalIgnoreCase))).ToArray(),
            "favorites"=>all.Where(card=>card.IsFavorite).ToArray(),
            "recent"=>ViewModel.RecentItems.Where(card=>card.IsGame).ToArray(),
            _=>all,
        }:all;
        CardViewModel? featured=(items.Count>0?items[0]:null)??ViewModel.FeaturedItem;
        CardViewModel[] quickPicks=items.Where(card=>featured is null||card.Id!=featured.Id).Take(12).ToArray();
        if(quickPicks.Length==0&&featured is not null)quickPicks=[featured];
        CrossbarFeaturedCard.Card=featured;
        CrossbarFeaturedButton.Visibility=featured is null?Visibility.Collapsed:Visibility.Visible;
        CrossbarFeaturedHeading.Visibility=featured is null?Visibility.Collapsed:Visibility.Visible;
        CrossbarFeaturedHeading.Text=games&&_crossbarSecondaryId=="continue"?"CONTINUE PLAYING":$"{ViewModel.CurrentSectionTitle.ToUpper(CultureInfo.CurrentCulture)} FEATURED";
        CrossbarQuickPicksTitle.Text=games?"QUICK PICKS":ViewModel.CurrentSectionTitle.ToUpper(CultureInfo.CurrentCulture);
        CrossbarQuickPicksList.ItemsSource=quickPicks;
        CrossbarQuickPicksList.Visibility=quickPicks.Length>0?Visibility.Visible:Visibility.Collapsed;
        CrossbarEmptyText.Visibility=quickPicks.Length==0?Visibility.Visible:Visibility.Collapsed;
    }

    private async void Card_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CardViewModel card)
        {
            await _sounds.PlayAsync(UiSoundEvent.Open);
            await ShowDetailsAsync(card);
        }
    }
    private async void HeroPlay_Click(object sender,RoutedEventArgs e)
    {
        if(ViewModel.FeaturedItem is not { } card)
        {
            ViewModel.SelectSection(ViewModel.Sections.First(section=>section.Section==NavigationSection.Games));
            UpdateSectionSurface();
            return;
        }
        await _sounds.PlayAsync(UiSoundEvent.Confirm);
        await ViewModel.LaunchAsync(card);
    }
    private async void HeroDetails_Click(object sender,RoutedEventArgs e)
    {
        if(ViewModel.FeaturedItem is { } card)await ShowDetailsAsync(card);
        else
        {
            ViewModel.SelectSection(ViewModel.Sections.First(section=>section.Section==NavigationSection.Library));
            UpdateSectionSurface();
        }
    }
    private async void HeroBanner_HeroIndexRequested(object? sender,int index){await ViewModel.SelectHeroAsync(index);}
    private void ToastDismiss_Click(object sender,RoutedEventArgs e)=>ViewModel.DismissNotification();
    private void ToastPrimary_Click(object sender,RoutedEventArgs e){if(ViewModel.CurrentNotification?.PrimaryAction=="Retry")RefreshSteam_Click(sender,e);}
    private async void ToastSecondary_Click(object sender,RoutedEventArgs e)
    {
        if(ViewModel.CurrentNotification?.SecondaryAction!="Open Diagnostics"||_lastProviderDiagnostic is not { } diagnostic)return;
        await Dialog("Provider Diagnostics",new TextBlock{Text=$"Provider: {diagnostic.Provider}\nOperation: {diagnostic.Operation}\nFailed entity: {diagnostic.FailedEntity}\nConstraint: {diagnostic.Constraint}\nSuggested recovery: {diagnostic.SuggestedRecovery}\nCorrelation ID: {diagnostic.CorrelationId}",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e) => await ShowItemEditorAsync();
    private async void ImportSteam_Click(object sender, RoutedEventArgs e) => await ShowSteamImportAsync();
    private async void RefreshSteam_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ProviderRefreshResult result=await _providerImports.RefreshAsync("steam");
            await ViewModel.RefreshAsync();
            ViewModel.ReportStatus($"Steam refresh: {result.Summary.Updated} updated, {result.Summary.Unchanged} unchanged, {result.Summary.Missing} missing, {result.Summary.Failed} failed.");
        }
        catch(Exception exception)
        {
            string correlationId=Guid.NewGuid().ToString("N")[..12];string constraint=exception is Microsoft.Data.Sqlite.SqliteException sqlite&&sqlite.Message.Contains("ProviderLibrarySources.Id",StringComparison.Ordinal)?"ProviderLibrarySources.Id":"Provider persistence";
            _lastProviderDiagnostic=new("Steam","Refresh","Provider library source",constraint,"Retry the refresh. If it repeats, keep the database and open diagnostics before changing Steam library settings.",correlationId);
            _logger.LogError("SteamRefreshFailed","Steam refresh persistence failed.",exception,new Dictionary<string,object?>{{"Provider","Steam"},{"Operation","Refresh"},{"FailedEntity","Provider library source"},{"Constraint",constraint},{"SuggestedRecovery",_lastProviderDiagnostic.SuggestedRecovery},{"CorrelationId",correlationId}});
            ViewModel.ReportProviderFailure();
        }
    }
    private async void SteamSettings_Click(object sender,RoutedEventArgs e)=>await ShowSteamSettingsAsync();
    private async void SettingsHome_Click(object sender,RoutedEventArgs e)=>await OpenSettingsHomeAsync();
    private async void ScanLaunchers_Click(object sender,RoutedEventArgs e)
    {
        try{await ShowLauncherImportAsync();}
        catch(Exception exception){_logger.LogError("LauncherImportFailed","Launcher discovery or import failed.",exception);ViewModel.ReportStatus($"Launcher import could not complete: {exception.Message}");await _sounds.PlayAsync(UiSoundEvent.Error);}
    }
    private async void ImportEmulatedGames_Click(object sender,RoutedEventArgs e)=>await StartEmulatedGameImportAsync();
    private async void EmulationSettings_Click(object sender,RoutedEventArgs e)=>await ShowEmulationSettingsAsync();
    private async void ImportWindowsApps_Click(object sender,RoutedEventArgs e)=>await ShowWindowsApplicationImportAsync();
    private async void WindowsAppsSettings_Click(object sender,RoutedEventArgs e)=>await ShowWindowsApplicationSettingsAsync();
    private async void DesktopControlSettings_Click(object sender,RoutedEventArgs e)=>await ShowDesktopControlSettingsAsync();
    private async void AddGalleryFolder_Click(object sender,RoutedEventArgs e)=>await AddGalleryFolderAsync();
    private async void AddVideoFolder_Click(object sender,RoutedEventArgs e)=>await AddVideoFolderAsync();
    private async void RefreshGallery_Click(object sender,RoutedEventArgs e)=>await RefreshGalleryAsync();
    private async void GallerySettings_Click(object sender,RoutedEventArgs e)=>await ShowGallerySettingsAsync();
    private async void MetadataArtworkSettings_Click(object sender,RoutedEventArgs e)=>await ShowMetadataArtworkSettingsAsync();
    private async void FindMissingArtwork_Click(object sender,RoutedEventArgs e)=>await ShowFindMissingArtworkAsync();
    private async void ReviewArtworkMatches_Click(object sender,RoutedEventArgs e)=>await ShowArtworkReviewQueueAsync();
    private async void QuickMenuSettings_Click(object sender,RoutedEventArgs e)=>await ShowQuickMenuSettingsAsync();
    private async void StartupRecoverySettings_Click(object sender,RoutedEventArgs e)=>await ShowStartupRecoverySettingsAsync();
    private async void CompatibilityFeedback_Click(object sender,RoutedEventArgs e)=>await ShowCompatibilityFeedbackAsync();
    private async void ReportProblem_Click(object sender,RoutedEventArgs e)=>await ShowReportProblemAsync();
    private async void About_Click(object sender,RoutedEventArgs e)=>await ShowAboutAsync();
    private async void CloseApplication_Click(object sender,RoutedEventArgs e)=>await CloseApplicationAsync();
    private async void CreateAlbum_Click(object sender,RoutedEventArgs e)=>await CreateGalleryAlbumAsync();
    private async void RetryDatabase_Click(object sender, RoutedEventArgs e){try{await _databaseInitializer.InitializeAsync();await ViewModel.RefreshAsync();ViewModel.ReportStatus("The local library is ready.");}catch(Exception ex){ViewModel.ReportStatus($"Retry failed: {ex.Message}");}}
    private void OpenDataFolder_Click(object sender, RoutedEventArgs e){_paths.EnsureCreated();Process.Start(new ProcessStartInfo{FileName=_paths.Root,UseShellExecute=true});}

    private Task OpenSettingsHomeAsync(string? settingId=null)
    {
        if(SettingsOverlay.Visibility!=Visibility.Visible)_settingsFocusOrigin=FocusManager.GetFocusedElement(Root.XamlRoot) as Control;
        SettingsViewHost.Content=_themeManager.CreateSettingsView(new(_settingsCatalog,_settingsSearch,OpenSettingFromHubAsync,ResetSettingsCategoryAsync,CloseSettingsHomeAsync,settingId));
        SettingsOverlay.Visibility=Visibility.Visible;
        SettingsViewHost.Focus(FocusState.Programmatic);
        return Task.CompletedTask;
    }

    private Task CloseSettingsHomeAsync()
    {
        SettingsOverlay.Visibility=Visibility.Collapsed;SettingsViewHost.Content=null;
        if(_settingsFocusOrigin is { } origin)origin.Focus(FocusState.Programmatic);else FocusRememberedOrFirstCard();
        _settingsFocusOrigin=null;return Task.CompletedTask;
    }

    private async Task CloseApplicationAsync()
    {
        if(_guestMode.IsGuest&&!_guestSettings.AllowDesktopExit){ViewModel.ReportStatus("Closing NexaStation is disabled in Guest Mode.");return;}
        ContentDialogResult result=await Dialog("Close NexaStation?",new TextBlock{Text="The application will close. Active external games, applications, and media are not terminated.",TextWrapping=TextWrapping.Wrap},"Close NexaStation",null,"Cancel").ShowAsync();
        if(result!=ContentDialogResult.Primary)return;
        SettingsOverlay.Visibility=Visibility.Collapsed;SettingsViewHost.Content=null;Close();
    }

    private async Task ApplyActiveUiThemeAsync()
    {
        bool crossbar=string.Equals(_themeManager.ActiveThemeId,"nexa.crossbar",StringComparison.Ordinal);
        string targetLayout=crossbar?InterfaceLayoutService.CrossbarId:InterfaceLayoutService.ModernId;
        bool layoutChanged=!string.Equals(_layouts.CurrentLayoutId,targetLayout,StringComparison.Ordinal);
        if(layoutChanged)
        {
            await CustomizationViewModel.ApplyLayoutAsync(targetLayout);
            await ViewModel.RefreshAsync();
        }
        if(crossbar)ApplyCrossbarThemeResources();
        else
        {
            await CustomizationViewModel.ApplyThemeAsync(CustomizationViewModel.CurrentThemeId);
            BrandSubtitle.Text="GAMES · APPS · MEDIA";
            DashboardBackgroundImage.Opacity=.2;
            ControllerFooter.Background=null;
        }
        if(_isLoaded)UpdateSectionSurface();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,FocusSelectedSectionSurface);
    }

    private void ApplyCrossbarThemeResources()
    {
        Color background=Color.FromArgb(255,4,18,48);Color surface=Color.FromArgb(225,7,27,57);Color accent=Color.FromArgb(255,82,220,255);Color muted=Color.FromArgb(255,173,216,232);
        Application.Current.Resources["NexaBackgroundColor"]=background;
        Application.Current.Resources["NexaBackgroundBrush"]=new SolidColorBrush(background);
        Application.Current.Resources["NexaSurfaceBrush"]=new SolidColorBrush(surface);
        Application.Current.Resources["NexaAccentColor"]=accent;
        Application.Current.Resources["NexaAccentBrush"]=new SolidColorBrush(accent);
        Application.Current.Resources["NexaAccentBorderBrush"]=new SolidColorBrush(Color.FromArgb(160,accent.R,accent.G,accent.B));
        Application.Current.Resources["NexaMutedTextBrush"]=new SolidColorBrush(muted);
        LinearGradientBrush gradient=new(){StartPoint=new(0,0),EndPoint=new(1,1),GradientStops={new GradientStop{Color=background,Offset=0},new GradientStop{Color=Color.FromArgb(255,7,74,112),Offset=.58},new GradientStop{Color=Color.FromArgb(255,5,15,38),Offset=1}}};
        Root.Background=gradient;WindowRoot.Background=gradient;TopHeader.Background=new SolidColorBrush(Color.FromArgb(224,3,16,39));ControllerFooter.Background=new SolidColorBrush(Color.FromArgb(224,3,16,39));BrandSubtitle.Text="CROSSBAR · GAMES · MEDIA";DashboardBackgroundImage.Opacity=.06;
    }

    private async Task ResetSettingsCategoryAsync(string categoryId)
    {
        SettingsCategoryDescriptor category=_settingsCatalog.Categories.Single(value=>value.Id==categoryId);
        if(await Dialog("Reset section",new TextBlock{Text=$"Restore defaults for {category.Title}? Settings in other categories are not changed.",TextWrapping=TextWrapping.Wrap},"Reset",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        try
        {
            foreach(SettingDescriptor setting in _settingsCatalog.GetCategory(categoryId).Where(value=>value.ValueBinding is not null&&value.DefaultValue is not null&&!value.Dangerous))await _settings.SetAsync(setting.ValueBinding!,setting.DefaultValue);
            ViewModel.ReportStatus($"{category.Title} defaults restored.");
        }
        catch(Exception exception){await Dialog("Failed to save",new TextBlock{Text=$"The section could not be reset. {exception.Message}",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();}
    }

    private async Task OpenSettingFromHubAsync(string settingId)
    {
        try
        {
            if(settingId=="personalization.theme"){await ShowThemeSelectionAsync();return;}
            if(settingId=="emulators.find"){await ShowEmulatorDiscoveryAsync();return;}
            if(settingId=="artwork.bulk"){await ShowFindMissingArtworkAsync();return;}
            if(settingId=="artwork.review"){await ShowArtworkReviewQueueAsync();return;}
            if(settingId=="help.report"){await ShowReportProblemAsync();return;}
            if(settingId=="storage.location"){OpenDataFolder_Click(this,new RoutedEventArgs());return;}
            if(settingId=="reset.input"){await RestoreInputDefaultsAsync();return;}
            if(settingId=="startup.close-application"){await CloseApplicationAsync();return;}
            string category=_settingsCatalog.GetById(settingId).CategoryId;
            switch(category)
            {
                case "personalization":await ShowAppearanceSettingsAsync();break;
                case "input":InputSettings_Click(this,new RoutedEventArgs());break;
                case "providers":await ShowSteamSettingsAsync();break;
                case "emulators":await ShowEmulationSettingsAsync();break;
                case "artwork":await ShowMetadataArtworkSettingsAsync();break;
                case "applications":await ShowWindowsApplicationSettingsAsync();break;
                case "media-player" or "audio":await ShowQuickMenuSettingsAsync();break;
                case "gallery":await ShowGallerySettingsAsync();break;
                case "capture":CaptureSettings_Click(this,new RoutedEventArgs());break;
                case "display":DisplaySettings_Click(this,new RoutedEventArgs());break;
                case "network":RemotePlaySettings_Click(this,new RoutedEventArgs());break;
                case "privacy":if(settingId=="privacy.guest")GuestSettings_Click(this,new RoutedEventArgs());else await ShowCompatibilityFeedbackAsync();break;
                case "startup" or "updates" or "storage":await ShowStartupRecoverySettingsAsync();break;
                case "accessibility":await ShowAppearanceSettingsAsync();break;
                case "help":await ShowAboutAsync();break;
                case "advanced":await ShowCompatibilityFeedbackAsync();break;
                case "reset-recovery":await Dialog("Reset and Recovery",new TextBlock{Text="This disruptive action is intentionally isolated. The requested reset is not available from this build-safe surface unless an existing validated recovery command implements it.",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();break;
            }
        }
        catch(Exception exception){await Dialog("Setting unavailable",new TextBlock{Text=$"This setting could not be opened. {exception.Message}",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();}
    }

    private async Task ShowThemeSelectionAsync()
    {
        ThemeDescriptor[] themes=_themeManager.AvailableThemes.ToArray();ComboBox selector=new(){ItemsSource=themes,DisplayMemberPath=nameof(ThemeDescriptor.DisplayName),SelectedItem=themes.FirstOrDefault(value=>value.Id==_themeManager.ActiveThemeId)??themes[0],MinWidth=420};ContentControl previewHost=new(){Height=310,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};TextBlock details=new(){TextWrapping=TextWrapping.Wrap,Opacity=.75};Button preview=new(){Content="Preview",HorizontalAlignment=HorizontalAlignment.Left};Button cancelPreview=new(){Content="Cancel Preview",HorizontalAlignment=HorizontalAlignment.Left};StackPanel actions=new(){Orientation=Orientation.Horizontal,Spacing=10};actions.Children.Add(preview);actions.Children.Add(cancelPreview);StackPanel panel=new(){Spacing=12};panel.Children.Add(selector);panel.Children.Add(details);panel.Children.Add(previewHost);panel.Children.Add(actions);
        void UpdateDetails(){if(selector.SelectedItem is ThemeDescriptor selected)details.Text=$"{selected.Description}\nNavigation: {selected.NavigationStyle} · Input: {selected.RecommendedInput} · Performance: {selected.RelativePerformance}\nAccessibility: High Contrast {Yes(selected.SupportsHighContrast)}, Reduced Motion {Yes(selected.SupportsReducedMotion)} · Version {selected.Version}";}
        selector.SelectionChanged+=(_,_)=>UpdateDetails();UpdateDetails();
        preview.Click+=async(_,_)=>{if(selector.SelectedItem is not ThemeDescriptor selected)return;ThemeChangeResult result=await _themeManager.PreviewAsync(selected.Id);if(result.Succeeded)previewHost.Content=result.Shell;else details.Text=result.Error;};
        cancelPreview.Click+=async(_,_)=>{await _themeManager.CancelPreviewAsync();previewHost.Content=null;selector.SelectedItem=themes.First(value=>value.Id==_themeManager.ActiveThemeId);};
        ContentDialogResult choice=await Dialog("Settings · Personalization · Theme",panel,"Apply",null,"Close").ShowAsync();
        if(choice!=ContentDialogResult.Primary){await _themeManager.CancelPreviewAsync();return;}
        if(selector.SelectedItem is not ThemeDescriptor theme)return;ThemeChangeResult applied=await _themeManager.ApplyAsync(theme.Id);if(!applied.Succeeded){await Dialog("Theme could not be loaded",new TextBlock{Text=applied.Error??"NexaStation could not load a usable theme.",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();return;}await ApplyActiveUiThemeAsync();if(applied.FellBackToSafeTheme){await Dialog("Default theme restored",new TextBlock{Text="The selected theme could not be loaded, so NexaStation restored the built-in Nexa theme.",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();ViewModel.ReportStatus("The built-in Nexa theme was restored safely.");}else ViewModel.ReportStatus($"{theme.DisplayName} applied to the full dashboard.");if(SettingsOverlay.Visibility==Visibility.Visible)await OpenSettingsHomeAsync("personalization.theme");
        static string Yes(bool value)=>value?"Yes":"No";
    }

    private void LibrarySearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) ViewModel.Search(sender.Text);
    }
    private void LibraryView_SelectionChanged(object sender,SelectionChangedEventArgs e){if(sender is ComboBox{SelectedItem:string value})ViewModel.SetLibraryView(value);}
    private void LibrarySort_SelectionChanged(object sender,SelectionChangedEventArgs e){if(sender is ComboBox{SelectedItem:string value})ViewModel.SetLibrarySort(value);}
    private void LibraryPresentation_SelectionChanged(object sender,SelectionChangedEventArgs e){if(sender is ComboBox{SelectedItem:string value})ViewModel.SetLibraryPresentation(value);}
    private void GamesPresentation_SelectionChanged(object sender,SelectionChangedEventArgs e){if(sender is ComboBox{SelectedItem:string value})ViewModel.SetGamesPresentation(value);}
    private async void AdaptiveLibrary_ItemInvoked(object? sender,CardViewModel card){await _sounds.PlayAsync(UiSoundEvent.Open);await ShowDetailsAsync(card);}

    private async void Root_Loaded(object sender,RoutedEventArgs e)
    {
        _isLoaded = true;
        HeaderClock.Text=_time.Current.DisplayTime;
        _=RefreshHeaderControllerAsync();
        if(CustomizationViewModel.Presentation is { } presentation)ApplyPresentation(presentation);
        await ApplyActiveUiThemeAsync();
        ApplyHomeDensity();
        RefreshSectionSurface();
        await ApplyPersistedDisplayAsync();
        FrontendProfile ownerProfile = await _frontendProfiles.GetOwnerAsync();
        _localUserName = ownerProfile.Name;
        _ownerAvatarKey = ownerProfile.AvatarKey ?? "owner";
        _guestSettings = await _frontendProfiles.GetGuestSettingsAsync();
        ApplyGuestPresentation();
        try{await EnsureAutomaticSmartLibrariesAsync();}
        catch(Exception exception){_logger.LogError("AutomaticLibrariesFailed","Automatic provider and emulator libraries could not be synchronized.",exception);}
        await UpdateBatteryStatusAsync();
        ArtworkJobSnapshot? recent=(await _artworkJobs.ListAsync()).FirstOrDefault(job=>job.Status is not ArtworkJobStatus.Completed and not ArtworkJobStatus.CompletedWithErrors and not ArtworkJobStatus.Cancelled and not ArtworkJobStatus.Failed)??(await _artworkJobs.ListAsync()).FirstOrDefault(job=>job.CompletedAtUtc>DateTimeOffset.UtcNow.AddDays(-1));if(recent is not null)_=MonitorArtworkJobAsync(recent.Id);
    }
    private void Root_SizeChanged(object sender,SizeChangedEventArgs e){if(_xamlInitialized&&_isLoaded)ApplyHomeDensity();}
    private void WindowRoot_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyInterfaceScale(_interfaceScale);
    private void Root_GettingFocus(UIElement sender,GettingFocusEventArgs args)
    {
        if(_quickMenu.IsOpen&&_holdSafety.IsHolding)_holdSafety.OnFocusChanged();
        UpdateActionTarget(args.NewFocusedElement as DependencyObject);
    }

    private void RegisterPrimaryActionHandlers(IPrimaryActionRegistration registration)
    {
        foreach(string itemType in new[]{"Game","Application","LibraryItem","GalleryMedia","Video","Collection","DiscoverApplication"})
            registration.Register(itemType,ExecuteRegisteredPrimaryActionAsync);
    }

    private void UpdateActionTarget(DependencyObject? focused)
    {
        object? data=ContextTargetFrom(focused);
        if(data is null){_actionTargets.ClearFocusedTarget("Content");return;}
        (string id,string type,bool available,IReadOnlySet<InputAction> actions)=data switch
        {
            CardViewModel card=>(card.Id,card.Item.ContentType==ContentType.Game?"Game":card.Item.ContentType==ContentType.Application?"Application":"LibraryItem",LaunchTargetValidator.Validate(card.Item.LaunchTarget) is null,new HashSet<InputAction>{InputAction.Confirm,InputAction.ContextMenu,InputAction.Options,InputAction.Favorite,InputAction.ToggleDetails}),
            GalleryCardViewModel card=>(card.Id,card.Item.MediaType==GalleryMediaType.Video?"Video":"GalleryMedia",card.Item.Available,new HashSet<InputAction>{InputAction.Confirm,InputAction.ContextMenu,InputAction.Options,InputAction.Favorite,InputAction.ToggleDetails}),
            CollectionCardViewModel card=>(card.Id,"Collection",true,new HashSet<InputAction>{InputAction.Confirm,InputAction.ContextMenu,InputAction.Options}),
            DiscoverAppCardViewModel card=>(card.Definition.Id,"DiscoverApplication",card.PrimaryAction!="Unavailable",new HashSet<InputAction>{InputAction.Confirm,InputAction.ContextMenu,InputAction.Options,InputAction.ToggleDetails}),
            _=>(string.Empty,string.Empty,false,new HashSet<InputAction>()),
        };
        if(string.IsNullOrWhiteSpace(id)){_actionTargets.ClearFocusedTarget("Content");return;}
        _actionTargets.SetFocusedTarget(new("MainWindow","Content",ViewModel.SelectedSection.Id,null,id,type,data,actions,DateTimeOffset.UtcNow,"ActionableFocus",available));
    }

    private ActionTargetResolution ResolveActionTarget(InputAction action)
    {
        UpdateActionTarget(FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject);
        return _actionTargets.Resolve(action,new("MainWindow","Content",ViewModel.SelectedSection.Id));
    }

    private async Task<PrimaryActionResult> ExecuteRegisteredPrimaryActionAsync(InputAction action,ActionTargetSnapshot target,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();object? data=target.DataContext;
        if(action is InputAction.ContextMenu or InputAction.Options){if(data is null)return new(false,"The focused item is no longer available.");await OpenContextMenuAsync(data,FindFocusableOrigin(FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject));return new(true);}
        switch(data)
        {
            case CardViewModel card when action==InputAction.Confirm:await ViewModel.LaunchAsync(card);return new(true);
            case GalleryCardViewModel card when action==InputAction.Confirm:await OpenGalleryCardAsync(card);return new(true);
            case CollectionCardViewModel card when action==InputAction.Confirm:await OpenCollectionAsync(card);return new(true);
            case DiscoverAppCardViewModel card when action==InputAction.Confirm:await DiscoverAppsViewModel.ExecutePrimaryAsync(card,cancellationToken);return new(true);
            case CardViewModel card when action==InputAction.Favorite:await ViewModel.ToggleFavoriteAsync(card);return new(true);
            case GalleryCardViewModel card when action==InputAction.Favorite:await GalleryViewModel.ToggleFavoriteAsync(card,cancellationToken);return new(true);
            case CardViewModel card when action==InputAction.ToggleDetails:await ShowDetailsAsync(card);return new(true);
            case GalleryCardViewModel card when action==InputAction.ToggleDetails:await ShowGalleryDetailsAsync(card);return new(true);
            case DiscoverAppCardViewModel card when action==InputAction.ToggleDetails:await ExecuteDiscoverActionAsync(card,"discover.details");return new(true);
            default:return new(false,"The focused item does not support this action.");
        }
    }

    private async Task OpenCollectionAsync(CollectionCardViewModel card)
    {
        await ShowShelfDetailAsync(await _collections.GetByIdAsync(card.Id)??card.Definition);
    }
    public void RefreshSectionSurface()
    {
        // SelectedIndex raises SelectionChanged while InitializeComponent is still
        // constructing controls. The Loaded event performs the first safe refresh.
        if (!_xamlInitialized || !_isLoaded) return;
        EnsureSectionControlsInitialized();
        UpdateSectionSurface();
    }

    private void UpdateSectionSurface()
    {
        bool gallery=ViewModel.SelectedSection.Section==NavigationSection.Gallery;
        bool media=ViewModel.SelectedSection.Section==NavigationSection.Media;
        bool collections=ViewModel.SelectedSection.Section==NavigationSection.Collections;
        bool home=ViewModel.SelectedSection.Section==NavigationSection.Home;
        bool apps=ViewModel.SelectedSection.Section==NavigationSection.Apps;
        bool shared=!gallery&&!media&&!collections;
        bool crossbarSurface=_activeLayout==InterfaceLayoutType.ClassicCrossbar;
        DashboardContent.Visibility=shared&&_activeLayout==InterfaceLayoutType.ModernDashboard?Visibility.Visible:Visibility.Collapsed;
        CrossbarRoot.Visibility=crossbarSurface?Visibility.Visible:Visibility.Collapsed;
        HandheldRoot.Visibility=shared&&_activeLayout==InterfaceLayoutType.CompactHandheld?Visibility.Visible:Visibility.Collapsed;
        GalleryRoot.Visibility=gallery&&!crossbarSurface?Visibility.Visible:Visibility.Collapsed;
        MediaRoot.Visibility=media&&!crossbarSurface?Visibility.Visible:Visibility.Collapsed;
        CollectionsRoot.Visibility=collections&&!crossbarSurface?Visibility.Visible:Visibility.Collapsed;
        ModernHomeSurface.Visibility=home?Visibility.Visible:Visibility.Collapsed;
        AppsCatalogueRoot.Visibility=apps?Visibility.Visible:Visibility.Collapsed;
        ModernSectionSurface.Visibility=home||apps?Visibility.Collapsed:Visibility.Visible;
        LibraryToolsPanel.Visibility=ViewModel.SelectedSection.Section==NavigationSection.Library?Visibility.Visible:Visibility.Collapsed;
        GamesToolsPanel.Visibility=ViewModel.SelectedSection.Section==NavigationSection.Games?Visibility.Visible:Visibility.Collapsed;
        TopHeader.Visibility=crossbarSurface?Visibility.Collapsed:Visibility.Visible;
        ControllerFooter.Visibility=crossbarSurface?Visibility.Collapsed:Visibility.Visible;
        DashboardBackgroundImage.Visibility=crossbarSurface?Visibility.Collapsed:Visibility.Visible;
        if(crossbarSurface)RefreshCrossbarSurface();
        if(media)_=MediaViewModel.RefreshAsync();
        if(collections)_=CustomizationViewModel.RefreshCollectionsAsync();
        if(apps)_=DiscoverAppsViewModel.RefreshAsync();
        if(home)ApplyHomeDensity();
    }

    private void ApplyHomeDensity()
    {
        if(!_xamlInitialized||Root.ActualWidth<=0||Root.ActualHeight<=0)return;
        HomeDashboardLayout layout=_homeDensityPolicy.Resolve(Root.ActualWidth,Root.ActualHeight);
        ApplyHeaderDensity(layout.Density);
        TopHeader.Height=layout.HeaderHeight;TopHeader.Padding=layout.Density switch{HomeDashboardDensity.Large=>new Thickness(60,14,60,14),HomeDashboardDensity.Standard=>new Thickness(48,11,48,11),_=>new Thickness(28,9,28,9)};
        ControllerFooter.Height=layout.FooterHeight;DashboardFrame.Margin=new Thickness(layout.OuterMargin,layout.Density==HomeDashboardDensity.Compact?10:12,layout.OuterMargin,layout.Density==HomeDashboardDensity.Compact?24:14);DashboardFrame.Spacing=layout.Density==HomeDashboardDensity.Compact?18:13;HeroBanner.Height=layout.HeroHeight;
        ModernHomeSurface.RowSpacing=layout.Density switch{HomeDashboardDensity.Large=>24,HomeDashboardDensity.Standard=>13,_=>18};
        foreach(CardViewModel card in ViewModel.ContinueItems)card.SetDisplaySize(layout.GameCardWidth,layout.GameCardHeight);
        foreach(CardViewModel card in ViewModel.RecentAppItems.Concat(ViewModel.FavoriteItems))card.SetDisplaySize(layout.SquareTileSize,layout.SquareTileSize);
        foreach(CardViewModel card in ViewModel.RecentItems)card.SetDisplaySize(layout.ActivityRowWidth,layout.ActivityRowHeight);
        foreach(GalleryCardViewModel card in GalleryViewModel.HomePreview)card.SetDisplaySize(layout.PhotoCardWidth,layout.PhotoCardHeight);
        foreach(DiscoverAppCardViewModel card in DiscoverAppsViewModel.StreamingItems)card.SetDisplaySize(layout.SquareTileSize);
        GamesEndpoint.Width=layout.EndpointWidth;GamesEndpoint.Height=layout.GameCardHeight;
        AppsEndpoint.Width=layout.EndpointWidth;AppsEndpoint.Height=layout.SquareTileSize;
        PhotosEndpoint.Width=layout.EndpointWidth;PhotosEndpoint.Height=layout.PhotoCardHeight;
        MediaEndpoint.Width=layout.EndpointWidth;MediaEndpoint.Height=layout.SquareTileSize;
        FavoritesEndpoint.Width=layout.EndpointWidth;FavoritesEndpoint.Height=layout.SquareTileSize;
        RecentEndpoint.Width=layout.EndpointWidth;RecentEndpoint.Height=Math.Max(layout.SquareTileSize,layout.ActivityRowHeight*2+8);
        DashboardContent.VerticalScrollMode=layout.UsesVerticalScrolling?ScrollMode.Enabled:ScrollMode.Disabled;DashboardContent.VerticalScrollBarVisibility=layout.UsesVerticalScrolling?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden;
        PlaceHomePanels(layout.Density);BalanceHomePanels(layout.Density);
    }

    private void OnHomeCollectionChanged(object? sender,NotifyCollectionChangedEventArgs args)
    {
        if(!_isLoaded)return;
        DispatcherQueue.TryEnqueue(ApplyHomeDensity);
    }

    private void ApplyHeaderDensity(HomeDashboardDensity density)
    {
        bool compact=density==HomeDashboardDensity.Compact;
        TopHeader.ColumnDefinitions[0].Width=new GridLength(compact?190:238);
        foreach(ToggleButton button in VisualDescendants<ToggleButton>(HeaderSections))
        {
            button.MinWidth=compact?66:88;
            button.MinHeight=compact?44:48;
            button.Padding=compact?new Thickness(9,8,9,8):new Thickness(16,10,16,10);
            button.FontSize=compact?13:15;
        }
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject root) where T:DependencyObject
    {
        int count=VisualTreeHelper.GetChildrenCount(root);
        for(int index=0;index<count;index++)
        {
            DependencyObject child=VisualTreeHelper.GetChild(root,index);
            if(child is T match)yield return match;
            foreach(T descendant in VisualDescendants<T>(child))yield return descendant;
        }
    }

    private void PlaceHomePanels(HomeDashboardDensity density)
    {
        bool compact=density==HomeDashboardDensity.Compact;BrandSubtitle.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        Grid.SetColumn(ApplicationsPanel,0);Grid.SetRow(ApplicationsPanel,0);Grid.SetColumnSpan(ApplicationsPanel,compact?2:1);
        Grid.SetColumn(RecentPhotosPanel,compact?0:1);Grid.SetRow(RecentPhotosPanel,compact?1:0);Grid.SetColumnSpan(RecentPhotosPanel,compact?2:1);
        Grid.SetColumn(HomeMediaPanel,0);Grid.SetRow(HomeMediaPanel,0);Grid.SetColumnSpan(HomeMediaPanel,compact?3:1);
        Grid.SetColumn(FavoritesPanel,compact?0:1);Grid.SetRow(FavoritesPanel,compact?1:0);Grid.SetColumnSpan(FavoritesPanel,compact?3:1);
        Grid.SetColumn(RecentlyAddedPanel,compact?0:2);Grid.SetRow(RecentlyAddedPanel,compact?2:0);Grid.SetColumnSpan(RecentlyAddedPanel,compact?3:1);
    }

    private void BalanceHomePanels(HomeDashboardDensity density)
    {
        HomeMediaPanel.Visibility=DiscoverAppsViewModel.StreamingItems.Count>0?Visibility.Visible:Visibility.Collapsed;RecentlyAddedPanel.Visibility=ViewModel.RecentItems.Count>0?Visibility.Visible:Visibility.Collapsed;
        if(density==HomeDashboardDensity.Compact)return;
        bool applications=ViewModel.ApplicationSectionVisibility==Visibility.Visible,photos=GalleryViewModel.HomePreviewVisibility==Visibility.Visible;
        if(!applications&&photos){Grid.SetColumn(RecentPhotosPanel,0);Grid.SetColumnSpan(RecentPhotosPanel,2);}else if(applications&&!photos){Grid.SetColumn(ApplicationsPanel,0);Grid.SetColumnSpan(ApplicationsPanel,2);}
        bool media=HomeMediaPanel.Visibility==Visibility.Visible,favorites=FavoritesPanel.Visibility==Visibility.Visible,recent=RecentlyAddedPanel.Visibility==Visibility.Visible;
        if(media&&!favorites&&recent){Grid.SetColumnSpan(HomeMediaPanel,2);Grid.SetColumn(RecentlyAddedPanel,2);}else if(!media&&favorites&&recent){Grid.SetColumn(FavoritesPanel,0);Grid.SetColumn(RecentlyAddedPanel,1);Grid.SetColumnSpan(RecentlyAddedPanel,2);}else if(media&&favorites&&!recent){Grid.SetColumnSpan(FavoritesPanel,2);}else if(media&&!favorites&&!recent)Grid.SetColumnSpan(HomeMediaPanel,3);else if(!media&&favorites&&!recent){Grid.SetColumn(FavoritesPanel,0);Grid.SetColumnSpan(FavoritesPanel,3);}else if(!media&&!favorites&&recent){Grid.SetColumn(RecentlyAddedPanel,0);Grid.SetColumnSpan(RecentlyAddedPanel,3);}
    }

    private void EnsureSectionControlsInitialized()
    {
        if (DashboardContent is null || ModernHomeSurface is null || AppsCatalogueRoot is null || InstalledAppsPanel is null || DiscoverAppsPanel is null || ModernSectionSurface is null || LibraryToolsPanel is null || GamesToolsPanel is null || SectionAdaptiveGrid is null || CrossbarRoot is null || HandheldRoot is null || CollectionsRoot is null || GalleryRoot is null || MediaRoot is null || RecentPhotosPanel is null || HeroBanner is null)
            throw new InvalidOperationException("MainWindow section controls were not initialized after MainWindow.Loaded. Verify MainWindow.xaml and InitializeComponent().");
    }

    // Gallery presentation state is owned by GalleryDashboardViewModel. Legacy call
    // sites remain as a post-refresh invariant check so a broken initialization is
    // surfaced immediately without dereferencing XAML controls during construction.
    private void UpdateGalleryEmptyState()
    {
        if (GalleryViewModel.Rows is null || GalleryViewModel.HomePreview is null || GalleryViewModel.Sources is null)
            throw new InvalidOperationException("Gallery rows were not initialized before the view was refreshed.");
    }
    private void OnPresentationChanged(object? sender,LayoutPresentationState state)=>DispatcherQueue.TryEnqueue(()=>ApplyPresentation(state));
    private void ApplyPresentation(LayoutPresentationState state)
    {
        _activeLayout=state.LayoutType;if(!_xamlInitialized)return;DashboardContent.Margin=new Thickness(0,8,0,0);CrossbarRoot.Margin=state.LayoutType==InterfaceLayoutType.ClassicCrossbar?new Thickness(0):new Thickness(state.OuterMargin,8,state.OuterMargin,state.OuterMargin);HandheldRoot.Margin=new Thickness(state.OuterMargin,6,state.OuterMargin,state.OuterMargin);UpdateSectionSurface();
    }
    private void OnThemeChanged(object? sender,NexaStation.Customization.Models.ThemeChangedEventArgs args)=>DispatcherQueue.TryEnqueue(()=>ApplyTheme(args.Theme));
    private void ApplyTheme(ThemeDefinition theme)
    {
        if(!_xamlInitialized)return;Root.RequestedTheme=theme.ThemeType==ThemeType.Light?ElementTheme.Light:ElementTheme.Dark;Color accent=ParseColor(theme.AccentColor);Color readableAccentText=ParseColor(ThemeService.ReadableForeground(theme.AccentColor));Application.Current.Resources["NexaAccentColor"]=accent;Application.Current.Resources["NexaAccentBrush"]=new SolidColorBrush(accent);Application.Current.Resources["NexaInverseAccentTextBrush"]=new SolidColorBrush(readableAccentText);Application.Current.Resources["NexaAccentBorderBrush"]=new SolidColorBrush(Color.FromArgb(128,accent.R,accent.G,accent.B));Application.Current.Resources["NexaAccentTintBrush"]=new SolidColorBrush(Color.FromArgb(64,accent.R,accent.G,accent.B));string backgroundValue=theme.Background.Type is BackgroundType.Solid or BackgroundType.Gradient&&theme.Background.Value?.StartsWith('#')==true?theme.Background.Value.Split(',')[0]:"#090B13";Color background=theme.ThemeType==ThemeType.Light?ParseColor("#F4F6FA"):theme.ThemeType==ThemeType.HighContrast?Colors.Black:ParseColor(backgroundValue);Application.Current.Resources["NexaBackgroundColor"]=background;Application.Current.Resources["NexaBackgroundBrush"]=new SolidColorBrush(background);Application.Current.Resources["NexaSurfaceBrush"]=new SolidColorBrush(theme.ThemeType==ThemeType.Light?ParseColor("#FFFDFD"):theme.ThemeType==ThemeType.HighContrast?Colors.Black:ParseColor("#171B2B"));Application.Current.Resources["NexaMutedTextBrush"]=new SolidColorBrush(theme.ThemeType==ThemeType.Light?ParseColor("#445064"):theme.ThemeType==ThemeType.HighContrast?Colors.White:ParseColor("#A8B2C7"));Root.Background=theme.Background.Type switch{BackgroundType.Gradient=>new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1),GradientStops={new GradientStop{Color=background,Offset=0},new GradientStop{Color=ParseColor(theme.SecondaryAccentColor??theme.AccentColor),Offset=1}}},BackgroundType.Image when theme.Background.Value is { } path&&File.Exists(path)=>new ImageBrush{ImageSource=new BitmapImage(new Uri(path)),Stretch=theme.Background.Stretch==BackgroundStretch.Fit?Stretch.Uniform:theme.Background.Stretch==BackgroundStretch.Center?Stretch.None:Stretch.UniformToFill,Opacity=theme.Background.Opacity*(1-theme.Background.DarkeningOverlay)},_=>new SolidColorBrush(background)};
    }
    private static Color ParseColor(string value){string hex=value.TrimStart('#');if(hex.Length!=6)return Colors.DeepSkyBlue;return Color.FromArgb(255,byte.Parse(hex.AsSpan(0,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),byte.Parse(hex.AsSpan(2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),byte.Parse(hex.AsSpan(4,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture));}
    private void GallerySearch_TextChanged(AutoSuggestBox sender,AutoSuggestBoxTextChangedEventArgs args){if(args.Reason==AutoSuggestionBoxTextChangeReason.UserInput)GalleryViewModel.Search(sender.Text);}
    private async void GalleryView_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!_isLoaded)return;if(sender is ComboBox{SelectedItem:string value}){GalleryViewKind view=value switch{"Timeline"=>GalleryViewKind.Timeline,"Folders"=>GalleryViewKind.Folders,"Grid"=>GalleryViewKind.Grid,"Albums"=>GalleryViewKind.Albums,_=>GalleryViewKind.Home};await GalleryViewModel.SetViewAsync(view);}}
    private async void GalleryGrid_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!_isLoaded)return;if(sender is ComboBox{SelectedItem:string value})await GalleryViewModel.SetGridAsync(value switch{"Small"=>GalleryGridSize.Small,"Large"=>GalleryGridSize.Large,_=>GalleryGridSize.Medium});}
    private async void GallerySort_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!_isLoaded)return;if(sender is ComboBox{SelectedItem:string value}){GallerySortMode sort=value switch{"Oldest captured"=>GallerySortMode.OldestCaptured,"Recently added"=>GallerySortMode.RecentlyAdded,"Recently opened"=>GallerySortMode.RecentlyOpened,"Filename"=>GallerySortMode.Filename,"File size"=>GallerySortMode.FileSize,_=>GallerySortMode.NewestCaptured};await GalleryViewModel.SetSortAsync(sort);}}

    private async void GalleryCard_Loaded(object sender,RoutedEventArgs e){if(sender is FrameworkElement{DataContext:GalleryCardViewModel card})try{await card.EnsureThumbnailAsync();}catch(Exception exception){_logger.LogError("GalleryThumbnailDisplayFailed","A visible Gallery thumbnail could not be prepared.",exception);}}
    private async void GalleryCard_ItemClick(object sender,ItemClickEventArgs e){if(e.ClickedItem is GalleryCardViewModel card)await OpenGalleryCardAsync(card);}
    private async Task AddGalleryFolderAsync()
    {
        ComboBox type=new(){Header="Gallery source type",ItemsSource=Enum.GetNames<GallerySourceKind>(),SelectedIndex=0,MinWidth=320};if(await Dialog("Add Gallery Folder · 1 of 4",type,"Choose Folder",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;string? folder=await PickFolderAsync();if(folder is null)return;
        IReadOnlyList<GallerySourceOverlap> overlaps=await _galleryLibrary.PreviewSourceAsync(folder);if(overlaps.Count>0){string summary=string.Join(Environment.NewLine,overlaps.Select(value=>$"{value.Kind}: {value.Existing.Name}"));ContentDialogResult overlap=await Dialog("Overlapping Gallery source",new TextBlock{Text=summary+"\n\nOverlapping scans may increase processing cost. Exact duplicates cannot be added.",TextWrapping=TextWrapping.Wrap},overlaps.Any(value=>value.Kind==GallerySourceOverlapKind.Exact)?"Open Existing Source":"Add Anyway",null,"Cancel").ShowAsync();if(overlap!=ContentDialogResult.Primary||overlaps.Any(value=>value.Kind==GallerySourceOverlapKind.Exact)){if(overlap==ContentDialogResult.Primary){await GalleryViewModel.SetSourceAsync(overlaps[0].Existing.Id);ViewModel.SelectSection(ViewModel.Sections.First(value=>value.Section==NavigationSection.Gallery));UpdateSectionSurface();}return;}}
        TextBox name=new(){Header="Source display name",Text=Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),MinWidth=360};CheckBox subfolders=new(){Content="Include subfolders",IsChecked=true};CheckBox images=new(){Content="Include images",IsChecked=true};CheckBox videos=new(){Content="Include videos",IsChecked=true};CheckBox hidden=new(){Content="Include hidden files",IsChecked=false};CheckBox watch=new(){Content="Watch for changes (optional, debounced)",IsChecked=false};CheckBox privateSource=new(){Content="Private · hide from Home, global search, and recent activity",IsChecked=false};StackPanel options=new(){Spacing=8};options.Children.Add(name);options.Children.Add(subfolders);options.Children.Add(images);options.Children.Add(videos);options.Children.Add(hidden);options.Children.Add(watch);options.Children.Add(privateSource);if(await Dialog("Add Gallery Folder · 3 of 4",options,"Privacy Review",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        StackPanel privacy=new(){Spacing=10};privacy.Children.Add(new TextBlock{Text="NexaStation will index filenames and local metadata from this folder. Files remain on this PC and are not uploaded.",TextWrapping=TextWrapping.Wrap});privacy.Children.Add(new TextBlock{Text="Source media is opened read-only. NexaStation never deletes, moves, renames, edits, or uploads it.",TextWrapping=TextWrapping.Wrap,Opacity=0.75});ContentDialogResult action=await Dialog("Add Gallery Folder · 4 of 4",privacy,"Add and Scan","Add Without Scanning","Cancel").ShowAsync();if(action==ContentDialogResult.None)return;DateTime now=DateTime.UtcNow;GallerySource source=new(){Id=Guid.NewGuid().ToString("N"),Name=string.IsNullOrWhiteSpace(name.Text)?Path.GetFileName(folder):name.Text.Trim(),FolderPath=folder,NormalizedFolderPath=folder,SourceKind=Enum.TryParse(type.SelectedItem?.ToString(),out GallerySourceKind parsed)?parsed:GallerySourceKind.Custom,IncludeSubfolders=subfolders.IsChecked==true,IncludeImages=images.IsChecked==true,IncludeVideos=videos.IsChecked==true,IncludeHiddenFiles=hidden.IsChecked==true,WatchForChanges=watch.IsChecked==true,PrivacyLevel=privateSource.IsChecked==true?GalleryPrivacyLevel.Private:GalleryPrivacyLevel.Normal,ShowOnHome=privateSource.IsChecked!=true,CreatedAtUtc=now,UpdatedAtUtc=now};try{GallerySource saved=await _galleryLibrary.AddSourceAsync(source,true,action==ContentDialogResult.Primary);await _galleryWatchers.ReloadAsync();await GalleryViewModel.RefreshAsync();UpdateGalleryEmptyState();ViewModel.ReportStatus($"Added Gallery source {saved.Name}. Original files were not modified.");}catch(Exception exception){ViewModel.ReportStatus($"Gallery source could not be added: {exception.Message}");}
    }

    private async Task AddVideoFolderAsync()
    {
        string? folder=await PickFolderAsync();if(folder is null)return;
        IReadOnlyList<GallerySourceOverlap> overlaps=await _galleryLibrary.PreviewSourceAsync(folder);
        if(overlaps.Any(value=>value.Kind==GallerySourceOverlapKind.Exact)){ViewModel.ReportStatus($"{overlaps[0].Existing.Name} already indexes this folder.");return;}
        TextBox name=new(){Header="Video source name",Text=Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)),MinWidth=380};
        CheckBox subfolders=new(){Content="Scan subfolders",IsChecked=true};CheckBox watch=new(){Content="Watch for new videos",IsChecked=true};CheckBox privateSource=new(){Content="Private source",IsChecked=false};
        StackPanel options=new(){Spacing=9};options.Children.Add(new TextBlock{Text="NexaStation will scan this approved folder for local video files only. Files remain read-only and are never moved or uploaded.",TextWrapping=TextWrapping.Wrap});options.Children.Add(name);options.Children.Add(subfolders);options.Children.Add(watch);options.Children.Add(privateSource);
        if(await Dialog("Add Local Video Folder",options,"Add and Scan",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        DateTime now=DateTime.UtcNow;string normalized=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));GallerySource source=new(){Id=Guid.NewGuid().ToString("N"),Name=string.IsNullOrWhiteSpace(name.Text)?Path.GetFileName(normalized):name.Text.Trim(),FolderPath=normalized,NormalizedFolderPath=normalized,SourceKind=GallerySourceKind.Videos,IncludeSubfolders=subfolders.IsChecked==true,IncludeImages=false,IncludeVideos=true,WatchForChanges=watch.IsChecked==true,PrivacyLevel=privateSource.IsChecked==true?GalleryPrivacyLevel.Private:GalleryPrivacyLevel.Normal,ShowOnHome=false,CreatedAtUtc=now,UpdatedAtUtc=now};
        try{GallerySource saved=await _galleryLibrary.AddSourceAsync(source,true,true);await _galleryWatchers.ReloadAsync();await GalleryViewModel.RefreshAsync();await MediaViewModel.RefreshAsync();UpdateGalleryEmptyState();ViewModel.ReportStatus($"Scanned {saved.Name} for local videos.");}catch(Exception exception){ViewModel.ReportStatus($"Video folder could not be added: {exception.Message}");}
    }

    private async Task RefreshGalleryAsync(){IReadOnlyList<GallerySource> sources=await _galleryPersistence.GetSourcesAsync();int added=0,updated=0,missing=0,failed=0;foreach(GallerySource source in sources.Where(value=>value.Enabled)){try{GalleryRefreshResult result=await _galleryLibrary.RefreshSourceAsync(source.Id);added+=result.Added;updated+=result.Updated;missing+=result.Missing;failed+=result.Failed;}catch(Exception exception){failed++;_logger.LogError("GalleryRefreshFailed","A Gallery source refresh failed.",exception);}}await GalleryViewModel.RefreshAsync();UpdateGalleryEmptyState();ViewModel.ReportStatus($"Gallery refresh: {added} added, {updated} updated, {missing} missing, {failed} failed.");}
    private async Task ShowGallerySettingsAsync()
    {
        GallerySource[] sources=(await _galleryPersistence.GetSourcesAsync()).ToArray();GalleryThumbnailCacheStats stats=await _galleryThumbnails.GetCacheStatsAsync();ComboBox sourceBox=new(){Header="Gallery source",ItemsSource=sources,DisplayMemberPath="Name",SelectedIndex=sources.Length>0?0:-1,MinWidth=400};CheckBox enabled=new(){Content="Source enabled",IsChecked=sources.FirstOrDefault()?.Enabled};CheckBox watch=new(){Content="Watch for changes",IsChecked=sources.FirstOrDefault()?.WatchForChanges};CheckBox privateSource=new(){Content="Private source",IsChecked=sources.FirstOrDefault()?.PrivacyLevel==GalleryPrivacyLevel.Private};sourceBox.SelectionChanged+=(_,_)=>{if(sourceBox.SelectedItem is GallerySource source){enabled.IsChecked=source.Enabled;watch.IsChecked=source.WatchForChanges;privateSource.IsChecked=source.PrivacyLevel==GalleryPrivacyLevel.Private;}};
        bool removeRequested=false,locateRequested=false;Button locate=new(){Content="Locate Selected Source"};Button remove=new(){Content="Remove Selected Source and Index"};StackPanel actions=new(){Orientation=Orientation.Horizontal,Spacing=8};actions.Children.Add(locate);actions.Children.Add(remove);StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text="Only folders you explicitly approve are indexed. Source media remains local and is never deleted.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(sourceBox);panel.Children.Add(enabled);panel.Children.Add(watch);panel.Children.Add(privateSource);panel.Children.Add(actions);panel.Children.Add(new TextBlock{Text=$"Sources: {sources.Length}\nThumbnail cache: {stats.DiskEntries} files · {stats.DiskBytes/1024d/1024d:F1} MB disk · {stats.MemoryBytes/1024d/1024d:F1} MB memory",TextWrapping=TextWrapping.Wrap,Opacity=0.75});ContentDialog settingsDialog=Dialog("Settings · Gallery",panel,"Save","Clear Thumbnail Cache","Close");locate.Click+=(_,_)=>{locateRequested=true;settingsDialog.Hide();};remove.Click+=(_,_)=>{removeRequested=true;settingsDialog.Hide();};ContentDialogResult result=await settingsDialog.ShowAsync();
        if(removeRequested&&sourceBox.SelectedItem is GallerySource removeSource){if(await Dialog("Remove Gallery source?",new TextBlock{Text="The Gallery index for this source will be removed. Original files will not be deleted.",TextWrapping=TextWrapping.Wrap},"Remove Source and Index",null,"Cancel").ShowAsync()==ContentDialogResult.Primary){await _galleryLibrary.RemoveSourceAsync(removeSource.Id,false);await _galleryWatchers.ReloadAsync();await GalleryViewModel.RefreshAsync();UpdateGalleryEmptyState();ViewModel.ReportStatus("Gallery source and its NexaStation index were removed. Original media was untouched.");}return;}
        if(locateRequested&&sourceBox.SelectedItem is GallerySource locateSource){string? folder=await PickFolderAsync();if(folder is not null){IReadOnlyList<GallerySourceOverlap> overlaps=await _galleryPersistence.FindSourceOverlapsAsync(folder);if(overlaps.Any(value=>value.Existing.Id!=locateSource.Id)){ViewModel.ReportStatus("The selected folder overlaps another Gallery source. Relink was cancelled.");return;}await _galleryPersistence.UpdateSourceAsync(locateSource with{FolderPath=folder,NormalizedFolderPath=Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)),Available=true,UpdatedAtUtc=DateTime.UtcNow});await _galleryWatchers.ReloadAsync();await _galleryLibrary.RefreshSourceAsync(locateSource.Id);await GalleryViewModel.RefreshAsync();ViewModel.ReportStatus("Gallery source relinked without changing media IDs, favorites, or albums.");}return;}
        if(result==ContentDialogResult.Primary&&sourceBox.SelectedItem is GallerySource selected){await _galleryPersistence.UpdateSourceAsync(selected with{Enabled=enabled.IsChecked==true,WatchForChanges=watch.IsChecked==true,PrivacyLevel=privateSource.IsChecked==true?GalleryPrivacyLevel.Private:GalleryPrivacyLevel.Normal,ShowOnHome=privateSource.IsChecked!=true,UpdatedAtUtc=DateTime.UtcNow});await _galleryWatchers.ReloadAsync();await GalleryViewModel.RefreshAsync();ViewModel.ReportStatus("Gallery source settings saved.");}else if(result==ContentDialogResult.Secondary){await _galleryThumbnails.ClearCacheAsync();await GalleryViewModel.RefreshAsync();ViewModel.ReportStatus("Gallery thumbnail cache cleared. Original media was not touched.");}
    }
    private async Task CreateGalleryAlbumAsync(){TextBox name=new(){Header="Album name",MinWidth=340};TextBox description=new(){Header="Description (optional)",MinWidth=340};StackPanel panel=new(){Spacing=8};panel.Children.Add(name);panel.Children.Add(description);if(await Dialog("Create Album",panel,"Create",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||string.IsNullOrWhiteSpace(name.Text))return;await _galleryAlbums.CreateAsync(name.Text,description.Text);await GalleryViewModel.RefreshAsync();ViewModel.ReportStatus($"Created album {name.Text.Trim()}.");}

    private async Task ShowMetadataArtworkSettingsAsync()
    {
        IReadOnlyList<ArtworkJobSnapshot> jobs=await _artworkJobs.ListAsync();
        List<string> statusLines=[];
        ArtworkJobPreflightResult providerReadiness=await _artworkPreflight.CheckAsync(new("Provider status",["status"],[ArtworkKind.CoverPortrait]));
        foreach(ArtworkProviderReadiness provider in providerReadiness.Providers)statusLines.Add($"{provider.DisplayName}: {(provider.Enabled&&provider.Available&&provider.Authenticated?"Ready":provider.Enabled?"Setup required":"Disabled")}{(string.IsNullOrWhiteSpace(provider.Message)?string.Empty:$" · {provider.Message}")}");
        TextBlock providerStatus=new(){Text=string.Join(Environment.NewLine,statusLines),TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
        CheckBox onlineArtwork=new(){Content="Allow online artwork lookups",IsChecked=await _settings.GetAsync("Metadata.Privacy.AllowOnlineArtwork",false)};
        CheckBox onlineMetadata=new(){Content="Allow online metadata lookups",IsChecked=await _settings.GetAsync("Metadata.Privacy.AllowOnlineMetadata",false)};
        CheckBox serialLookup=new(){Content="Allow game serial and title-ID lookup",IsChecked=await _settings.GetAsync("Metadata.Privacy.AllowSerialLookup",false)};
        CheckBox fuzzyLookup=new(){Content="Allow fuzzy title lookup (ambiguous results require review)",IsChecked=await _settings.GetAsync("Metadata.Privacy.AllowFuzzyTitleLookup",false)};
        CheckBox regionLookup=new(){Content="Include region in provider searches",IsChecked=await _settings.GetAsync("Metadata.Privacy.IncludeRegion",true)};
        CheckBox fullHash=new(){Content="Enable full ROM hashing for small cartridge files only",IsChecked=await _settings.GetAsync("Metadata.Matching.EnableSmallRomHashing",false)};
        IArtworkProvider[] accountProviders=_artworkProviders.Where(value=>value.Id is "steamgriddb" or "thegamesdb").OrderBy(value=>value.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToArray();int selectedProviderIndex=0;for(int index=0;index<accountProviders.Length;index++)if(await _providerCredentials.GetAsync(accountProviders[index].Id) is not null){selectedProviderIndex=index;break;}
        ComboBox accountProvider=new(){Header="Free artwork provider",ItemsSource=accountProviders,DisplayMemberPath="DisplayName",SelectedIndex=accountProviders.Length>0?selectedProviderIndex:-1,MinWidth=420};
        TextBox accountName=new(){Header="Account label (optional)",MinWidth=420};
        PasswordBox secret=new(){Header="Personal API key",PasswordRevealMode=PasswordRevealMode.Peek,MinWidth=420};
        TextBlock connectionResult=new(){Text="These providers offer free personal API access but require an account-issued key. Keys are stored by Windows Credential Manager and never written to NexaStation's database, settings, logs, or diagnostics.",TextWrapping=TextWrapping.Wrap,Opacity=.75};
        Button getKey=new(){Content="Get Free API Key"};Button test=new(){Content="Save and Test"};Button remove=new(){Content="Remove Account"};StackPanel accountActions=new(){Orientation=Orientation.Horizontal,Spacing=8};accountActions.Children.Add(getKey);accountActions.Children.Add(test);accountActions.Children.Add(remove);
        ComboBox jobBox=new(){Header="Persistent artwork job",ItemsSource=jobs,DisplayMemberPath="Name",SelectedIndex=jobs.Count>0?0:-1,MinWidth=420};TextBlock jobDetails=new(){TextWrapping=TextWrapping.Wrap,Opacity=.8};void UpdateJobDetails(){jobDetails.Text=jobBox.SelectedItem is ArtworkJobSnapshot job?$"{OutcomeLabel(job)} · {job.CompletedItems}/{job.TotalItems} complete · {job.AppliedItems} applied · {job.NoMatchItems} no match · {job.ReviewItems} review · {job.FailedItems} failed":"No artwork jobs.";}jobBox.SelectionChanged+=(_,_)=>UpdateJobDetails();UpdateJobDetails();
        Button pauseJob=new(){Content="Pause"};Button resumeJob=new(){Content="Resume"};Button cancelJob=new(){Content="Cancel"};StackPanel jobActions=new(){Orientation=Orientation.Horizontal,Spacing=8};foreach(Button button in new[]{pauseJob,resumeJob,cancelJob})jobActions.Children.Add(button);pauseJob.Click+=async(_,_)=>{if(jobBox.SelectedItem is ArtworkJobSnapshot job)try{await _artworkJobs.PauseAsync(job.Id);ViewModel.ReportStatus($"Paused artwork job {job.Name}.");}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}};resumeJob.Click+=async(_,_)=>{if(jobBox.SelectedItem is ArtworkJobSnapshot job)try{await _artworkJobs.ResumeAsync(job.Id);_=_artworkJobProcessor.RunAsync(job.Id);ViewModel.ReportStatus($"Resumed artwork job {job.Name}.");}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}};cancelJob.Click+=async(_,_)=>{if(jobBox.SelectedItem is ArtworkJobSnapshot job)try{await _artworkJobs.CancelAsync(job.Id);ViewModel.ReportStatus($"Cancelled artwork job {job.Name}.");}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}};
        Button openLocal=new(){Content="Open Local Artwork Import Folder"};openLocal.Click+=(_,_)=>{string folder=Path.Combine(_paths.Data,"Artwork","Import");Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});};
        StackPanel panel=new(){Spacing=10};panel.Children.Add(new TextBlock{Text="Providers",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Add(providerStatus);panel.Children.Add(new TextBlock{Text="Privacy",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});foreach(UIElement setting in new UIElement[]{onlineArtwork,onlineMetadata,serialLookup,fuzzyLookup,regionLookup,fullHash})panel.Children.Add(setting);panel.Children.Add(new TextBlock{Text="Accounts",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Add(accountProvider);panel.Children.Add(accountName);panel.Children.Add(secret);panel.Children.Add(accountActions);panel.Children.Add(connectionResult);panel.Children.Add(new TextBlock{Text=$"Managed artwork: {_paths.Data}\\Artwork\nPersistent jobs: {jobs.Count} retained\nDefault policy: Fill Missing Only · never replace user-selected or locked artwork.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        bool removeRequested=false;ContentDialog dialog=Dialog("Settings · Metadata and Artwork",new ScrollViewer{Content=panel,MaxHeight=650},"Save",null,"Close");
        panel.Children.Insert(panel.Children.Count-1,new TextBlock{Text="Jobs and local files",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Insert(panel.Children.Count-1,jobBox);panel.Children.Insert(panel.Children.Count-1,jobDetails);panel.Children.Insert(panel.Children.Count-1,jobActions);panel.Children.Insert(panel.Children.Count-1,openLocal);
        getKey.Click+=(_,_)=>{if(accountProvider.SelectedItem is not IArtworkProvider selected){connectionResult.Text="Choose a provider first.";return;}string url=selected.Id=="steamgriddb"?"https://www.steamgriddb.com/profile/preferences/api":"https://api.thegamesdb.net/key.php";Process.Start(new ProcessStartInfo(url){UseShellExecute=true});connectionResult.Text=$"Opened the official {selected.DisplayName} key page. Sign in, create or copy your personal free key, then return here to save and test it.";};
        test.Click+=async(_,_)=>{if(accountProvider.SelectedItem is not IArtworkProvider selected){connectionResult.Text="Choose a provider first.";return;}try{if(!string.IsNullOrWhiteSpace(secret.Password))await _providerCredentials.SaveAsync(new(selected.Id,secret.Password,accountName.Text.Trim()));ArtworkProviderConnectionTestResult tested=await _artworkProviderTester.TestAsync(selected.Id);if(tested.ValidationState==ProviderValidationState.Valid)onlineArtwork.IsChecked=true;connectionResult.Text=tested.Message+(tested.ValidationState==ProviderValidationState.Valid?" Online artwork has been enabled; choose Save to keep the privacy setting.":string.Empty);}catch(Exception exception){connectionResult.Text=$"Connection test failed safely: {exception.Message}";}};
        remove.Click+=(_,_)=>{removeRequested=true;dialog.Hide();};
        ContentDialogResult result=await dialog.ShowAsync();
        try
        {
            if(removeRequested&&accountProvider.SelectedItem is IArtworkProvider removeProvider){await _providerCredentials.DeleteAsync(removeProvider.Id);await _artworkProviderTester.SetEnabledAsync(removeProvider.Id,false);ViewModel.ReportStatus($"Removed the protected {removeProvider.DisplayName} account and disabled the provider.");return;}
            if(result!=ContentDialogResult.Primary)return;
            ArtworkProviderConnectionTestResult? validation=null;if(accountProvider.SelectedItem is IArtworkProvider selectedProvider){if(!string.IsNullOrWhiteSpace(secret.Password))await _providerCredentials.SaveAsync(new(selectedProvider.Id,secret.Password,accountName.Text.Trim()));if(await _providerCredentials.GetAsync(selectedProvider.Id) is not null)validation=await _artworkProviderTester.TestAsync(selectedProvider.Id);if(validation?.ValidationState==ProviderValidationState.Valid)onlineArtwork.IsChecked=true;}
            await _settings.SetAsync("Metadata.Privacy.AllowOnlineArtwork",onlineArtwork.IsChecked==true);await _settings.SetAsync("Metadata.Privacy.AllowOnlineMetadata",onlineMetadata.IsChecked==true);await _settings.SetAsync("Metadata.Privacy.AllowSerialLookup",serialLookup.IsChecked==true);await _settings.SetAsync("Metadata.Privacy.AllowFuzzyTitleLookup",fuzzyLookup.IsChecked==true);await _settings.SetAsync("Metadata.Privacy.IncludeRegion",regionLookup.IsChecked==true);await _settings.SetAsync("Metadata.Matching.EnableSmallRomHashing",fullHash.IsChecked==true);
            ViewModel.ReportStatus(validation is null?"Metadata, artwork, matching, and privacy settings saved.":validation.ValidationState==ProviderValidationState.Valid?$"{validation.Message} Settings saved.":$"Settings saved, but {validation.Message}");
        }
        finally{secret.Password=string.Empty;}
    }

    private async Task ShowFindMissingArtworkAsync(string? singleItemId=null)
    {
        LibraryItem[] games = singleItemId is null
            ? (await _library.QueryAsync(new(ContentType:ContentType.Game,IncludeHidden:false,MaximumResults:5000))).ToArray()
            : (await _library.GetByIdAsync(singleItemId)) is { ContentType:ContentType.Game } selected ? [selected] : [];
        if(games.Length==0){ViewModel.ReportStatus("No eligible games were found for artwork search.");return;}

        CheckBox cover=new(){Content="Cover artwork",IsChecked=true};
        CheckBox hero=new(){Content="Hero background",IsChecked=true};
        CheckBox wide=new(){Content="Wide grid artwork",IsChecked=false};
        CheckBox logo=new(){Content="Logo",IsChecked=false};
        CheckBox fillMissing=new(){Content="Fill missing artwork only",IsChecked=true};
        CheckBox protectUser=new(){Content="Never replace user-selected artwork",IsChecked=true,IsEnabled=false};
        NumberBox concurrency=new(){Header="Maximum concurrent requests",Minimum=1,Maximum=4,Value=2,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};
        TextBlock privacy=new(){Text=await _settings.GetAsync("Metadata.Privacy.AllowOnlineArtwork",false)
            ? "Online artwork is enabled. The selected provider may receive title, platform, and—only if separately enabled—serial/region lookup data."
            : "Online artwork is disabled. This job will search only the explicitly configured local artwork folder.",TextWrapping=TextWrapping.Wrap,Opacity=.75};
        StackPanel panel=new(){Spacing=9};
        panel.Children.Add(new TextBlock{Text=$"Scope: {(singleItemId is null?$"{games.Length} visible games":games[0].Title)}",TextWrapping=TextWrapping.Wrap});
        foreach(UIElement option in new UIElement[]{cover,hero,wide,logo,fillMissing,protectUser,concurrency,privacy})panel.Children.Add(option);
        if(await Dialog(singleItemId is null?"Find Missing Artwork":"Find Artwork",panel,"Create Job",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        List<ArtworkKind> types=[];if(cover.IsChecked==true)types.Add(ArtworkKind.CoverPortrait);if(hero.IsChecked==true)types.Add(ArtworkKind.HeroBackground);if(wide.IsChecked==true)types.Add(ArtworkKind.GridWide);if(logo.IsChecked==true)types.Add(ArtworkKind.Logo);
        if(types.Count==0){ViewModel.ReportStatus("Choose at least one artwork type.");return;}

        EmulatedIdentityRepairResult repaired=await _emulationLibrary.RepairIdentitiesAsync(games.Select(game=>game.Id).ToArray());if(repaired.Updated>0)await ViewModel.RefreshAsync();
        List<string> eligible=[];
        foreach(LibraryItem game in games)
        {
            bool needs=false;
            foreach(ArtworkKind type in types)
            {
                if(fillMissing.IsChecked!=true||!await _artworkPresence.IsPresentAsync(game.Id,type)){needs=true;break;}
            }
            if(needs)eligible.Add(game.Id);
        }
        if(eligible.Count==0){ViewModel.ReportStatus("Every selected game already has the requested artwork.");return;}
        ArtworkJobRequest request=new(singleItemId is null?"Find missing game artwork":$"Artwork for {games[0].Title}",eligible,types,fillMissing.IsChecked==true,true,900,(int)concurrency.Value);ArtworkJobPreflightResult preflight=await _artworkPreflight.CheckAsync(request);if(!preflight.CanStart){StackPanel blocked=new(){Spacing=8};blocked.Children.Add(new TextBlock{Text=string.Join(Environment.NewLine,preflight.BlockingReasons),TextWrapping=TextWrapping.Wrap});blocked.Children.Add(new TextBlock{Text=string.Join(Environment.NewLine,preflight.Providers.Select(provider=>$"{provider.DisplayName}: {(provider.Enabled?provider.Message??provider.FailureCode??"Not ready":"Disabled")}")),TextWrapping=TextWrapping.Wrap,Opacity=.75});if(await Dialog("Artwork provider setup required",blocked,"Open Settings",null,"Cancel").ShowAsync()==ContentDialogResult.Primary)await ShowMetadataArtworkSettingsAsync();return;}
        string jobId=await _artworkJobs.CreateAsync(request);
        await _artworkJobs.StartAsync(jobId);
        _=_artworkJobProcessor.RunAsync(jobId);
        _=MonitorArtworkJobAsync(jobId);ViewModel.ReportStatus($"Artwork job started for {eligible.Count} game{(eligible.Count==1?string.Empty:"s")}. Live progress is shown above the library.");
    }

    private async Task MonitorArtworkJobAsync(string jobId)
    {
        _artworkJobMonitor?.Cancel();_artworkJobMonitor?.Dispose();_artworkJobMonitor=new();CancellationToken token=_artworkJobMonitor.Token;int applied=0;
        try
        {
            await foreach(ArtworkJobSnapshot snapshot in _artworkJobs.WatchAsync(jobId,token))
            {
                _activeArtworkJob=snapshot;UpdateArtworkJobStrip(snapshot);if(snapshot.AppliedItems>applied){IReadOnlyList<ArtworkJobItemSnapshot> items=await _artworkJobs.ListItemsAsync(jobId,token);await ViewModel.RefreshArtworkAsync(items.Where(item=>item.Status==ArtworkJobItemStatus.Applied).Select(item=>item.LibraryItemId),token);applied=snapshot.AppliedItems;}
                if(snapshot.Outcome is ArtworkJobOutcome.Succeeded or ArtworkJobOutcome.Partial or ArtworkJobOutcome.NoArtworkFound or ArtworkJobOutcome.NeedsReview or ArtworkJobOutcome.Failed or ArtworkJobOutcome.Cancelled){await ViewModel.RefreshAsync();ViewModel.ReportStatus($"{snapshot.Name}: {OutcomeLabel(snapshot)}. {snapshot.AppliedItems} applied, {snapshot.NoMatchItems} no match, {snapshot.ReviewItems} review, {snapshot.FailedItems} failed.");}
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception exception){ViewModel.ReportStatus($"Artwork progress could not be monitored: {exception.Message}");}
    }
    private void UpdateArtworkJobStrip(ArtworkJobSnapshot job)
    {
        if(!_xamlInitialized)return;ArtworkJobStrip.Visibility=Visibility.Visible;ArtworkJobTitle.Text=job.Name;ArtworkJobStage.Text=OutcomeLabel(job);ArtworkJobProgress.Maximum=Math.Max(1,job.TotalItems);ArtworkJobProgress.Value=Math.Min(job.CompletedItems,ArtworkJobProgress.Maximum);ArtworkJobCounts.Text=$"{job.CompletedItems}/{job.TotalItems} · {job.AppliedItems} applied · {job.ReviewItems} review · {job.NoMatchItems} no match · {job.FailedItems} failed";bool paused=job.Status is ArtworkJobStatus.Paused or ArtworkJobStatus.Offline or ArtworkJobStatus.RateLimited;bool final=job.Outcome is ArtworkJobOutcome.Succeeded or ArtworkJobOutcome.Partial or ArtworkJobOutcome.NoArtworkFound or ArtworkJobOutcome.NeedsReview or ArtworkJobOutcome.Failed or ArtworkJobOutcome.Cancelled;ArtworkPauseButton.Visibility=!paused&&!final?Visibility.Visible:Visibility.Collapsed;ArtworkResumeButton.Visibility=paused?Visibility.Visible:Visibility.Collapsed;ArtworkCancelButton.Visibility=!final?Visibility.Visible:Visibility.Collapsed;
    }
    private static string OutcomeLabel(ArtworkJobSnapshot job)=>job.Outcome switch{ArtworkJobOutcome.Succeeded=>"Finished · artwork applied",ArtworkJobOutcome.Partial=>"Finished with some unmatched items",ArtworkJobOutcome.NoArtworkFound=>"Finished · no artwork found",ArtworkJobOutcome.NeedsReview=>"Waiting for match review",ArtworkJobOutcome.Failed=>"Artwork job failed",ArtworkJobOutcome.Cancelled=>"Artwork job cancelled",_=>job.Status switch{ArtworkJobStatus.Identifying=>"Identifying games",ArtworkJobStatus.Searching=>"Searching providers",ArtworkJobStatus.Downloading=>"Downloading and validating artwork",ArtworkJobStatus.Paused=>"Paused",ArtworkJobStatus.RateLimited=>"Paused by provider rate limit",ArtworkJobStatus.Offline=>"Paused while offline",_=>"Preparing"}};
    private async void ArtworkPause_Click(object sender,RoutedEventArgs e){if(_activeArtworkJob is null)return;try{await _artworkJobs.PauseAsync(_activeArtworkJob.Id);}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}}
    private async void ArtworkResume_Click(object sender,RoutedEventArgs e){if(_activeArtworkJob is null)return;try{await _artworkJobs.ResumeAsync(_activeArtworkJob.Id);_=_artworkJobProcessor.RunAsync(_activeArtworkJob.Id);_ = MonitorArtworkJobAsync(_activeArtworkJob.Id);}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}}
    private async void ArtworkCancel_Click(object sender,RoutedEventArgs e){if(_activeArtworkJob is null)return;try{await _artworkJobs.CancelAsync(_activeArtworkJob.Id);UpdateArtworkJobStrip(await _artworkJobs.GetAsync(_activeArtworkJob.Id));}catch(Exception exception){ViewModel.ReportStatus(exception.Message);}}
    private async void ArtworkDetails_Click(object sender,RoutedEventArgs e){if(_activeArtworkJob is not null)await ShowArtworkJobDetailsAsync(_activeArtworkJob.Id);}
    private async Task ShowArtworkJobDetailsAsync(string jobId)
    {
        ArtworkJobSnapshot job=await _artworkJobs.GetAsync(jobId);ArtworkJobRequest request=await _artworkJobs.GetRequestAsync(jobId);ArtworkJobPreflightResult readiness=await _artworkPreflight.CheckAsync(request);IReadOnlyList<ArtworkJobItemSnapshot> items=await _artworkJobs.ListItemsAsync(jobId);string providers=string.Join(Environment.NewLine,readiness.Providers.Select(provider=>$"{provider.DisplayName}: {(provider.Enabled&&provider.Available&&provider.Authenticated?"Ready":provider.Message??provider.FailureCode??"Not ready")}"));string itemLines=string.Join(Environment.NewLine,items.Select(item=>$"{item.Title} · {item.Status}{(item.SelectedProviderId is null?string.Empty:$" · {item.SelectedProviderId}")}{(item.MatchConfidence is null?string.Empty:$" · score {item.MatchConfidence}")}{(item.FailureMessage is null?string.Empty:$" · {item.FailureMessage}")}"));StackPanel panel=new(){Spacing=9};panel.Children.Add(new TextBlock{Text=$"{OutcomeLabel(job)}\n{job.CompletedItems}/{job.TotalItems} complete · {job.AppliedItems} applied · {job.NoMatchItems} no match · {job.ReviewItems} review · {job.FailedItems} failed",TextWrapping=TextWrapping.Wrap});panel.Children.Add(new TextBlock{Text="Providers",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text=providers,TextWrapping=TextWrapping.Wrap,Opacity=.8});panel.Children.Add(new TextBlock{Text="Items",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text=itemLines,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true});bool retryable=job.Outcome is ArtworkJobOutcome.Succeeded or ArtworkJobOutcome.Partial or ArtworkJobOutcome.NoArtworkFound or ArtworkJobOutcome.Failed or ArtworkJobOutcome.Cancelled;ContentDialogResult action=await Dialog("Artwork Job Details",new ScrollViewer{Content=panel,MaxHeight=580},retryable?"Retry":"Close",job.Outcome==ArtworkJobOutcome.NeedsReview?"Review Matches":null,retryable?"Close":string.Empty).ShowAsync();if(action==ContentDialogResult.Secondary){await ShowArtworkReviewQueueAsync();return;}if(action!=ContentDialogResult.Primary||!retryable)return;ArtworkJobPreflightResult retry=await _artworkPreflight.CheckAsync(request);if(!retry.CanStart){ViewModel.ReportStatus(string.Join(" ",retry.BlockingReasons));return;}string retried=await _artworkJobs.CreateAsync(request with{Name=$"Retry · {request.Name}"});await _artworkJobs.StartAsync(retried);_=_artworkJobProcessor.RunAsync(retried);_ = MonitorArtworkJobAsync(retried);
    }

    private async Task ShowArtworkReviewQueueAsync()
    {
        while(true)
        {
            IReadOnlyList<ArtworkReviewItem> queue=await _artworkReviews.ListAsync();
            if(queue.Count==0){ViewModel.ReportStatus("No artwork matches are waiting for review.");return;}
            ArtworkReviewItem item=queue[0];
            if(item.Candidates.Count==0){await _artworkReviews.SkipAsync(item.JobItemId);continue;}
            ComboBox candidateBox=new(){Header="Candidate",ItemsSource=item.Candidates.Select(value=>$"{value.Title} · {value.PlatformId??"Unknown platform"} · {value.ProviderId} · score {value.Score}").ToArray(),SelectedIndex=0,MinWidth=500};
            TextBlock evidence=new(){TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true};
            void UpdateEvidence(){ArtworkMatchCandidate candidate=item.Candidates[Math.Max(0,candidateBox.SelectedIndex)];evidence.Text=$"Local: {item.LocalTitle}\nPlatform: {item.PlatformId??"Unknown"}\nFile: {Path.GetFileName(item.FileName)??"Unknown"}\nSerial: {item.GameSerial??"Unknown"}\n\nMatch reasons: {string.Join("; ",candidate.Reasons??[])}\nConflicts: {(candidate.Conflicts?.Count>0?string.Join("; ",candidate.Conflicts):"None")}";}candidateBox.SelectionChanged+=(_,_)=>UpdateEvidence();UpdateEvidence();
            StackPanel panel=new(){Spacing=10};panel.Children.Add(new TextBlock{Text=$"{queue.Count} match{(queue.Count==1?string.Empty:"es")} waiting",Opacity=.75});panel.Children.Add(candidateBox);panel.Children.Add(evidence);panel.Children.Add(new TextBlock{Text="Accept and Remember confirms this external game identity and protects the chosen provider artwork from automatic replacement.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
            ContentDialogResult action=await Dialog("Review Artwork Match",panel,"Accept and Remember","Skip","Close").ShowAsync();
            if(action==ContentDialogResult.None)return;
            if(action==ContentDialogResult.Secondary){await _artworkReviews.SkipAsync(item.JobItemId);continue;}
            ArtworkMatchCandidate selected=item.Candidates[Math.Max(0,candidateBox.SelectedIndex)];
            try{await _artworkReviews.AcceptAsync(item.JobItemId,selected.ProviderId,selected.ProviderGameId,true);await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Accepted artwork match for {item.LocalTitle}.");}
            catch(Exception exception){ViewModel.ReportStatus($"The selected artwork could not be applied: {exception.Message}");return;}
        }
    }

    private async void CreateCollection_Click(object sender,RoutedEventArgs e)
    {
        try{await CreateCollectionAsync();}
        catch(Exception exception){_logger.LogError("ShelfCreateFailed","A Shelf could not be created.",exception);ViewModel.ReportStatus($"The Shelf could not be created: {exception.Message}");await _sounds.PlayAsync(UiSoundEvent.Error);}
    }
    private async Task CreateCollectionAsync()
    {
        TextBox name=new(){Header="Shelf name",MinWidth=500};TextBox description=new(){Header="Description (optional)",MinWidth=500};ComboBox type=new(){Header="Shelf type",ItemsSource=new[]{new ShelfTypeOption(CollectionType.Manual,"Shelf"),new ShelfTypeOption(CollectionType.Smart,"Dynamic Shelf"),new ShelfTypeOption(CollectionType.Folder,"Shelf Group")},DisplayMemberPath="Name",SelectedIndex=0,MinWidth=500};ComboBox parent=new(){Header="Shelf Group (optional)",ItemsSource=(await _collections.GetAsync()).Where(x=>x.CollectionType==CollectionType.Folder&&!x.Id.StartsWith("auto:",StringComparison.Ordinal)).ToArray(),DisplayMemberPath="Name",PlaceholderText="Top level",MinWidth=500};ComboBox match=new(){Header="Rules",ItemsSource=new[]{new MatchModeOption(SmartRuleMatchMode.MatchAll,"Match All"),new MatchModeOption(SmartRuleMatchMode.MatchAny,"Match Any")},DisplayMemberPath="Name",SelectedIndex=0,MinWidth=500};
        SmartRuleField[] fields=[SmartRuleField.Title,SmartRuleField.ContentType,SmartRuleField.Provider,SmartRuleField.Platform,SmartRuleField.Favorite,SmartRuleField.Hidden,SmartRuleField.Available,SmartRuleField.RecentlyPlayed,SmartRuleField.RecentlyOpened,SmartRuleField.AddedDate,SmartRuleField.LastActivityDate,SmartRuleField.Playtime,SmartRuleField.ApplicationCategory,SmartRuleField.MediaType,SmartRuleField.GallerySourceKind,SmartRuleField.Emulator,SmartRuleField.LifecycleStatus];List<DynamicRuleRow> rows=Enumerable.Range(0,5).Select(index=>new DynamicRuleRow(index,fields)).ToList();rows[0].Root.Visibility=Visibility.Visible;Button addRule=new(){Content="Add Rule"};Button removeRule=new(){Content="Remove Last Rule"};Button previewButton=new(){Content="Preview up to 100 results"};TextBlock preview=new(){Text="Preview has not been run.",TextWrapping=TextWrapping.Wrap,Opacity=.8};StackPanel rulesPanel=new(){Spacing=8};rulesPanel.Children.Add(match);foreach(DynamicRuleRow row in rows)rulesPanel.Children.Add(row.Root);StackPanel ruleButtons=new(){Orientation=Orientation.Horizontal,Spacing=8};ruleButtons.Children.Add(addRule);ruleButtons.Children.Add(removeRule);ruleButtons.Children.Add(previewButton);rulesPanel.Children.Add(ruleButtons);rulesPanel.Children.Add(preview);int visibleRules=1;addRule.Click+=(_,_)=>{if(visibleRules<rows.Count)rows[visibleRules++].Root.Visibility=Visibility.Visible;};removeRule.Click+=(_,_)=>{if(visibleRules>1)rows[--visibleRules].Root.Visibility=Visibility.Collapsed;};
        StackPanel panel=new(){Spacing=10};foreach(UIElement control in new UIElement[]{name,description,type,parent,rulesPanel})panel.Children.Add(control);void UpdateType(){rulesPanel.Visibility=type.SelectedItem is ShelfTypeOption{Type:CollectionType.Smart}?Visibility.Visible:Visibility.Collapsed;}type.SelectionChanged+=(_,_)=>UpdateType();UpdateType();
        SmartCollectionDefinition Draft(){DateTime at=DateTime.UtcNow;SmartRuleMatchMode mode=(match.SelectedItem as MatchModeOption)?.Mode??SmartRuleMatchMode.MatchAll;CollectionDefinition definition=new("preview",string.IsNullOrWhiteSpace(name.Text)?"Untitled Dynamic Shelf":name.Text.Trim(),description.Text.Trim(),CollectionType.Smart,null,null,null,0,false,mode,at,at);return new(definition,rows.Take(visibleRules).Select((row,index)=>row.Create("preview",index,at)).ToArray());}
        previewButton.Click+=async(_,_)=>{try{IReadOnlyList<CollectionResolvedItem> results=await _smartCollections.PreviewAsync(Draft(),100);preview.Text=results.Count==0?"No matches yet.":$"{results.Count}{(results.Count==100?"+":string.Empty)} match(es): {string.Join(", ",results.Take(8).Select(item=>item.DisplayName))}{(results.Count>8?"…":string.Empty)}";}catch(Exception exception){preview.Text=exception.Message;}};
        if(await Dialog("Create Shelf",new ScrollViewer{Content=panel,MaxHeight=640},"Create",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||string.IsNullOrWhiteSpace(name.Text)||type.SelectedItem is not ShelfTypeOption selected)return;DateTime now=DateTime.UtcNow;SmartRuleMatchMode selectedMatch=(match.SelectedItem as MatchModeOption)?.Mode??SmartRuleMatchMode.MatchAll;CollectionDefinition saved=await _collections.SaveAsync(new("",name.Text.Trim(),description.Text.Trim(),selected.Type,null,null,(parent.SelectedItem as CollectionDefinition)?.Id,0,false,selectedMatch,now,now));
        if(selected.Type==CollectionType.Smart){SmartCollectionRule[] rules=rows.Take(visibleRules).Select((row,index)=>row.Create(saved.Id,index,now)).ToArray();SmartCollectionValidationResult validation=await _smartCollections.ValidateAsync(new(saved,rules));if(!validation.Valid){await _collections.DeleteAsync(saved.Id);ViewModel.ReportStatus(string.Join(" ",validation.Errors));return;}await _collections.SetRulesAsync(saved.Id,rules);}
        _smartCollections.Invalidate();await CustomizationViewModel.RefreshCollectionsAsync();ViewModel.ReportStatus($"Created {saved.Name}.");
    }
    private sealed record ShelfTypeOption(CollectionType Type,string Name);
    private sealed record MatchModeOption(SmartRuleMatchMode Mode,string Name);
    private sealed class DynamicRuleRow
    {
        public DynamicRuleRow(int index,IReadOnlyList<SmartRuleField> fields){Field=new(){Header=$"Rule {index+1} field",ItemsSource=fields,SelectedItem=SmartRuleField.Title,MinWidth=185};Operator=new(){Header="Condition",ItemsSource=Enum.GetValues<SmartRuleOperator>(),SelectedItem=SmartRuleOperator.Contains,MinWidth=150};Value=new(){Header="Value",PlaceholderText="Text, date, number, status, or enum value",MinWidth=220};Root=new(){Orientation=Orientation.Horizontal,Spacing=8,Visibility=Visibility.Collapsed};Root.Children.Add(Field);Root.Children.Add(Operator);Root.Children.Add(Value);Field.SelectionChanged+=(_,_)=>Configure();Configure();}
        public StackPanel Root{get;}public ComboBox Field{get;}public ComboBox Operator{get;}public TextBox Value{get;}
        public SmartCollectionRule Create(string shelfId,int order,DateTime at){SmartRuleField field=Field.SelectedItem is SmartRuleField selected?selected:SmartRuleField.Title;SmartRuleOperator operation=Operator.SelectedItem is SmartRuleOperator op?op:SmartRuleOperator.Equals;string? value=operation is SmartRuleOperator.IsTrue or SmartRuleOperator.IsFalse?null:System.Text.Json.JsonSerializer.Serialize(Value.Text.Trim());return new(Guid.NewGuid().ToString("N"),shelfId,0,field,operation,value,order,at,at);}
        private void Configure(){SmartRuleField field=Field.SelectedItem is SmartRuleField selected?selected:SmartRuleField.Title;bool boolean=field is SmartRuleField.Favorite or SmartRuleField.Hidden or SmartRuleField.Available or SmartRuleField.RecentlyPlayed or SmartRuleField.RecentlyOpened;if(boolean){Operator.ItemsSource=new[]{SmartRuleOperator.IsTrue,SmartRuleOperator.IsFalse};Operator.SelectedIndex=0;Value.Visibility=Visibility.Collapsed;}else{Operator.ItemsSource=field is SmartRuleField.AddedDate or SmartRuleField.LastActivityDate?new[]{SmartRuleOperator.Before,SmartRuleOperator.After,SmartRuleOperator.Equals}:field==SmartRuleField.Playtime?new[]{SmartRuleOperator.Equals,SmartRuleOperator.GreaterThan,SmartRuleOperator.LessThan}:new[]{SmartRuleOperator.Equals,SmartRuleOperator.NotEquals,SmartRuleOperator.Contains,SmartRuleOperator.StartsWith,SmartRuleOperator.In,SmartRuleOperator.NotIn};Operator.SelectedIndex=0;Value.Visibility=Visibility.Visible;Value.PlaceholderText=field switch{SmartRuleField.LifecycleStatus=>"Unassigned, Backlog, Playing, Completed, or Dropped",SmartRuleField.MediaType=>"Image, AnimatedImage, or Video",SmartRuleField.GallerySourceKind=>"Photos, Screenshots, Recordings, Videos, Wallpapers, or Custom",SmartRuleField.ContentType=>"Game or Application",SmartRuleField.AddedDate or SmartRuleField.LastActivityDate=>"YYYY-MM-DD",SmartRuleField.Playtime=>"Seconds",_=>"Value"};}}
    }

    private async Task<int> EnsureAutomaticSmartLibrariesAsync()
    {
        await _automaticLibraryGate.WaitAsync();
        try
        {
            IReadOnlyList<LibraryItem> items=await _library.QueryAsync(new(IncludeHidden:true,MaximumResults:5000));
            IReadOnlyList<CollectionDefinition> existing=await _collections.GetAsync();
            Dictionary<string,CollectionDefinition> byId=existing.ToDictionary(value=>value.Id,StringComparer.OrdinalIgnoreCase);
            List<(string Id,string Name,SmartRuleField Field,string Value,string Description)> desired=[];

            foreach(string providerId in items.SelectMany(item=>item.LaunchTargets)
                .Select(target=>target.ProviderId)
                .Where(value=>!string.IsNullOrWhiteSpace(value)&&!value.Equals("emulation",StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value=>value,StringComparer.OrdinalIgnoreCase)!)
            {
                desired.Add(($"auto:provider:{providerId}",$"{ProviderDisplayName(providerId)} Library",SmartRuleField.Provider,providerId!,"Automatically includes installed items from this provider."));
            }

            Dictionary<string,EmulatorInstallation> installations=(await _emulation.GetInstallationsAsync()).ToDictionary(value=>value.Id,StringComparer.OrdinalIgnoreCase);
            foreach(string emulatorId in items.SelectMany(item=>item.LaunchTargets)
                .Select(target=>target.EmulatorInstallationId)
                .Where(value=>!string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value=>installations.TryGetValue(value!,out EmulatorInstallation? installation)?installation.DisplayName:value,StringComparer.OrdinalIgnoreCase)!)
            {
                string name=installations.TryGetValue(emulatorId!,out EmulatorInstallation? installation)?installation.DisplayName:emulatorId!;
                desired.Add(($"auto:emulator:{emulatorId}",$"{name} Library",SmartRuleField.Emulator,emulatorId!,"Automatically includes games configured to use this emulator."));
            }

            int changed=0;int nextSort=existing.Count==0?0:existing.Max(value=>value.SortOrder)+1;
            foreach((string id,string name,SmartRuleField field,string value,string description) in desired)
            {
                DateTime now=DateTime.UtcNow;
                if(!byId.TryGetValue(id,out CollectionDefinition? collection))
                {
                    collection=await _collections.SaveAsync(new(id,name,description,CollectionType.Smart,field==SmartRuleField.Emulator?"Emulator":"Provider",null,null,nextSort++,false,SmartRuleMatchMode.MatchAll,now,now));
                    byId[id]=collection;changed++;
                }
                else if(collection.CollectionType!=CollectionType.Smart)
                {
                    collection=await _collections.SaveAsync(collection with{CollectionType=CollectionType.Smart,MatchMode=SmartRuleMatchMode.MatchAll,UpdatedAtUtc=now});
                    byId[id]=collection;changed++;
                }

                string serialized=System.Text.Json.JsonSerializer.Serialize(value);
                IReadOnlyList<SmartCollectionRule> rules=await _collections.GetRulesAsync(id);
                bool ruleMatches=rules.Count==1&&rules[0].Field==field&&rules[0].Operator==SmartRuleOperator.Equals&&string.Equals(rules[0].ValueJson,serialized,StringComparison.Ordinal);
                if(!ruleMatches)
                {
                    await _collections.SetRulesAsync(id,[new($"{id}:rule",id,0,field,SmartRuleOperator.Equals,serialized,0,now,now)]);
                    changed++;
                }
            }

            if(changed>0)
            {
                _smartCollections.Invalidate();
                await CustomizationViewModel.RefreshCollectionsAsync();
            }
            return changed;
        }
        finally{_automaticLibraryGate.Release();}
    }

    private static string ProviderDisplayName(string providerId)=>providerId.ToLowerInvariant() switch
    {
        "steam"=>"Steam",
        "epic"=>"Epic Games",
        "gog"=>"GOG",
        "ubisoft"=>"Ubisoft Connect",
        "ea"=>"EA app",
        "battlenet"=>"Battle.net",
        "windows-start-menu"=>"Windows Start Menu",
        "windows-apps-folder"=>"Windows Apps",
        "windows-pwa"=>"Installed Web Apps",
        _=>CultureInfo.CurrentCulture.TextInfo.ToTitleCase(providerId.Replace('-',' '))
    };

    private async void Collection_ItemClick(object sender,ItemClickEventArgs e)
    {
        CollectionDefinition? selectedShelf=e.ClickedItem switch{CollectionCardViewModel card=>card.Definition,CollectionDefinition definition=>definition,_=>null};if(selectedShelf is null)return;_shelfFocusOrigin=(sender as GridView)?.ContainerFromItem(e.ClickedItem) as Control;await ShowShelfDetailAsync(selectedShelf,_shelfFocusOrigin);
    }
    private async Task ShowShelfDetailAsync(CollectionDefinition shelf,Control? focusOrigin=null)
    {
        _shelfFocusOrigin=focusOrigin??_shelfFocusOrigin;await CustomizationViewModel.OpenShelfAsync(shelf);ShowShelfDetail();
    }
    private void ShowShelfDetail()
    {
        CollectionsRoot.Visibility=Visibility.Collapsed;CrossbarCollectionsContent.Visibility=Visibility.Collapsed;ShelfDetailRoot.Visibility=Visibility.Visible;bool manage=CustomizationViewModel.CanManageShelf;ShelfAddItemButton.Visibility=manage?Visibility.Visible:Visibility.Collapsed;ShelfMoveEarlierButton.Visibility=manage?Visibility.Visible:Visibility.Collapsed;ShelfMoveLaterButton.Visibility=manage?Visibility.Visible:Visibility.Collapsed;ShelfRemoveButton.Visibility=manage?Visibility.Visible:Visibility.Collapsed;ShelfBackButton.Focus(FocusState.Programmatic);
    }
    private void ShelfBack_Click(object sender,RoutedEventArgs e)
    {
        ShelfDetailRoot.Visibility=Visibility.Collapsed;if(_activeLayout==InterfaceLayoutType.ClassicCrossbar)CrossbarCollectionsContent.Visibility=Visibility.Visible;else CollectionsRoot.Visibility=Visibility.Visible;DispatcherQueue.TryEnqueue(()=>_shelfFocusOrigin?.Focus(FocusState.Programmatic));
    }
    private async void LifecycleShelf_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not Button{Tag:string tag})return;string[] parts=tag.Split(':');if(parts.Length!=2||!Enum.TryParse(parts[1],out ContentLifecycleStatus status))return;bool video=parts[0]=="video";string title=sender is Button{Content:string label}?label:status.ToString();await CustomizationViewModel.OpenLifecycleAsync(video?ContentLifecycleTargetType.GalleryMediaItem:ContentLifecycleTargetType.LibraryItem,status,title,video?"Videos in this queue.":"Games with this progress status.");ShowShelfDetail();
    }
    private async void SpecialShelf_Click(object sender,RoutedEventArgs e)
    {
        bool favorites=(sender as Button)?.Tag as string=="favorites";await CustomizationViewModel.OpenSpecialShelfAsync(favorites,favorites?"Favorites":"Recently Added");ShowShelfDetail();
    }
    private void ShelfSearch_TextChanged(AutoSuggestBox sender,AutoSuggestBoxTextChangedEventArgs args){if(args.Reason==AutoSuggestionBoxTextChangeReason.UserInput)CustomizationViewModel.FilterShelf(sender.Text);}
    private async void ShelfItem_ItemClick(object sender,ItemClickEventArgs e)
    {
        if(e.ClickedItem is not ShelfItemViewModel item||!item.Available)return;if(item.Item.LibraryItem is { } libraryItem)await ViewModel.LaunchAsync(new CardViewModel(libraryItem,libraryItem.ContentType==ContentType.Game?"\uE7FC":"\uECAA"));else if(item.Item.GalleryMediaItem is { } media)await OpenGalleryCardAsync(new GalleryCardViewModel(media,_galleryThumbnails));else if(item.Item.GalleryAlbum is { } album){await CustomizationViewModel.OpenAlbumAsync(album);ShowShelfDetail();}else if(item.Item.NestedShelf is { } nested)await ShowShelfDetailAsync(nested);
    }
    private async void ShelfRemove_Click(object sender,RoutedEventArgs e){if(ShelfItemsGrid.SelectedItem is ShelfItemViewModel item){await CustomizationViewModel.RemoveShelfItemAsync(item);ViewModel.ReportStatus($"Removed {item.DisplayName} from the Shelf. The source item was not changed.");}}
    private async void ShelfMoveEarlier_Click(object sender,RoutedEventArgs e){if(ShelfItemsGrid.SelectedItem is ShelfItemViewModel item)await CustomizationViewModel.MoveShelfItemAsync(item,-1);}
    private async void ShelfMoveLater_Click(object sender,RoutedEventArgs e){if(ShelfItemsGrid.SelectedItem is ShelfItemViewModel item)await CustomizationViewModel.MoveShelfItemAsync(item,1);}
    private async void ShelfAddItem_Click(object sender,RoutedEventArgs e)
    {
        CollectionDefinition? shelf=CustomizationViewModel.OpenShelf;if(shelf?.CollectionType!=CollectionType.Manual)return;List<ShelfAddCandidate> all=[];all.AddRange((await _library.QueryAsync(new(IncludeHidden:true,MaximumResults:5000))).Select(item=>new ShelfAddCandidate(CollectionTargetType.LibraryItem,item.Id,item.Title,item.ContentType==ContentType.Game?"Game":"App")));all.AddRange((await _galleryPersistence.QueryAsync(new(IncludeHidden:true,IncludeUnavailable:true,MaximumResults:5000))).Select(item=>new ShelfAddCandidate(CollectionTargetType.GalleryMediaItem,item.Id,item.FileName,item.MediaType==GalleryMediaType.Video?"Video":"Photo")));all.AddRange((await _galleryPersistence.GetAlbumsAsync()).Select(item=>new ShelfAddCandidate(CollectionTargetType.GalleryAlbum,item.Id,item.Name,"Album")));all.AddRange((await _collections.GetAsync()).Where(item=>item.Id!=shelf.Id&&!item.Id.StartsWith("auto:",StringComparison.Ordinal)).Select(item=>new ShelfAddCandidate(CollectionTargetType.Collection,item.Id,item.Name,"Shelf")));
        AutoSuggestBox search=new(){PlaceholderText="Search games, apps, Gallery, albums, and Shelves",MinWidth=480,QueryIcon=new SymbolIcon(Symbol.Find)};ListView choices=new(){ItemsSource=all,DisplayMemberPath="Label",SelectionMode=ListViewSelectionMode.Single,MaxHeight=420};search.TextChanged+=(_,args)=>{if(args.Reason==AutoSuggestionBoxTextChangeReason.UserInput)choices.ItemsSource=all.Where(item=>item.Label.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).ToArray();};StackPanel panel=new(){Spacing=10};panel.Children.Add(search);panel.Children.Add(choices);if(await Dialog("Add Item to Shelf",panel,"Add",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&choices.SelectedItem is ShelfAddCandidate selected){await _collections.AddItemAsync(new(shelf.Id,selected.TargetType,selected.TargetId,0,DateTime.UtcNow));await CustomizationViewModel.OpenShelfAsync(shelf);await CustomizationViewModel.RefreshCollectionsAsync();ViewModel.ReportStatus($"Added {selected.Name} to {shelf.Name}.");}
    }
    private sealed record ShelfAddCandidate(CollectionTargetType TargetType,string TargetId,string Name,string Type){public string Label=>$"{Name} · {Type}";}

    private async Task AddCollectionRowAsync(CollectionDefinition collection)
    {
        DashboardConfiguration dashboard=await _dashboardConfiguration.GetOrCreateAsync(_layouts.CurrentLayoutId);List<DashboardRowConfiguration> rows=(await _dashboardConfiguration.GetRowsAsync(dashboard.Id)).ToList();DateTime now=DateTime.UtcNow;rows.Add(new(Guid.NewGuid().ToString("N"),dashboard.Id,$"collection-{collection.Id}",collection.Name,collection.CollectionType==CollectionType.Smart?DashboardRowType.SmartCollection:DashboardRowType.ManualCollection,collection.Id,rows.Count,true,18,CardStyle.Wide,CardSize.Medium,true,true,true,true,now,now));await _dashboardConfiguration.SaveRowsAsync(dashboard.Id,rows);await CustomizationViewModel.RefreshPresentationAsync();await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Added {collection.Name} to Home.");
    }
    private async void AppearanceSettings_Click(object sender,RoutedEventArgs e)=>await ShowAppearanceSettingsAsync();
    private async Task ShowAppearanceSettingsAsync()
    {
        InterfaceLayoutDefinition[] layouts=CustomizationViewModel.Layouts.ToArray();ThemeDefinition[] themes=CustomizationViewModel.Themes.ToArray();UiSoundPack[] packs=(await _sounds.GetPacksAsync()).ToArray();PerformanceSettings currentPerformance=await _performance.GetAsync();ThemeDefinition initialTheme=themes.FirstOrDefault(x=>x.Id==CustomizationViewModel.CurrentThemeId)??themes[0];
        ComboBox layoutBox=new(){Header="Interface layout",ItemsSource=layouts,DisplayMemberPath="DisplayName",SelectedItem=layouts.FirstOrDefault(x=>x.Id==CustomizationViewModel.CurrentLayoutId),MinWidth=420};ComboBox themeBox=new(){Header="Theme",ItemsSource=themes,DisplayMemberPath="Name",SelectedItem=initialTheme,MinWidth=420};TextBox accent=new(){Header="Primary accent (#RRGGBB)",Text=initialTheme.AccentColor,MinWidth=420};TextBox secondaryAccent=new(){Header="Secondary accent (#RRGGBB)",Text=initialTheme.SecondaryAccentColor??initialTheme.AccentColor,MinWidth=420};TextBlock contrastStatus=new(){TextWrapping=TextWrapping.Wrap,Opacity=.85};Button restoreColors=new(){Content="Restore Accessible Colors"};ComboBox backgroundChoice=new(){Header="Background",ItemsSource=new[]{"Theme default","Solid color","Gradient","User-selected image","Approved Gallery image","Selected-item artwork","No background"},SelectedIndex=0,MinWidth=420};ComboBox stretch=new(){Header="Image fit",ItemsSource=Enum.GetValues<BackgroundStretch>(),SelectedItem=BackgroundStretch.Fill,MinWidth=420};NumberBox blur=new(){Header="Blur amount",Minimum=0,Maximum=50,Value=0,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};Slider overlay=new(){Header="Darkening overlay",Minimum=0,Maximum=100,Value=35};Slider opacity=new(){Header="Background opacity",Minimum=0,Maximum=100,Value=100};Button chooseImage=new(){Content="Choose Local Image"};TextBlock imageStatus=new(){Text="No new image selected.",Opacity=.75,TextWrapping=TextWrapping.Wrap};string? selectedImage=null;chooseImage.Click+=async(_,_)=>{selectedImage=await PickArtworkAsync();imageStatus.Text=selectedImage is null?"No new image selected.":$"Managed copy will be created from {Path.GetFileName(selectedImage)}.";};
        ComboBox presetBox=new(){Header="Visual performance",ItemsSource=Enum.GetValues<VisualPerformancePreset>().Where(x=>x!=VisualPerformancePreset.Custom).ToArray(),SelectedItem=currentPerformance.Preset==VisualPerformancePreset.Custom?VisualPerformancePreset.Balanced:currentPerformance.Preset,MinWidth=420};CheckBox reducedMotion=new(){Content="Reduced motion",IsChecked=currentPerformance.ReducedMotion};ComboBox soundBox=new(){Header="UI sound pack",ItemsSource=packs,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=await _settings.GetAsync("Interface.SoundPackId","nexa-soft"),MinWidth=420};Slider soundVolume=new(){Header="UI sound volume",Minimum=0,Maximum=100,Value=await _settings.GetAsync("Interface.SoundVolume",.6)*100,MinWidth=420};TextBlock preview=new(){TextWrapping=TextWrapping.Wrap,Opacity=.8};
        string ContrastBackground(){ThemeDefinition selected=themeBox.SelectedItem as ThemeDefinition??initialTheme;if(backgroundChoice.SelectedItem as string=="Solid color")return accent.Text;return selected.ThemeType==ThemeType.Light?"#F4F6FA":selected.ThemeType==ThemeType.HighContrast?"#000000":selected.Background.Value?.Split(',')[0] is { } value&&value.StartsWith('#')?value:"#090B13";}void UpdateContrast(){double ratio=ThemeService.ContrastRatio(accent.Text,ContrastBackground());contrastStatus.Text=ratio>=4.5?$"Accent contrast: {ratio:F1}:1. Readable foreground text will be selected automatically.":$"Warning: accent contrast is only {ratio:F1}:1 against the selected surface. You may override this warning, or restore accessible colors.";}restoreColors.Click+=(_,_)=>{ThemeDefinition selected=themeBox.SelectedItem as ThemeDefinition??initialTheme;accent.Text=selected.AccentColor;secondaryAccent.Text=selected.SecondaryAccentColor??selected.AccentColor;UpdateContrast();};accent.TextChanged+=(_,_)=>UpdateContrast();secondaryAccent.TextChanged+=(_,_)=>UpdateContrast();backgroundChoice.SelectionChanged+=(_,_)=>UpdateContrast();
        void UpdatePreview(){if(layoutBox.SelectedItem is InterfaceLayoutDefinition selected)preview.Text=$"Preview: {selected.DisplayName}\n{selected.Description}\nNavigation: {(selected.LayoutType==InterfaceLayoutType.ClassicCrossbar?"category-first":selected.LayoutType==InterfaceLayoutType.CompactHandheld?"tabs and compact grid":"dashboard rows")}. No library query is run for this preview.";}layoutBox.SelectionChanged+=(_,_)=>UpdatePreview();themeBox.SelectionChanged+=async(_,_)=>{if(themeBox.SelectedItem is ThemeDefinition selected){accent.Text=selected.AccentColor;secondaryAccent.Text=selected.SecondaryAccentColor??selected.AccentColor;UpdateContrast();await CustomizationViewModel.PreviewThemeAsync(selected.Id);}};UpdatePreview();UpdateContrast();
        StackPanel panel=new(){Spacing=9};foreach(UIElement control in new UIElement[]{layoutBox,preview,themeBox,accent,secondaryAccent,contrastStatus,restoreColors,backgroundChoice,chooseImage,imageStatus,stretch,blur,overlay,opacity,presetBox,reducedMotion,soundBox,soundVolume})panel.Children.Add(control);
        ContentDialogResult result=await Dialog("Appearance and Interface",new ScrollViewer{Content=panel,MaxHeight=620},"Apply","Customize Dashboard","Cancel").ShowAsync();if(result==ContentDialogResult.Secondary){await CustomizationViewModel.CancelThemePreviewAsync();await ShowDashboardSettingsAsync();return;}if(result!=ContentDialogResult.Primary){await CustomizationViewModel.CancelThemePreviewAsync();return;}
        string? preferred=(FocusManager.GetFocusedElement(Root.XamlRoot) as FrameworkElement)?.DataContext is CardViewModel focused?focused.Id:null;await _semanticFocus.SaveAsync(new(ViewModel.SelectedSection.Section,ViewModel.Rows.Count>0?ViewModel.Rows[0].RowKey:null,CollectionTargetType.LibraryItem,preferred,null,_layouts.CurrentLayoutId,0));
        if(layoutBox.SelectedItem is InterfaceLayoutDefinition selectedLayout)await CustomizationViewModel.ApplyLayoutAsync(selectedLayout.Id);ThemeDefinition selectedTheme=themeBox.SelectedItem as ThemeDefinition??initialTheme;bool customColors=!string.Equals(accent.Text,selectedTheme.AccentColor,StringComparison.OrdinalIgnoreCase)||!string.Equals(secondaryAccent.Text,selectedTheme.SecondaryAccentColor??selectedTheme.AccentColor,StringComparison.OrdinalIgnoreCase);BackgroundDefinition background=selectedTheme.Background;string choice=backgroundChoice.SelectedItem as string??"Theme default";
        try
        {
            if(choice=="Solid color")background=new(BackgroundType.Solid,accent.Text);else if(choice=="Gradient")background=new(BackgroundType.Gradient,$"{accent.Text},{secondaryAccent.Text}");else if(choice=="No background")background=new(BackgroundType.None);else if(choice is "User-selected image" or "Selected-item artwork"){string? source=choice=="User-selected image"?selectedImage:(FocusManager.GetFocusedElement(Root.XamlRoot) as FrameworkElement)?.DataContext is CardViewModel selectedCard?selectedCard.Item.BackgroundAsset?.LocalPath??selectedCard.Item.CoverAsset?.LocalPath:null;if(source is null)throw new InvalidOperationException("Choose an image or focus a card with local artwork first.");background=await _themeAssets.ImportImageAsync(source,stretch.SelectedItem is BackgroundStretch selectedStretch?selectedStretch:BackgroundStretch.Fill,(int)blur.Value,overlay.Value/100d,opacity.Value/100d);}else if(choice=="Approved Gallery image"){GalleryBackgroundSelection gallery=await _galleryPersistence.GetBackgroundAsync()??throw new InvalidOperationException("Set a non-private Gallery image as the Gallery background first.");GalleryMediaItem item=await _galleryPersistence.GetMediaItemAsync(gallery.MediaItemId)??throw new InvalidOperationException("The approved Gallery background is unavailable.");if(item.SourcePrivacyLevel==GalleryPrivacyLevel.Private)throw new InvalidOperationException("Private Gallery sources cannot supply interface backgrounds.");background=new(BackgroundType.Image,gallery.ManagedPath,stretch.SelectedItem is BackgroundStretch galleryStretch?galleryStretch:BackgroundStretch.Fill,(int)blur.Value,overlay.Value/100d,opacity.Value/100d,true);}
            bool customBackground=choice!="Theme default";if(customColors||customBackground){ThemeDefinition custom=selectedTheme with{Id="custom-local",Name="Custom Local",Description="User-approved local colors and background.",ThemeType=ThemeType.Custom,AccentColor=accent.Text.Trim(),SecondaryAccentColor=secondaryAccent.Text.Trim(),Background=background,BuiltIn=false,AuthorDisplayName="Local user"};ThemeDefinition saved=await _themes.SaveCustomAsync(custom);await CustomizationViewModel.ApplyThemeAsync(saved.Id);await CustomizationViewModel.InitializeAsync();}else await CustomizationViewModel.ApplyThemeAsync(selectedTheme.Id);
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or IOException){await CustomizationViewModel.CancelThemePreviewAsync();ViewModel.ReportStatus($"Appearance was not applied: {ex.Message}");await _sounds.PlayAsync(UiSoundEvent.Error);return;}
        if(presetBox.SelectedItem is VisualPerformancePreset selectedPreset)await _performance.ApplyAsync(selectedPreset,reducedMotion.IsChecked==true);if(soundBox.SelectedItem is UiSoundPack selectedPack)await _sounds.ApplyPackAsync(selectedPack.Id);await _sounds.SetVolumeAsync(soundVolume.Value/100d);await CustomizationViewModel.RefreshPresentationAsync();await ViewModel.RefreshAsync();FocusRestoreResult restore=await _semanticFocus.RestoreAsync(new(ViewModel.SelectedSection.Section,_layouts.CurrentLayoutId,ViewModel.Rows.SelectMany(x=>x.Items).Select(x=>x.Id).ToArray(),preferred));if(restore.TargetId is not null)DispatcherQueue.TryEnqueue(()=>FocusCard(restore.TargetId));await _sounds.PlayAsync(UiSoundEvent.Confirm);ViewModel.ReportStatus("Interface settings applied.");
    }
    private async Task ShowDashboardSettingsAsync()
    {
        DashboardConfiguration dashboard=await _dashboardConfiguration.GetOrCreateAsync(_layouts.CurrentLayoutId);List<DashboardRowConfiguration> rows=(await _dashboardConfiguration.GetRowsAsync(dashboard.Id)).OrderBy(x=>x.SortOrder).ToList();ComboBox rowBox=new(){Header="Dashboard row",ItemsSource=rows,DisplayMemberPath="DisplayName",SelectedIndex=rows.Count>0?0:-1,MinWidth=420};CheckBox enabled=new(){Content="Show this row"};NumberBox maximum=new(){Header="Maximum items",Minimum=1,Maximum=100,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};ComboBox style=new(){Header="Card style",ItemsSource=Enum.GetValues<CardStyle>()};ComboBox size=new(){Header="Card size",ItemsSource=Enum.GetValues<CardSize>()};CheckBox title=new(){Content="Show row title"};Button up=new(){Content="Move Up"};Button down=new(){Content="Move Down"};StackPanel move=new(){Orientation=Orientation.Horizontal,Spacing=8};move.Children.Add(up);move.Children.Add(down);StackPanel panel=new(){Spacing=8};foreach(UIElement control in new UIElement[]{rowBox,enabled,maximum,style,size,title,move})panel.Children.Add(control);int current=Math.Max(0,rowBox.SelectedIndex);bool loading=false;void SaveControls(){if(loading||rows.Count==0)return;DashboardRowConfiguration row=rows[current];rows[current]=row with{Enabled=enabled.IsChecked==true,MaximumItems=(int)maximum.Value,CardStyle=style.SelectedItem is CardStyle cs?cs:row.CardStyle,CardSize=size.SelectedItem is CardSize cz?cz:row.CardSize,ShowTitle=title.IsChecked==true};}void LoadControls(){if(rows.Count==0)return;loading=true;DashboardRowConfiguration row=rows[current];enabled.IsChecked=row.Enabled;maximum.Value=row.MaximumItems;style.SelectedItem=row.CardStyle;size.SelectedItem=row.CardSize;title.IsChecked=row.ShowTitle;loading=false;}rowBox.SelectionChanged+=(_,_)=>{SaveControls();current=Math.Max(0,rowBox.SelectedIndex);LoadControls();};void Move(int offset){SaveControls();int target=Math.Clamp(current+offset,0,rows.Count-1);if(target==current)return;DashboardRowConfiguration row=rows[current];rows.RemoveAt(current);rows.Insert(target,row);current=target;rowBox.ItemsSource=null;rowBox.ItemsSource=rows;rowBox.SelectedIndex=current;LoadControls();}up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);LoadControls();ContentDialogResult result=await Dialog("Customize Dashboard",new ScrollViewer{Content=panel,MaxHeight=600},"Save","Reset Layout","Cancel").ShowAsync();if(result==ContentDialogResult.Secondary){if(await Dialog("Reset this dashboard?",new TextBlock{Text="The selected layout's rows will return to safe defaults. Your library and Shelves are not changed.",TextWrapping=TextWrapping.Wrap},"Reset",null,"Cancel").ShowAsync()==ContentDialogResult.Primary)await _dashboardConfiguration.ResetAsync(_layouts.CurrentLayoutId);}else if(result==ContentDialogResult.Primary){SaveControls();await _dashboardConfiguration.SaveRowsAsync(dashboard.Id,rows);}else return;await CustomizationViewModel.RefreshPresentationAsync();await ViewModel.RefreshAsync();ViewModel.ReportStatus("Dashboard configuration updated.");
    }
    private async void InterfaceTransfer_Click(object sender,RoutedEventArgs e)
    {
        string exported=await _interfaceConfiguration.ExportAsync();TextBox json=new(){Header="Versioned interface configuration JSON",Text=exported,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,MinWidth=620,MinHeight=420};ContentDialogResult result=await Dialog("Import / Export Interface",json,"Preview and Import",null,"Close").ShowAsync();if(result!=ContentDialogResult.Primary)return;InterfaceConfigurationPreview preview=await _interfaceConfiguration.PreviewImportAsync(json.Text);string summary=preview.Valid?$"Dashboards: {preview.DashboardCount}\nRows: {preview.RowCount}\nPins: {preview.PinCount}\nShelves: {preview.CollectionCount}\n{string.Join(Environment.NewLine,preview.Warnings)}":"Invalid configuration:\n"+string.Join(Environment.NewLine,preview.Warnings);if(!preview.Valid||await Dialog("Import Preview",new TextBlock{Text=summary,TextWrapping=TextWrapping.Wrap},"Import with Backup",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;InterfaceConfigurationImportResult imported=await _interfaceConfiguration.ImportAsync(json.Text);if(imported.Succeeded){await CustomizationViewModel.InitializeAsync();await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Interface imported. Backup: {imported.BackupId}");}else ViewModel.ReportStatus(string.Join(" ",imported.Errors));
    }

    private async Task ShowSteamImportAsync()
    {
        ILibraryProvider? provider=_providers.GetById("steam");
        if(provider is null){ViewModel.ReportStatus("The Steam provider is not registered.");return;}
        string? configured=await _settings.GetAsync<string?>("Providers.Steam.InstallationPath",null);
        ProviderAvailabilityResult availability=await provider.DetectAsync();
        string? root=configured??availability.InstallationPath;
        if(!availability.Detected&&string.IsNullOrWhiteSpace(root))
        {
            StackPanel missing=new(){Spacing=10};missing.Children.Add(new TextBlock{Text="Steam was not detected automatically.",TextWrapping=TextWrapping.Wrap});
            missing.Children.Add(new TextBlock{Text=string.Join(Environment.NewLine,availability.DiagnosticDetails),TextWrapping=TextWrapping.Wrap,Opacity=0.7});
            ContentDialogResult action=await Dialog("Steam provider",missing,"Choose Steam Folder","Retry Detection","Cancel").ShowAsync();
            if(action==ContentDialogResult.Secondary){availability=await provider.DetectAsync();root=availability.InstallationPath;if(root is null){ViewModel.ReportStatus("Steam was not detected automatically.");return;}}
            else if(action==ContentDialogResult.Primary){root=await PickFolderAsync();if(root is null)return;await _settings.SetAsync("Providers.Steam.InstallationPath",root);}
            else return;
        }

        ProviderScanResult scan=await provider.ScanAsync(new ProviderScanRequest(FullRefresh:true,ConfiguredInstallationPath:root));
        if(scan.WasCancelled)return;
        if(scan.Errors.Count>0&&scan.DiscoveredItems.Count==0){await Dialog("Steam scan failed",new TextBlock{Text=string.Join(Environment.NewLine,scan.Errors),TextWrapping=TextWrapping.Wrap},"Retry",null,"Close").ShowAsync();return;}
        ImportPreview preview=await _providerImports.CreatePreviewAsync("steam",scan);
        StackPanel panel=new(){Spacing=10};
        panel.Children.Add(new TextBlock{Text=$"{preview.Candidates.Count} applications found in {scan.LibrarySources.Count} libraries. Select the items to import.",TextWrapping=TextWrapping.Wrap});
        StackPanel controls=new(){Orientation=Orientation.Horizontal,Spacing=8};Button selectAll=new(){Content="Select all new"};Button clear=new(){Content="Clear"};controls.Children.Add(selectAll);controls.Children.Add(clear);panel.Children.Add(controls);
        ListView list=new(){Height=460,SelectionMode=ListViewSelectionMode.None};List<CheckBox> boxes=[];
        foreach(ImportCandidate candidate in preview.Candidates){CheckBox box=new(){Content=$"{candidate.Item.Title}  ·  {candidate.Status}{(candidate.Item.Installed?string.Empty:" · unavailable")}",IsChecked=candidate.SelectedByDefault,Tag=candidate,Margin=new Thickness(4)};boxes.Add(box);list.Items.Add(box);}
        selectAll.Click+=(_,_)=>{foreach(CheckBox box in boxes)if(box.Tag is ImportCandidate{Status:ImportCandidateStatus.New})box.IsChecked=true;};clear.Click+=(_,_)=>{foreach(CheckBox box in boxes)box.IsChecked=false;};panel.Children.Add(list);
        if(preview.Warnings.Count>0)panel.Children.Add(new TextBlock{Text=$"Warnings: {string.Join(" · ",preview.Warnings.Take(3))}",TextWrapping=TextWrapping.Wrap,Opacity=0.7});
        ContentDialogResult result=await Dialog("Import Steam Games",panel,"Import Selected",null,"Cancel").ShowAsync();if(result!=ContentDialogResult.Primary)return;
        ImportSelection[] selections=boxes.Where(box=>box.IsChecked==true).Select(box=>(ImportCandidate)box.Tag).Select(candidate=>new ImportSelection(candidate.Item,candidate.Status==ImportCandidateStatus.PossibleDuplicate&&candidate.ExistingLibraryItemId is not null?ImportSelectionAction.Link:ImportSelectionAction.Import,candidate.ExistingLibraryItemId)).ToArray();
        ProviderImportResult imported=await _providerImports.ImportAsync("steam",selections);await ViewModel.RefreshAsync();await EnsureAutomaticSmartLibrariesAsync();
        ViewModel.ReportStatus($"Steam import complete: {imported.Summary.Added} added, {imported.Summary.Updated} linked, {imported.Summary.Skipped} skipped, {imported.Summary.Failed} failed.");
    }

    private async Task ShowLauncherImportAsync()
    {
        string[] launcherIds=["epic","gog","ubisoft","ea","battlenet"];
        List<(ILibraryProvider Provider,ProviderScanResult Scan,ImportPreview Preview)> results=[];
        foreach(string id in launcherIds)
        {
            ILibraryProvider? provider=_providers.GetById(id);if(provider is null)continue;
            ProviderScanResult scan=await provider.ScanAsync(new());ImportPreview preview=await _providerImports.CreatePreviewAsync(id,scan);results.Add((provider,scan,preview));
        }
        StackPanel panel=new(){Spacing=9};List<CheckBox> choices=[];
        panel.Children.Add(new TextBlock{Text="NexaStation reads documented/local installation metadata and the standard uninstall registry. Launcher files and credentials are never modified or accessed.",TextWrapping=TextWrapping.Wrap,Opacity=.8});
        foreach((ILibraryProvider provider,ProviderScanResult scan,ImportPreview preview) in results)
        {
            panel.Children.Add(new TextBlock{Text=$"{provider.DisplayName} · {scan.DiscoveredItems.Count} detected · {scan.Warnings.Count} warnings",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
            foreach(ImportCandidate candidate in preview.Candidates)
            {
                CheckBox choice=new(){Content=$"{candidate.Item.Title} · {candidate.Status}",IsChecked=candidate.SelectedByDefault,Tag=(provider.Id,candidate),Margin=new Thickness(4,0,4,0)};choices.Add(choice);panel.Children.Add(choice);
            }
        }
        if(choices.Count==0){panel.Children.Add(new TextBlock{Text="No supported non-Steam launcher games were detected on this PC.",TextWrapping=TextWrapping.Wrap});await Dialog("Other Game Launchers",panel,"Close",null,string.Empty).ShowAsync();return;}
        if(await Dialog("Import Other Game Launchers",new ScrollViewer{Content=panel,MaxHeight=620},"Import Selected",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        int added=0,updated=0,skipped=0,failed=0;
        foreach(IGrouping<string,CheckBox> group in choices.Where(value=>value.IsChecked==true).GroupBy(value=>((ValueTuple<string,ImportCandidate>)value.Tag).Item1,StringComparer.OrdinalIgnoreCase))
        {
            ImportSelection[] selections=group.Select(value=>((ValueTuple<string,ImportCandidate>)value.Tag).Item2).Select(candidate=>new ImportSelection(candidate.Item,candidate.Status==ImportCandidateStatus.PossibleDuplicate&&candidate.ExistingLibraryItemId is not null?ImportSelectionAction.Link:ImportSelectionAction.Import,candidate.ExistingLibraryItemId)).ToArray();
            ProviderImportResult imported=await _providerImports.ImportAsync(group.Key,selections);added+=imported.Summary.Added;updated+=imported.Summary.Updated;skipped+=imported.Summary.Skipped;failed+=imported.Summary.Failed;
        }
        await ViewModel.RefreshAsync();await EnsureAutomaticSmartLibrariesAsync();ViewModel.ReportStatus($"Launcher import complete: {added} added, {updated} linked or updated, {skipped} skipped, {failed} failed.");
    }

    private async Task ShowSteamSettingsAsync()
    {
        ILibraryProvider? provider=_providers.GetById("steam");if(provider is null)return;
        ProviderAvailabilityResult availability=await provider.DetectAsync();string? configured=await _settings.GetAsync<string?>("Providers.Steam.InstallationPath",null);string? root=configured??availability.InstallationPath;
        if(root is null){ContentDialogResult missing=await Dialog("Steam provider",new TextBlock{Text="Steam was not detected automatically. Choose the folder containing steam.exe and steamapps.",TextWrapping=TextWrapping.Wrap},"Choose Steam Folder","Retry Detection","Cancel").ShowAsync();if(missing==ContentDialogResult.Primary){string? selected=await PickFolderAsync();if(selected is not null){await _settings.SetAsync("Providers.Steam.InstallationPath",selected);await ShowSteamSettingsAsync();}}else if(missing==ContentDialogResult.Secondary)await ShowSteamSettingsAsync();return;}
        ProviderScanResult diagnostics=await provider.ScanAsync(new ProviderScanRequest(ConfiguredInstallationPath:root,EnabledLibrarySourceIds:Array.Empty<string>()));
        string[] saved=await _settings.GetAsync("Providers.Steam.EnabledLibrarySourceIds",Array.Empty<string>());HashSet<string>? enabled=saved.Length==0?null:saved.ToHashSet(StringComparer.OrdinalIgnoreCase);
        StackPanel panel=new(){Spacing=10};panel.Children.Add(new TextBlock{Text=$"Detected installation\n{diagnostics.InstallationPath??root}",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text=$"Diagnostics: {diagnostics.LibrarySources.Count} library sources · {diagnostics.Warnings.Count} warnings · {diagnostics.Errors.Count} errors",Opacity=0.75});
        List<CheckBox> sourceBoxes=[];foreach(ProviderLibrarySource source in diagnostics.LibrarySources){CheckBox box=new(){Content=$"{source.Path} · {(source.Available?"Available":"Unavailable")}",IsChecked=enabled is null||enabled.Contains(source.Id),Tag=source.Id};sourceBoxes.Add(box);panel.Children.Add(box);}
        CheckBox startup=new(){Content="Refresh imported Steam items when NexaStation starts",IsChecked=await _settings.GetAsync("Providers.Steam.RefreshOnStartup",false)};panel.Children.Add(startup);
        if(diagnostics.Warnings.Count>0||diagnostics.Errors.Count>0)panel.Children.Add(new TextBlock{Text=string.Join(Environment.NewLine,diagnostics.Warnings.Concat(diagnostics.Errors)),TextWrapping=TextWrapping.Wrap,Opacity=0.7});
        ContentDialogResult result=await Dialog("Settings · Providers · Steam",panel,"Save","Choose Steam Folder","Close").ShowAsync();
        if(result==ContentDialogResult.Primary){string[] selected=sourceBoxes.Where(box=>box.IsChecked==true).Select(box=>(string)box.Tag).ToArray();await _settings.SetAsync("Providers.Steam.EnabledLibrarySourceIds",selected.Length==sourceBoxes.Count?Array.Empty<string>():selected.Length==0?["__none__"]:selected);await _settings.SetAsync("Providers.Steam.RefreshOnStartup",startup.IsChecked==true);ViewModel.ReportStatus("Steam provider settings saved.");}
        else if(result==ContentDialogResult.Secondary){string? selected=await PickFolderAsync();if(selected is not null){await _settings.SetAsync("Providers.Steam.InstallationPath",selected);await ShowSteamSettingsAsync();}}
    }

    private async Task<string?> PickFolderAsync(){FolderPicker picker=new();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return (await picker.PickSingleFolderAsync())?.Path;}

    private async Task ShowEmulationSettingsAsync()
    {
        IReadOnlyList<EmulatorInstallation> installations=await _emulation.GetInstallationsAsync();IReadOnlyList<GameSource> sources=await _emulation.GetGameSourcesAsync();ContentDialog? settingsDialog=null;
        StackPanel panel=new(){Spacing=10};panel.Children.Add(new TextBlock{Text="NexaStation indexes only folders you explicitly configure. It never downloads or modifies emulators, games, BIOS, firmware, keys, saves, or configuration files.",TextWrapping=TextWrapping.Wrap});Button discover=new(){Content="Find Installed Emulators",HorizontalAlignment=HorizontalAlignment.Left};discover.Click+=async(_,_)=>{settingsDialog?.Hide();await ShowEmulatorDiscoveryAsync();};panel.Children.Add(discover);Button discoveryFolders=new(){Content="Discovery Folders",HorizontalAlignment=HorizontalAlignment.Left};discoveryFolders.Click+=async(_,_)=>{string? folder=await PickFolderAsync();if(folder is null)return;IReadOnlyList<string> approved=await _settings.GetAsync("Emulators.DiscoveryFolders",Array.Empty<string>());await _settings.SetAsync("Emulators.DiscoveryFolders",approved.Append(folder).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());ViewModel.ReportStatus("Approved emulator discovery folder added. No drive-wide scan was enabled.");};panel.Children.Add(discoveryFolders);
        panel.Children.Add(new TextBlock{Text="Emulators",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        if(installations.Count==0)panel.Children.Add(new TextBlock{Text="No emulator installations configured.",Opacity=0.7});
        foreach(EmulatorInstallation installation in installations)
        {
            StackPanel row=new(){Orientation=Orientation.Horizontal,Spacing=8};CheckBox enabled=new(){Content=$"{installation.DisplayName} · {installation.Version??"version unknown"} · {installation.ValidationStatus}",IsChecked=installation.Enabled,Tag=installation};enabled.Click+=async(_,_)=>await _emulation.SetInstallationEnabledAsync(installation.Id,enabled.IsChecked==true);Button relink=new(){Content="Relink",Tag=installation};relink.Click+=async(_,_)=>{string? path=await PickEmulatorExecutableAsync();if(path is null)return;IEmulatorAdapter? adapter=_emulatorAdapters.GetById(installation.AdapterId);if(adapter is null)return;EmulatorProbeResult probe=await adapter.ProbeAsync(path);await _emulation.RelinkInstallationAsync(installation.Id,path,probe);ViewModel.ReportStatus($"Relinked {installation.DisplayName} without changing dependent games.");};row.Children.Add(enabled);row.Children.Add(relink);if(string.Equals(installation.AdapterId,"retroarch",StringComparison.OrdinalIgnoreCase)){Button cores=new(){Content="Cores"};cores.Click+=async(_,_)=>{settingsDialog?.Hide();await ConfigureRetroArchCoreAsync(installation);};row.Children.Add(cores);}panel.Children.Add(row);
        }
        panel.Children.Add(new TextBlock{Text="Game folders",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        foreach(GameSource source in sources)panel.Children.Add(new TextBlock{Text=$"{source.Name} · {source.PlatformId} · {(Directory.Exists(source.FolderPath)?"Available":"Missing")}",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text=$"Diagnostics · {installations.Count(i=>!File.Exists(i.ExecutablePath))} missing emulator(s) · {sources.Count(s=>!Directory.Exists(s.FolderPath))} missing folder(s)",Opacity=0.7});
        settingsDialog=Dialog("Settings · Emulation",panel,"Add Emulator","Add Game Folder","Close");ContentDialogResult result=await settingsDialog.ShowAsync();if(result==ContentDialogResult.Primary)await AddEmulatorAsync();else if(result==ContentDialogResult.Secondary)await AddGameFolderAsync();
    }

    private async Task ShowEmulatorDiscoveryAsync()
    {
        string[] approved=await _settings.GetAsync("Emulators.DiscoveryFolders",Array.Empty<string>());ViewModel.ReportStatus("Checking bounded emulator locations…");EmulatorDiscoveryResult result=await _emulatorDiscovery.DiscoverAsync(new(){DetectionSources=EmulatorDetectionSources.ExistingRecords|EmulatorDetectionSources.Registry|EmulatorDetectionSources.ApprovedPortableFolders,ApprovedFolders=approved,IncludePortableApplications=approved.Length>0,RefreshMode=EmulatorDiscoveryRefreshMode.Refresh});
        StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text=$"{result.Candidates.Count} candidate(s) found in {result.Duration.TotalSeconds:F1}s. {result.FilesInspected} bounded candidate paths were inspected. Full drives, network locations, and unrelated personal folders were not scanned.",TextWrapping=TextWrapping.Wrap});ListView list=new(){Height=420,SelectionMode=ListViewSelectionMode.None};List<(CheckBox Box,EmulatorCandidate Candidate)> rows=[];
        foreach(EmulatorCandidate candidate in result.Candidates){StackPanel row=new(){Spacing=3,Margin=new Thickness(2,5,2,5)};CheckBox selected=new(){Content=$"{candidate.DisplayName} · {candidate.Version??"version unknown"} · {candidate.Status}",IsChecked=candidate.Status==EmulatorCandidateStatus.DetectedAndSupported&&candidate.ExistingEmulatorId is null,IsEnabled=candidate.ExistingEmulatorId is null&&candidate.Status is not EmulatorCandidateStatus.InvalidInstallation and not EmulatorCandidateStatus.Missing};row.Children.Add(selected);row.Children.Add(new TextBlock{Text=$"{candidate.DetectionSource} · {Path.GetFileName(candidate.ExecutablePath)} · confidence {candidate.Confidence:P0}",Opacity=.72});list.Items.Add(row);rows.Add((selected,candidate));}panel.Children.Add(list);
        ContentDialogResult choice=await Dialog("Find Installed Emulators",panel,"Add Selected","Refresh","Close").ShowAsync();if(choice==ContentDialogResult.Secondary){await ShowEmulatorDiscoveryAsync();return;}if(choice!=ContentDialogResult.Primary)return;int added=0;foreach(var row in rows.Where(value=>value.Box.IsChecked==true)){NexaStation.EmulatorDiscovery.Definitions.EmulatorDefinition? definition=NexaStation.EmulatorDiscovery.Definitions.BuiltInEmulatorDefinitions.All.FirstOrDefault(value=>value.Id==row.Candidate.DefinitionId);if(definition is null||_emulatorAdapters.GetById(definition.AdapterId) is null)continue;await _emulationLibrary.AddInstallationAsync(definition.AdapterId,row.Candidate.ExecutablePath,row.Candidate.DisplayName,row.Candidate.IsPortable);added++;}ViewModel.ReportStatus($"Added {added} confirmed emulator installation(s). Existing configurations were preserved.");
    }

    private async Task AddEmulatorAsync()
    {
        ComboBox adapterBox=new(){Header="Emulator type",ItemsSource=_emulatorAdapters.Adapters.OrderBy(a=>a.DisplayName).ToArray(),DisplayMemberPath="DisplayName",SelectedIndex=0,MinWidth=360};
        if(await Dialog("Add Emulator · 1 of 4",adapterBox,"Select",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||adapterBox.SelectedItem is not IEmulatorAdapter adapter)return;
        string? executable=await PickEmulatorExecutableAsync();if(executable is null)return;
        EmulatorProbeResult probe=await adapter.ProbeAsync(executable);StackPanel probePanel=new(){Spacing=8};probePanel.Children.Add(new TextBlock{Text=$"Detected name: {probe.DetectedName??adapter.DisplayName}\nVersion: {probe.Version??"Not detected"}\nExecutable: {executable}\nPlatforms: {string.Join(", ",adapter.SupportedPlatformIds)}\nCapabilities: {probe.Capabilities}",TextWrapping=TextWrapping.Wrap});if(probe.Warnings.Count>0)probePanel.Children.Add(new TextBlock{Text=string.Join(Environment.NewLine,probe.Warnings),TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Microsoft.UI.Colors.Orange)});
        if(await Dialog("Add Emulator · 2 of 4 · Safe probe",probePanel,"Continue",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        TextBox name=new(){Header="Installation name",Text=probe.DetectedName??adapter.DisplayName};CheckBox portable=new(){Content="Portable build"};StackPanel namePanel=new(){Spacing=8};namePanel.Children.Add(name);namePanel.Children.Add(portable);
        if(await Dialog("Add Emulator · 3 of 4",namePanel,"Review",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        TextBox? manualTemplate=adapter.Id is "custom" or "rpcs3"?new TextBox{Header="Confirmed launch template",Text="{EMULATOR_PATH} {GAME_PATH}",MinWidth=420}:null;StackPanel review=new(){Spacing=8};review.Children.Add(new TextBlock{Text=$"{name.Text}\n{adapter.DisplayName}\n{executable}\nVersion: {probe.Version??"manual / unknown"}\nNexaStation will not change this emulator's configuration.",TextWrapping=TextWrapping.Wrap});if(manualTemplate is not null)review.Children.Add(manualTemplate);if(await Dialog("Add Emulator · 4 of 4",review,"Save",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        EmulatorInstallation saved=await _emulationLibrary.AddInstallationAsync(adapter.Id,executable,name.Text,portable.IsChecked==true);if(manualTemplate is not null){if(manualTemplate.Text.IndexOfAny(['&','|',';','\r','\n'])>=0){ViewModel.ReportStatus("Shell chaining is not allowed in emulator templates.");return;}saved=saved with{ArgumentsOverride=manualTemplate.Text,UpdatedAtUtc=DateTime.UtcNow};await _emulation.SaveInstallationAsync(saved);}ViewModel.ReportStatus($"Added {saved.DisplayName}. Probe status: {saved.ValidationStatus}.");
    }

    private async Task ConfigureRetroArchCoreAsync(EmulatorInstallation installation)
    {
        string? folder=await PickFolderAsync();if(folder is null)return;IReadOnlyList<RetroArchCore> discovered=await _retroArchCores.DiscoverAsync(installation,folder);if(discovered.Count==0){ViewModel.ReportStatus("No *_libretro.dll cores were found in the selected folder.");return;}IReadOnlyList<EmulatorPlatform> platforms=await _emulation.GetPlatformsAsync();ComboBox coreBox=new(){Header="Core",ItemsSource=discovered,DisplayMemberPath="DisplayName",SelectedIndex=0,MinWidth=360};ComboBox platformBox=new(){Header="Assign platform",ItemsSource=platforms,DisplayMemberPath="Name",SelectedIndex=0,MinWidth=360};StackPanel panel=new(){Spacing=8};panel.Children.Add(coreBox);panel.Children.Add(platformBox);if(await Dialog("RetroArch Core",panel,"Save",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||coreBox.SelectedItem is not RetroArchCore core||platformBox.SelectedItem is not EmulatorPlatform platform)return;await _emulation.SaveRetroArchCoreAsync(core with{SupportedPlatformIds=[platform.Id]});ViewModel.ReportStatus($"Saved {core.DisplayName} for {platform.Name}. No core files were modified.");
    }

    private async Task AddGameFolderAsync()
    {
        IReadOnlyList<EmulatorPlatform> platforms=await _emulation.GetPlatformsAsync();ComboBox platformBox=new(){Header="Platform",ItemsSource=platforms.Where(p=>p.Enabled).ToArray(),DisplayMemberPath="Name",SelectedIndex=0,MinWidth=360};if(await Dialog("Add Game Folder · 1 of 5",platformBox,"Select",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||platformBox.SelectedItem is not EmulatorPlatform platform)return;
        string? folder=await PickFolderAsync();if(folder is null)return;
        IReadOnlyList<EmulatorInstallation> installations=await _emulation.GetInstallationsAsync();ComboBox emulatorBox=new(){Header="Preferred emulator",ItemsSource=installations.Where(i=>i.Enabled).ToArray(),DisplayMemberPath="DisplayName",SelectedIndex=installations.Count>0?0:-1,MinWidth=360};CheckBox recursive=new(){Content="Include subfolders",IsChecked=true};CheckBox hidden=new(){Content="Include hidden files",IsChecked=false};StackPanel options=new(){Spacing=8};options.Children.Add(emulatorBox);options.Children.Add(recursive);options.Children.Add(hidden);options.Children.Add(new TextBlock{Text=$"File types: {string.Join(", ",platform.DefaultExtensions)}\nArchives are not scanned or extracted.",TextWrapping=TextWrapping.Wrap,Opacity=0.75});if(await Dialog("Add Game Folder · 3 of 5",options,"Save and Scan",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        DateTime now=DateTime.UtcNow;GameSource source=new(){Id=Guid.NewGuid().ToString("N"),Name=$"{platform.ShortName} Games",FolderPath=folder,PlatformId=platform.Id,IncludeSubfolders=recursive.IsChecked==true,ScanHiddenFiles=hidden.IsChecked==true,FollowSymbolicLinks=false,Enabled=true,AdapterPreferenceId=(emulatorBox.SelectedItem as EmulatorInstallation)?.Id,CreatedAtUtc=now,UpdatedAtUtc=now};await _emulation.SaveGameSourceAsync(source);await ScanAndPreviewAsync(source);
    }

    private async Task StartEmulatedGameImportAsync()
    {
        IReadOnlyList<GameSource> sources=await _emulation.GetGameSourcesAsync();if(sources.Count==0){await AddGameFolderAsync();return;}ComboBox sourceBox=new(){Header="Game folder",ItemsSource=sources.Where(s=>s.Enabled).ToArray(),DisplayMemberPath="Name",SelectedIndex=0,MinWidth=420};if(await Dialog("Import Emulated Games",sourceBox,"Scan",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&sourceBox.SelectedItem is GameSource source)await ScanAndPreviewAsync(source);
    }

    private async Task ScanAndPreviewAsync(GameSource source)
    {
        ViewModel.ReportStatus($"Scanning {source.Name}…");GameSourceScanResult scan=await _emulationLibrary.ScanAsync(source);if(scan.WasCancelled)return;if(!scan.CompletedSuccessfully){ViewModel.ReportStatus($"Scan failed: {string.Join(" · ",scan.Errors)}");return;}
        IReadOnlyList<EmulatorInstallation> installations=await _emulation.GetInstallationsAsync();StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text=$"{scan.Games.Count} matching files found. Files are only indexed; external content will not be changed.",TextWrapping=TextWrapping.Wrap});StackPanel controls=new(){Orientation=Orientation.Horizontal,Spacing=8};Button select=new(){Content="Select All Valid"};Button clear=new(){Content="Clear Selection"};controls.Children.Add(select);controls.Children.Add(clear);panel.Children.Add(controls);ListView list=new(){Height=460,SelectionMode=ListViewSelectionMode.None};List<(CheckBox Box,ComboBox Emulator,DiscoveredGame Game)> rows=[];
        foreach(DiscoveredGame game in scan.Games){StackPanel row=new(){Spacing=4,Margin=new Thickness(2,4,2,4)};bool valid=game.Status is EmulatedImportStatus.New or EmulatedImportStatus.Changed or EmulatedImportStatus.PossibleMultiDiscGame;CheckBox box=new(){Content=$"{game.SuggestedTitle} · {game.Status} · {Path.GetFileName(game.GamePath)}",IsChecked=valid};ComboBox emulator=new(){ItemsSource=installations.Where(i=>i.Enabled).ToArray(),DisplayMemberPath="DisplayName",SelectedValuePath="Id",SelectedValue=game.PreferredEmulatorInstallationId,PlaceholderText="Choose emulator",MinWidth=280};row.Children.Add(box);row.Children.Add(emulator);list.Items.Add(row);rows.Add((box,emulator,game));}
        select.Click+=(_,_)=>{foreach(var row in rows)if(row.Game.Status is not EmulatedImportStatus.UnsupportedFile and not EmulatedImportStatus.Invalid and not EmulatedImportStatus.AlreadyImported)row.Box.IsChecked=true;};clear.Click+=(_,_)=>{foreach(var row in rows)row.Box.IsChecked=false;};panel.Children.Add(list);ContentDialogResult result=await Dialog("Import Preview",panel,"Import Selected","Rescan","Cancel").ShowAsync();if(result==ContentDialogResult.Secondary){await ScanAndPreviewAsync(source);return;}if(result!=ContentDialogResult.Primary)return;
        EmulatedGameImportSelection[] selections=rows.Select(row=>new EmulatedGameImportSelection(row.Game with{PreferredEmulatorInstallationId=(row.Emulator.SelectedItem as EmulatorInstallation)?.Id},row.Box.IsChecked==true)).ToArray();EmulatedGameImportResult imported=await _emulationLibrary.ImportAsync(source,selections);await ViewModel.RefreshAsync();await EnsureAutomaticSmartLibrariesAsync();ViewModel.ReportStatus($"Emulation import: {imported.Added} added, {imported.Updated} updated, {imported.Skipped} skipped, {imported.Failed} failed.");
    }

    private async Task<string?> PickEmulatorExecutableAsync(){FileOpenPicker picker=new();picker.FileTypeFilter.Add(".exe");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFileAsync())?.Path;}

    private async Task ShowWindowsApplicationImportAsync()
    {
        bool currentUser=await _settings.GetAsync("Providers.WindowsApps.ScanCurrentUser",true);bool common=await _settings.GetAsync("Providers.WindowsApps.ScanCommon",true);bool appsFolder=await _settings.GetAsync("Providers.WindowsApps.IncludeAppsFolder",true);bool pwas=await _settings.GetAsync("Providers.WindowsApps.DetectPwas",true);
        ViewModel.ReportStatus("Scanning Windows-visible application entries…");
        WindowsApplicationScanResult scan=await _windowsApplications.ScanAllAsync(new(currentUser,common,true),appsFolder,pwas);
        if(scan.WasCancelled)return;if(!scan.CompletedSuccessfully&&scan.Candidates.Count==0){ViewModel.ReportStatus($"Windows application scan failed: {string.Join(" · ",scan.Errors)}");return;}
        WindowsApplicationImportPreview preview=await _windowsApplications.CreatePreviewAsync(scan);IReadOnlyList<ApplicationCategory> categories=await _windowsPersistence.GetCategoriesAsync();
        StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text=$"{preview.Candidates.Count} Windows-visible entries found. Recommended selection excludes maintenance tools, uninstallers, documentation, and ordinary website shortcuts.",TextWrapping=TextWrapping.Wrap});
        TextBox search=new(){Header="Search preview",PlaceholderText="Name, source, status, or category"};panel.Children.Add(search);StackPanel controls=new(){Orientation=Orientation.Horizontal,Spacing=8};Button recommended=new(){Content="Select Recommended"};Button allVisible=new(){Content="Select All Visible"};Button clear=new(){Content="Clear Selection"};controls.Children.Add(recommended);controls.Children.Add(allVisible);controls.Children.Add(clear);panel.Children.Add(controls);
        ListView list=new(){Height=500,SelectionMode=ListViewSelectionMode.None};List<(FrameworkElement Row,CheckBox Selected,ComboBox Action,ComboBox Category,CheckBox Desktop,WindowsApplicationPreviewCandidate Candidate)> rows=[];
        foreach(WindowsApplicationPreviewCandidate candidate in preview.Candidates)
        {
            StackPanel row=new(){Spacing=4,Margin=new Thickness(2,5,2,5),Tag=candidate};CheckBox selected=new(){Content=$"{candidate.Candidate.DisplayName} · {candidate.Status} · {candidate.Candidate.ProviderId}",IsChecked=candidate.SelectedByDefault};TextBlock reason=new(){Text=$"{candidate.Classification.SuggestedCategory} · {candidate.Classification.Confidence} confidence · {candidate.Classification.Reason}",Opacity=0.7,TextWrapping=TextWrapping.Wrap};StackPanel options=new(){Orientation=Orientation.Horizontal,Spacing=8};
            ComboBox action=new(){Header="Duplicate action",Width=205,ItemsSource=Enum.GetValues<WindowsImportAction>()};action.SelectedItem=candidate.Status==WindowsImportStatus.PossibleDuplicate?WindowsImportAction.LinkToExisting:WindowsImportAction.Import;
            ComboBox category=new(){Header="Category",Width=185,ItemsSource=categories,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=candidate.Classification.SuggestedCategory.ToLowerInvariant().Replace(' ','-')};CheckBox desktop=new(){Content="Enable desktop control (Basic Mouse)",VerticalAlignment=VerticalAlignment.Bottom,IsChecked=false};options.Children.Add(action);options.Children.Add(category);options.Children.Add(desktop);row.Children.Add(selected);row.Children.Add(reason);row.Children.Add(options);list.Items.Add(row);rows.Add((row,selected,action,category,desktop,candidate));
        }
        search.TextChanged+=(_,_)=>{string term=search.Text.Trim();foreach(var row in rows)row.Row.Visibility=term.Length==0||$"{row.Candidate.Candidate.DisplayName} {row.Candidate.Candidate.ProviderId} {row.Candidate.Status} {row.Candidate.Classification.SuggestedCategory}".Contains(term,StringComparison.OrdinalIgnoreCase)?Visibility.Visible:Visibility.Collapsed;};
        recommended.Click+=(_,_)=>{foreach(var row in rows)row.Selected.IsChecked=row.Row.Visibility==Visibility.Visible&&row.Candidate.SelectedByDefault;};allVisible.Click+=(_,_)=>{foreach(var row in rows)if(row.Row.Visibility==Visibility.Visible)row.Selected.IsChecked=true;};clear.Click+=(_,_)=>{foreach(var row in rows)row.Selected.IsChecked=false;};panel.Children.Add(list);
        if(scan.Warnings.Count>0||scan.Errors.Count>0)panel.Children.Add(new TextBlock{Text=$"Diagnostics: {scan.Warnings.Count} warning(s), {scan.Errors.Count} error(s). Individual unreadable entries were skipped.",TextWrapping=TextWrapping.Wrap,Opacity=0.7});
        ContentDialogResult result=await Dialog("Import Windows Applications",panel,"Import Selected","Rescan","Cancel").ShowAsync();if(result==ContentDialogResult.Secondary){await ShowWindowsApplicationImportAsync();return;}if(result!=ContentDialogResult.Primary)return;
        WindowsApplicationImportSelection[] selections=rows.Where(row=>row.Selected.IsChecked==true).Select(row=>new WindowsApplicationImportSelection(row.Candidate,(WindowsImportAction)(row.Action.SelectedItem??WindowsImportAction.Import),row.Candidate.ExistingLibraryItemId,(row.Category.SelectedItem as ApplicationCategory)?.Id,row.Desktop.IsChecked==true?"basic-mouse":null,row.Desktop.IsChecked==true)).ToArray();
        WindowsApplicationImportResult imported=await _windowsApplications.ImportAsync(selections);await ViewModel.RefreshAsync();await EnsureAutomaticSmartLibrariesAsync();ViewModel.ReportStatus($"Windows application import: {imported.Added} added, {imported.Updated} updated, {imported.Linked} linked, {imported.AlternateTargets} alternate targets, {imported.Skipped} skipped, {imported.Failed} failed.");
    }

    private async Task ShowWindowsApplicationSettingsAsync()
    {
        CheckBox current=new(){Content="Scan current-user Start Menu",IsChecked=await _settings.GetAsync("Providers.WindowsApps.ScanCurrentUser",true)};CheckBox common=new(){Content="Scan all-users Start Menu",IsChecked=await _settings.GetAsync("Providers.WindowsApps.ScanCommon",true)};CheckBox apps=new(){Content="Include current-user packaged applications",IsChecked=await _settings.GetAsync("Providers.WindowsApps.IncludeAppsFolder",true)};CheckBox pwas=new(){Content="Detect installed web apps from Windows-visible entries",IsChecked=await _settings.GetAsync("Providers.WindowsApps.DetectPwas",true)};CheckBox startup=new(){Content="Refresh imported Windows applications at startup",IsChecked=await _settings.GetAsync("Providers.WindowsApps.RefreshOnStartup",false)};StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text="Discovery is read-only. NexaStation never scans browser profiles, installs software, uninstalls software, or modifies Start Menu entries.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(current);panel.Children.Add(common);panel.Children.Add(apps);panel.Children.Add(pwas);panel.Children.Add(startup);if(await Dialog("Settings · Windows Applications",panel,"Save",null,"Close").ShowAsync()!=ContentDialogResult.Primary)return;await _settings.SetAsync("Providers.WindowsApps.ScanCurrentUser",current.IsChecked==true);await _settings.SetAsync("Providers.WindowsApps.ScanCommon",common.IsChecked==true);await _settings.SetAsync("Providers.WindowsApps.IncludeAppsFolder",apps.IsChecked==true);await _settings.SetAsync("Providers.WindowsApps.DetectPwas",pwas.IsChecked==true);await _settings.SetAsync("Providers.WindowsApps.RefreshOnStartup",startup.IsChecked==true);ViewModel.ReportStatus("Windows application discovery settings saved.");
    }

    private async Task ShowDesktopControlSettingsAsync()
    {
        StackPanel panel=new(){Spacing=9};panel.Children.Add(new TextBlock{Text="Desktop controller mode is opt-in per Windows application. Left stick moves the cursor; A/B click; right stick scrolls; both stick buttons immediately disable the session.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(new TextBlock{Text="Safety: elevated targets are refused, physical mouse activity pauses injection, disconnect/app exit releases all buttons, and only a small approved key set is available. Games and emulators never enable this mode.",TextWrapping=TextWrapping.Wrap,Opacity=0.75});await Dialog("Desktop Control",panel,"Close",null,string.Empty).ShowAsync();
    }

    private async Task ShowDetailsAsync(CardViewModel card)
    {
        LibraryItem item = (await _library.GetByIdAsync(card.Id)) ?? card.Item;
        ContentRestrictionState restriction = await _contentRestrictions.GetAsync(item.Id, RestrictedContentKind.LibraryItem);
        if (_guestMode.IsGuest && restriction != ContentRestrictionState.Allowed)
        {
            await Dialog("Restricted item", new TextBlock { Text = "This item is hidden by the active Guest Mode restrictions.", TextWrapping = TextWrapping.Wrap }, "Close", null, string.Empty).ShowAsync();
            return;
        }
        if(Debugger.IsAttached)_logger.Information(
            "ArtworkResolutionDiagnostic",
            "Artwork resolution was inspected for the selected item.",
            new Dictionary<string, object?> { ["ItemId"] = item.Id, ["Diagnostic"] = card.ArtworkDiagnostic });
        LaunchTarget? target = item.LaunchTarget;
        CompatibilityResolutionResult? compatibility=target is null?null:await _compatibilityResolver.ResolveAsync(CompatibilityLaunchPolicy.CreateTarget(item,target));
        string? compatibilityIdentity=compatibility?.Target.ProviderItemId??compatibility?.Target.AppUserModelId??compatibility?.Target.ExecutableIdentity;
        CompatibilityReport? latestCompatibility=(await _compatibilityReports.GetRecentAsync(500)).FirstOrDefault(report=>report.TargetType==compatibility?.Target.TargetType&&string.Equals(report.TargetIdentity,compatibilityIdentity,StringComparison.OrdinalIgnoreCase));
        WindowsApplicationSource? windowsSource=await _windowsPersistence.GetSourceForItemAsync(item.Id);
        EmulatedGameSource? emulatedGame=target?.EmulatedGameSourceId is string gameSourceId?await _emulation.GetEmulatedGameAsync(gameSourceId):null;
        EmulatorInstallation? selectedEmulator=emulatedGame?.PreferredEmulatorInstallationId is string emulatorId?await _emulation.GetInstallationAsync(emulatorId):null;
        string? platformDisplayName=emulatedGame is null?null:(await _emulation.GetPlatformsAsync()).FirstOrDefault(platform=>platform.Id==emulatedGame.PlatformId)?.Name??emulatedGame.PlatformId;
        string targetText = item.LaunchTargets.Count==0?"No launch target":string.Join(Environment.NewLine,item.LaunchTargets.Select(value=>$"{value.Name} · {value.ProviderId??"Manual"} · {value.LaunchType} · {(value.ExecutablePath??value.ShortcutPath??value.Uri?.AbsoluteUri)}{(value.Id==item.DefaultLaunchTargetId?" · Default":string.Empty)}"));
        bool missing = LaunchTargetValidator.Validate(target) is not null||emulatedGame is not null&&(!File.Exists(emulatedGame.GamePath)&&!Directory.Exists(emulatedGame.GamePath));
        StackPanel panel = new() { Spacing = 10 };
        if (item.CoverAsset is { LocalPath: var cover } && File.Exists(cover))
            panel.Children.Add(new Image { Height = 210, Stretch = Stretch.UniformToFill, Source = new BitmapImage(new Uri(cover)) });
        panel.Children.Add(new TextBlock { Text = item.Subtitle ?? item.ContentType.ToString(), FontSize = 16, Opacity = 0.75 });
        panel.Children.Add(new TextBlock { Text = item.Description ?? "No description.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = $"{TimeSpan.FromSeconds(item.TotalPlaytimeSeconds):g} total · Last launched: {item.LastLaunchedAtUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "Never"}" });
        panel.Children.Add(new TextBlock { Text = targetText, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        if(compatibility is not null)panel.Children.Add(new TextBlock{Text=$"Compatibility: {latestCompatibility?.Status.ToString()??CompatibilityStatus.Untested.ToString()}\nVerification: {(latestCompatibility is null?"Not locally verified":$"Locally verified with NexaStation {latestCompatibility.NexaStationVersion} on {latestCompatibility.CreatedAtUtc.LocalDateTime:g}")}\nActive profile: {(compatibility.AppliedProfiles.Count>0?compatibility.AppliedProfiles[0].Id:"Safe defaults")}\nApplied overrides: {(compatibility.HasOverrides?string.Join(", ",compatibility.AppliedProfiles.Select(profile=>profile.Id)):"None")}\nConflicts: {compatibility.Conflicts.Count}",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Microsoft.UI.Colors.LightSkyBlue)});
        if(Debugger.IsAttached)panel.Children.Add(new TextBlock{Text="Artwork diagnostics\n"+card.ArtworkDiagnostic,TextWrapping=TextWrapping.Wrap,FontFamily=new FontFamily("Cascadia Mono"),FontSize=11,Opacity=.75});
        if(item.LaunchTargets.Any(value=>value.ProviderId=="steam"))panel.Children.Add(new TextBlock{Text="Provider · Steam",Foreground=new SolidColorBrush(Microsoft.UI.Colors.LightSkyBlue),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        if(windowsSource is not null)panel.Children.Add(new TextBlock{Text=$"Provider · {windowsSource.ProviderId}\nSource: {windowsSource.SourceType}\nCategory: {item.PrimaryCategoryName??"Uncategorized"}\nCard style: {item.CardStyle}\nLaunch method: {target?.WindowsLaunchMethod}\nShortcut: {windowsSource.ShortcutPath??"None"}\nTarget: {windowsSource.TargetPath??windowsSource.AppUserModelId??windowsSource.ProtocolUri?.AbsoluteUri??"Windows shell identity"}\nTracking: {target?.TrackingMode}\nDesktop control: {(item.DesktopControlEnabled?"Enabled":"Disabled")}",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Microsoft.UI.Colors.LightSkyBlue)});
        if(emulatedGame is not null)panel.Children.Add(new TextBlock{Text=$"Provider · Emulation\nPlatform: {platformDisplayName}\nEmulator: {selectedEmulator?.DisplayName??"Not configured"}\nGame file: {emulatedGame.GamePath}\nSize: {(emulatedGame.FileSize is long size?$"{size:N0} bytes":"Unknown")}\nRegion: {emulatedGame.Region??"Unknown"}\nIdentifier: {emulatedGame.GameIdentifier??"Filename-only identification"}\nDiscs: {emulatedGame.DiscCount?.ToString(CultureInfo.InvariantCulture)??"1"}",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Microsoft.UI.Colors.LightSkyBlue)});
        if (missing) panel.Children.Add(new TextBlock { Text = "This installation or game file could not be located. Use the emulation recovery actions below.", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange) });
        ContentDialog dialog = Dialog(item.Title, panel, missing ? "Relink" : target?.LaunchType is LaunchType.WebApplication or LaunchType.Uri ? "Open" : "Play", "Edit", "Close");
        StackPanel actions = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        Button favoriteAction = new() { Content = item.Favorite ? "Unfavorite" : "Favorite" };
        favoriteAction.Click += async (_, _) => { await ViewModel.ToggleFavoriteAsync(new CardViewModel(item, card.Glyph)); dialog.Hide(); };
        Button hideAction = new() { Content = item.Hidden ? "Unhide" : "Hide" };
        hideAction.Click += async (_, _) => { await ViewModel.SetHiddenAsync(new CardViewModel(item, card.Glyph), !item.Hidden); dialog.Hide(); };
        Button removeAction = new() { Content = "Remove" };
        removeAction.Click += async (_, _) => { dialog.Hide(); await ConfirmRemoveAsync(new CardViewModel(item, card.Glyph)); };
        actions.Children.Add(favoriteAction); actions.Children.Add(hideAction); actions.Children.Add(removeAction);
        if(item.ContentType==ContentType.Game)
        {
            Button findArtwork=new(){Content="Find Artwork"};
            findArtwork.Click+=async(_,_)=>{dialog.Hide();await ShowFindMissingArtworkAsync(item.Id);};
            actions.Children.Add(findArtwork);
        }
        if (!_guestMode.IsGuest)
        {
            Button guestAccess = new() { Content = restriction == ContentRestrictionState.Allowed ? "Restrict for Guest" : "Allow for Guest" };
            guestAccess.Click += async (_, _) => { await _contentRestrictions.SetAsync(item.Id, RestrictedContentKind.LibraryItem, restriction == ContentRestrictionState.Allowed ? ContentRestrictionState.HiddenInGuest : ContentRestrictionState.Allowed); dialog.Hide(); ViewModel.ReportStatus("Guest access for this item was updated."); };
            actions.Children.Add(guestAccess);
        }
        if(compatibility is not null)
        {
            Button recordCompatibility=new(){Content="Record Compatibility"};recordCompatibility.Click+=async(_,_)=>{dialog.Hide();await RecordCompatibilityAsync(item,compatibility);};actions.Children.Add(recordCompatibility);
            Button reportProblem=new(){Content="Report a Problem"};reportProblem.Click+=async(_,_)=>{dialog.Hide();await ShowReportProblemAsync(latestCompatibility,compatibility.AppliedProfiles.Count>0?compatibility.AppliedProfiles[0]:null);};actions.Children.Add(reportProblem);
            string[] userProfiles=compatibility.AppliedProfiles.Where(profile=>profile.Source==CompatibilityProfileSource.User).Select(profile=>profile.Id).ToArray();
            if(userProfiles.Length>0){Button resetCompatibility=new(){Content="Reset User Overrides"};resetCompatibility.Click+=async(_,_)=>{dialog.Hide();if(await Dialog("Reset compatibility overrides?",new TextBlock{Text="Only user compatibility profiles applied to this item will be removed. Built-in rules and local reports remain.",TextWrapping=TextWrapping.Wrap},"Reset",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;await _compatibilityProfiles.DeleteUserProfilesAsync(userProfiles);ViewModel.ReportStatus("User compatibility overrides were reset for this item.");};actions.Children.Add(resetCompatibility);}
        }
        if(windowsSource is not null)
        {
            Button cardStyle=new(){Content=$"Card: {item.CardStyle}"};cardStyle.Click+=async(_,_)=>{ApplicationCardStyle next=item.CardStyle switch{ApplicationCardStyle.Wide=>ApplicationCardStyle.Square,ApplicationCardStyle.Square=>ApplicationCardStyle.Compact,ApplicationCardStyle.Compact=>ApplicationCardStyle.Icon,_=>ApplicationCardStyle.Wide};await _library.UpdateAsync(item with{CardStyle=next,UpdatedAtUtc=DateTime.UtcNow});dialog.Hide();await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Application card style changed to {next}.");};actions.Children.Add(cardStyle);
            Button desktop=new(){Content=item.DesktopControlEnabled?"Disable Desktop Control":"Enable Desktop Control"};desktop.Click+=async(_,_)=>{await _windowsPersistence.SetDesktopControlAssignmentAsync(item.DesktopControlEnabled?null:new DesktopControlAssignment(item.Id,"basic-mouse",true),item.Id);dialog.Hide();await ViewModel.RefreshAsync();ViewModel.ReportStatus(item.DesktopControlEnabled?"Desktop control disabled.":"Desktop control enabled with the Basic Mouse profile.");};actions.Children.Add(desktop);
            Button category=new(){Content="Change Category"};category.Click+=async(_,_)=>{IReadOnlyList<ApplicationCategory> choices=await _windowsPersistence.GetCategoriesAsync();ComboBox box=new(){ItemsSource=choices,DisplayMemberPath="Name",SelectedValuePath="Name",SelectedValue=item.PrimaryCategoryName,MinWidth=280};if(await Dialog("Application Category",box,"Save",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&box.SelectedItem is ApplicationCategory choice){await _windowsPersistence.SetCategoriesAsync(item.Id,[new(item.Id,choice.Id,true,ApplicationCategorySource.User)]);dialog.Hide();await ViewModel.RefreshAsync();}};actions.Children.Add(category);
        }
        if(item.LaunchTargets.FirstOrDefault(value=>value.ProviderId=="steam"&&value.Uri is not null) is LaunchTarget steamTarget)
        {
            Button openSteam=new(){Content="Open in Steam"};openSteam.Click+=(_,_)=>Process.Start(new ProcessStartInfo{FileName=steamTarget.Uri!.AbsoluteUri,UseShellExecute=true});actions.Children.Add(openSteam);
        }
        if(emulatedGame is not null)
        {
            Button changeEmulator=new(){Content="Change Emulator"};changeEmulator.Click+=async(_,_)=>{dialog.Hide();IReadOnlyList<EmulatorInstallation> choices=await _emulation.GetInstallationsAsync();ComboBox combo=new(){ItemsSource=choices.Where(value=>value.Enabled).ToArray(),DisplayMemberPath="DisplayName",SelectedValuePath="Id",SelectedValue=emulatedGame.PreferredEmulatorInstallationId,MinWidth=340};if(await Dialog("Preferred emulator",combo,"Continue",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&combo.SelectedItem is EmulatorInstallation choice){string? coreId=null;if(string.Equals(choice.AdapterId,"retroarch",StringComparison.OrdinalIgnoreCase)){IReadOnlyList<RetroArchCore> cores=await _emulation.GetRetroArchCoresAsync(choice.Id);ComboBox coreBox=new(){ItemsSource=cores.Where(core=>core.Available&&core.SupportedPlatformIds.Contains(emulatedGame.PlatformId,StringComparer.OrdinalIgnoreCase)).ToArray(),DisplayMemberPath="DisplayName",SelectedValuePath="Id",SelectedValue=emulatedGame.RetroArchCoreId,PlaceholderText="Select required core",MinWidth=340};if(await Dialog("RetroArch Core",coreBox,"Save",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||coreBox.SelectedItem is not RetroArchCore core)return;coreId=core.Id;}await _emulation.SetPreferredEmulatorAsync(emulatedGame.Id,choice.Id,coreId);ViewModel.ReportStatus("Per-game emulator and core preference updated.");}};actions.Children.Add(changeEmulator);
            Button relinkGame=new(){Content="Relink Game"};relinkGame.Click+=async(_,_)=>{string? replacement=await PickGameFileAsync();if(replacement is not null){await _emulation.RelinkGameAsync(emulatedGame.Id,replacement);dialog.Hide();ViewModel.ReportStatus("Game path relinked; artwork, favorites, and playtime were preserved.");}};actions.Children.Add(relinkGame);
            string? gameDirectory=Path.GetDirectoryName(emulatedGame.GamePath);if(Directory.Exists(gameDirectory)){Button openGameFolder=new(){Content="Open Game Folder"};openGameFolder.Click+=(_,_)=>Process.Start(new ProcessStartInfo{FileName=gameDirectory!,UseShellExecute=true});actions.Children.Add(openGameFolder);}
        }
        if (target?.ExecutablePath is { } executable)
        {
            Button openFolder = new() { Content = "Open folder" };
            openFolder.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(executable)!, UseShellExecute = true });
            actions.Children.Add(openFolder);
        }
        panel.Children.Add(actions);
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if(missing&&emulatedGame is not null){if(!File.Exists(emulatedGame.GamePath)&&!Directory.Exists(emulatedGame.GamePath)){string? path=await PickGameFileAsync();if(path is not null)await _emulation.RelinkGameAsync(emulatedGame.Id,path);}else await ShowEmulationSettingsAsync();}
            else if (missing) await RelinkAsync(item); else await ViewModel.LaunchAsync(new CardViewModel(item, card.Glyph));
        }
        else if (result == ContentDialogResult.Secondary){if(emulatedGame is not null)await ShowEmulationSettingsAsync();else await ShowItemEditorAsync(item);}
    }

    private async Task RecordCompatibilityAsync(LibraryItem item,CompatibilityResolutionResult resolution)
    {
        CompatibilityStatus[] statuses=[CompatibilityStatus.Untested,CompatibilityStatus.Launches,CompatibilityStatus.LaunchesWithLimitations,CompatibilityStatus.Broken,CompatibilityStatus.Unsupported,CompatibilityStatus.NeedsRetest];
        ComboBox status=new(){Header="Compatibility status",ItemsSource=statuses,SelectedItem=CompatibilityStatus.LaunchesWithLimitations,MinWidth=320};
        ComboBox tracking=new(){Header="Process tracking",ItemsSource=Enum.GetValues<CompatibilityResult>(),SelectedItem=CompatibilityResult.Unknown,MinWidth=320};
        ComboBox restoration=new(){Header="Frontend restoration",ItemsSource=Enum.GetValues<CompatibilityResult>(),SelectedItem=CompatibilityResult.Unknown,MinWidth=320};
        ComboBox input=new(){Header="Controller/input behavior",ItemsSource=Enum.GetValues<CompatibilityResult>(),SelectedItem=CompatibilityResult.Unknown,MinWidth=320};
        ComboBox performance=new(){Header="Performance",ItemsSource=Enum.GetValues<CompatibilityResult>(),SelectedItem=CompatibilityResult.Unknown,MinWidth=320};
        TextBox notes=new(){Header="Known limitations or verification notes",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=100};
        StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text="This records a local result only. 'Fully Compatible' requires repeated launch, tracking, restoration, focus, input, relaunch, and side-effect verification and cannot be selected from this single-run form.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(status);panel.Children.Add(tracking);panel.Children.Add(restoration);panel.Children.Add(input);panel.Children.Add(performance);panel.Children.Add(notes);
        if(await Dialog($"Compatibility · {item.Title}",panel,"Save Local Report",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        CompatibilityStatus selected=(CompatibilityStatus)status.SelectedItem;
        CompatibilityResult launch=selected switch{CompatibilityStatus.Launches=>CompatibilityResult.Passed,CompatibilityStatus.LaunchesWithLimitations=>CompatibilityResult.PassedWithLimitations,CompatibilityStatus.Broken=>CompatibilityResult.Failed,CompatibilityStatus.Unsupported=>CompatibilityResult.NotApplicable,_=>CompatibilityResult.Unknown};
        IReadOnlyList<ControllerStatus> controllers=await _controllerStatus.GetControllersAsync();
        CompatibilityReport report=new(){Id=Guid.NewGuid().ToString("N"),TargetType=resolution.Target.TargetType,TargetIdentity=resolution.Target.ProviderItemId??resolution.Target.AppUserModelId??resolution.Target.ExecutableIdentity,DisplayName=item.Title,ProviderId=resolution.Target.ProviderId,ApplicationVersion=resolution.Target.ApplicationVersion,NexaStationVersion=_releaseInfo.Version.ToString(),WindowsVersion=Environment.OSVersion.Version.ToString(),Architecture=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),ControllerFamily=controllers.Count>0?controllers[0].ControllerFamily:null,DisplayConfigurationSummary=$"Window {Root.ActualWidth:0}x{Root.ActualHeight:0}",LaunchResult=launch,TrackingResult=(CompatibilityResult)tracking.SelectedItem,RestoreResult=(CompatibilityResult)restoration.SelectedItem,InputResult=(CompatibilityResult)input.SelectedItem,PerformanceResult=(CompatibilityResult)performance.SelectedItem,UserNotes=notes.Text,CreatedAtUtc=DateTimeOffset.UtcNow,PrivacyLevel=CompatibilityPrivacyLevel.Standard,Status=selected};
        await _compatibilityReports.SaveAsync(report);ViewModel.ReportStatus("The compatibility result was saved locally and was not uploaded.");
    }

    private async Task ShowItemEditorAsync(LibraryItem? existing = null)
    {
        string kind = existing?.ContentType switch { ContentType.Game => "Game", ContentType.Media => "Media Shortcut", _ => "Application" };
        if (existing is null)
        {
            ComboBox types = new() { ItemsSource = new[] { "Game", "Application", "Web Application", "Media Shortcut", "Custom URI" }, SelectedIndex = 0, MinWidth = 320 };
            ContentDialogResult typeResult = await Dialog("Add Item · 1 of 4", types, "Next", null, "Cancel").ShowAsync();
            if (typeResult != ContentDialogResult.Primary) return;
            kind = (string)types.SelectedItem;
        }

        TextBox targetBox = new() { Header = kind is "Web Application" ? "Website URL" : kind is "Custom URI" or "Media Shortcut" ? "URI" : "Executable or shortcut", Text = existing?.LaunchTarget?.ExecutablePath ?? existing?.LaunchTarget?.ShortcutPath ?? existing?.LaunchTarget?.Uri?.AbsoluteUri ?? string.Empty };
        TextBox arguments = new() { Header = "Launch arguments (optional)", Text = existing?.LaunchTarget?.Arguments ?? string.Empty };
        TextBox working = new() { Header = "Working directory (optional)", Text = existing?.LaunchTarget?.WorkingDirectory ?? string.Empty };
        StackPanel targetPanel = new() { Spacing = 10 }; targetPanel.Children.Add(targetBox);
        if (kind is "Game" or "Application") { Button browse = new() { Content = "Browse for .exe or .lnk" }; browse.Click += async (_, _) => { string? path = await PickLaunchTargetAsync(); if (path is not null) { targetBox.Text = path; if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) working.Text = Path.GetDirectoryName(path) ?? string.Empty; } }; targetPanel.Children.Add(browse); }
        targetPanel.Children.Add(arguments);targetPanel.Children.Add(working);
        if (await Dialog(existing is null ? "Add Item · 2 of 4" : "Edit launch target", targetPanel, "Next", null, "Cancel").ShowAsync() != ContentDialogResult.Primary) return;

        TextBox title = new() { Header = "Title", Text = existing?.Title ?? SuggestTitle(targetBox.Text) };
        TextBox subtitle = new() { Header = "Subtitle", Text = existing?.Subtitle ?? string.Empty };
        TextBox description = new() { Header = "Description", Text = existing?.Description ?? string.Empty, AcceptsReturn = true, Height = 90 };
        CheckBox favorite = new() { Content = "Favorite", IsChecked = existing?.Favorite ?? false };
        CheckBox hide = new() { Content = "Hide NexaStation while running", IsChecked = existing?.LaunchTarget?.HideFrontendWhileRunning ?? kind is not "Web Application" };
        CheckBox restore = new() { Content = "Restore NexaStation after exit", IsChecked = existing?.LaunchTarget?.RestoreFrontendAfterExit ?? kind is not "Web Application" };
        string? artworkPath = existing?.CoverAsset?.LocalPath;
        TextBlock artworkLabel = new() { Text = artworkPath ?? "No cover selected", TextWrapping = TextWrapping.Wrap };
        Button artwork = new() { Content = "Choose local cover" }; artwork.Click += async (_, _) => { artworkPath = await PickArtworkAsync(); artworkLabel.Text = artworkPath ?? "No cover selected"; };
        StackPanel info = new() { Spacing = 9 };foreach (UIElement element in new UIElement[] { title, subtitle, description, artwork, artworkLabel, favorite, hide, restore }) info.Children.Add(element);
        if (await Dialog(existing is null ? "Add Item · 3 of 4" : "Edit item", info, "Review", null, "Cancel").ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(title.Text)) { ViewModel.ReportStatus("Title is required."); return; }

        LaunchType launchType = kind switch { "Web Application" => LaunchType.WebApplication, "Custom URI" or "Media Shortcut" => LaunchType.Uri, _ when targetBox.Text.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) => LaunchType.WindowsShortcut, _ => LaunchType.Executable };
        Uri? uri = launchType is LaunchType.Uri or LaunchType.WebApplication && Uri.TryCreate(targetBox.Text.Trim(), UriKind.Absolute, out Uri? parsed) ? parsed : null;
        LaunchTarget target = new(launchType, launchType == LaunchType.Executable ? Path.GetFullPath(targetBox.Text) : null, string.IsNullOrWhiteSpace(working.Text) ? null : Path.GetFullPath(working.Text), arguments.Text, uri, launchType == LaunchType.Executable ? Path.GetFileNameWithoutExtension(targetBox.Text) : null, hide.IsChecked == true, restore.IsChecked == true)
        { Id = existing?.LaunchTarget?.Id ?? Guid.NewGuid().ToString("N"), LibraryItemId = existing?.Id ?? string.Empty, ShortcutPath = launchType == LaunchType.WindowsShortcut ? Path.GetFullPath(targetBox.Text) : null, ProviderId=existing?.LaunchTarget?.ProviderId,IsEnabled=true,UserManaged=true,CreatedAtUtc = existing?.LaunchTarget?.CreatedAtUtc ?? DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        string? validation = launchType == LaunchType.WebApplication && (uri is null || (uri.Scheme != "http" && uri.Scheme != "https")) ? "Web applications require an http or https URL." : launchType == LaunchType.Uri && uri is null ? "Enter a valid absolute URI." : null;
        if (validation is not null) { ViewModel.ReportStatus(validation); return; }

        LibraryItem? duplicate = existing is null ? await _library.FindDuplicateAsync(target) : null;
        if (duplicate is not null)
        {
            ContentDialogResult duplicateResult = await Dialog("Possible duplicate", new TextBlock { Text = $"{duplicate.Title} uses the same launch target.", TextWrapping = TextWrapping.Wrap }, "Open Existing", "Add Anyway", "Cancel").ShowAsync();
            if (duplicateResult == ContentDialogResult.Primary) { await ShowDetailsAsync(new CardViewModel(duplicate, "\uECAA")); return; }
            if (duplicateResult != ContentDialogResult.Secondary) return;
        }

        DateTime now = DateTime.UtcNow; string id = existing?.Id ?? Guid.NewGuid().ToString("N");target = target with { LibraryItemId = id };
        Asset? coverAsset = artworkPath is null ? null : new(existing?.CoverAsset?.Id ?? Guid.NewGuid().ToString("N"), id, AssetType.Cover, artworkPath, null, null, File.Exists(artworkPath) ? new FileInfo(artworkPath).Length : null, existing?.CoverAsset?.AddedAtUtc ?? now);
        IReadOnlyList<LaunchTarget> targets=existing is null?[target]:existing.LaunchTargets.Select(value=>value.Id==target.Id?target:value).Append(existing.LaunchTargets.Any(value=>value.Id==target.Id)?null:target).OfType<LaunchTarget>().ToArray();
        LibraryItem item = new() { Id=id,Title=title.Text.Trim(),SortTitle=existing?.SortTitle??title.Text.Trim(),Subtitle=subtitle.Text.Trim(),Description=description.Text.Trim(),ContentType=kind=="Game"?ContentType.Game:kind=="Media Shortcut"?ContentType.Media:ContentType.Application,Favorite=favorite.IsChecked==true,Hidden=existing?.Hidden??false,AddedAtUtc=existing?.AddedAtUtc??now,UpdatedAtUtc=now,LastLaunchedAtUtc=existing?.LastLaunchedAtUtc,TotalPlaytimeSeconds=existing?.TotalPlaytimeSeconds??0,DefaultLaunchTargetId=target.Id,CoverAsset=coverAsset,BackgroundAsset=existing?.BackgroundAsset,CardStyle=existing?.CardStyle??ApplicationCardStyle.Wide,PrimaryCategoryName=existing?.PrimaryCategoryName,DesktopControlEnabled=existing?.DesktopControlEnabled??false,LaunchTargets=targets };
        TextBlock review = new() { Text = $"{item.Title}\n{kind}\n{targetBox.Text}\nCover: {artworkPath ?? "Generated placeholder"}\nHide: {target.HideFrontendWhileRunning} · Restore: {target.RestoreFrontendAfterExit}", TextWrapping = TextWrapping.Wrap };
        ContentDialogResult save = await Dialog(existing is null ? "Add Item · 4 of 4" : "Review changes", review, "Save", "Save and Test", "Cancel").ShowAsync();
        if (save == ContentDialogResult.None) return;
        await ViewModel.AddOrUpdateAsync(item, existing is not null);
        if(existing is not null&&await IsProviderManagedAsync(existing.Id))
        {
            if(!string.Equals(existing.Title,item.Title,StringComparison.Ordinal))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"Title");
            if(!string.Equals(existing.Subtitle,item.Subtitle,StringComparison.Ordinal))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"Subtitle");
            if(!string.Equals(existing.Description,item.Description,StringComparison.Ordinal))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"Description");
            if(existing.ContentType!=item.ContentType)await _providerPersistence.AddMetadataOverrideAsync(item.Id,"ContentType");
            if(!string.Equals(existing.CoverAsset?.LocalPath,item.CoverAsset?.LocalPath,StringComparison.OrdinalIgnoreCase))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"Cover");
            if(existing.LaunchTarget!=item.LaunchTarget)await _providerPersistence.AddMetadataOverrideAsync(item.Id,"LaunchBehavior");
        }
        if (save == ContentDialogResult.Secondary) await ViewModel.LaunchAsync(new CardViewModel(item, "\uECAA"));
    }

    private async Task RelinkAsync(LibraryItem item)
    {
        string? path = await PickLaunchTargetAsync(); if (path is null || item.LaunchTarget is null) return;
        LaunchType type = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? LaunchType.WindowsShortcut : LaunchType.Executable;
        LaunchTarget updated = item.LaunchTarget with { LaunchType=type,ExecutablePath=type==LaunchType.Executable?Path.GetFullPath(path):null,ShortcutPath=type==LaunchType.WindowsShortcut?Path.GetFullPath(path):null,WorkingDirectory=Path.GetDirectoryName(path),UserManaged=true,UpdatedAtUtc=DateTime.UtcNow };
        LibraryItem relinked=item with { LaunchTargets=item.LaunchTargets.Select(t=>t.Id==updated.Id?updated:t).ToArray(),UpdatedAtUtc=DateTime.UtcNow };
        await ViewModel.AddOrUpdateAsync(relinked,true);if(await IsProviderManagedAsync(item.Id))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"LaunchBehavior");await ShowDetailsAsync(new CardViewModel(relinked,"\uECAA"));
    }

    private async Task ConfirmRemoveAsync(CardViewModel card)
    {
        ContentDialogResult result=await Dialog("Remove item?",new TextBlock{Text=$"Remove {card.Title} and its NexaStation history? Original programs and artwork files will not be deleted.",TextWrapping=TextWrapping.Wrap},"Remove",null,"Cancel").ShowAsync();
        if(result==ContentDialogResult.Primary)await ViewModel.DeleteAsync(card);
    }

    private ContentDialog Dialog(string title,object content,string primary,string? secondary,string close)=>new(){Title=title,Content=content,PrimaryButtonText=primary,SecondaryButtonText=secondary??string.Empty,CloseButtonText=close,DefaultButton=ContentDialogButton.Primary,XamlRoot=Root.XamlRoot};
    private async Task<string?> PickLaunchTargetAsync(){FileOpenPicker picker=new();picker.FileTypeFilter.Add(".exe");picker.FileTypeFilter.Add(".lnk");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return (await picker.PickSingleFileAsync())?.Path;}
    private async Task<string?> PickArtworkAsync(){FileOpenPicker picker=new();foreach(string extension in new[]{".png",".jpg",".jpeg",".webp"})picker.FileTypeFilter.Add(extension);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return (await picker.PickSingleFileAsync())?.Path;}
    private async Task<string?> PickGameFileAsync(){FileOpenPicker picker=new();foreach(string extension in new[]{".iso",".gcm",".wbfs",".ciso",".gcz",".wia",".rvz",".dol",".elf",".bin",".img",".mdf",".chd",".cso",".pbp",".nes",".sfc",".smc",".gb",".gbc",".gba",".n64",".z64",".v64",".gen",".md",".sms",".cue",".self"})picker.FileTypeFilter.Add(extension);WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return(await picker.PickSingleFileAsync())?.Path;}
    private async Task<bool> IsProviderManagedAsync(string itemId)=>(await _providerPersistence.GetSourcesAsync("steam")).Any(source=>source.LibraryItemId==itemId);
    private static string SuggestTitle(string path){if(string.IsNullOrWhiteSpace(path))return string.Empty;try{FileVersionInfo info=FileVersionInfo.GetVersionInfo(path);return string.IsNullOrWhiteSpace(info.ProductName)?Path.GetFileNameWithoutExtension(path):info.ProductName;}catch(Exception ex)when(ex is FileNotFoundException or UnauthorizedAccessException or System.Security.SecurityException){return Path.GetFileNameWithoutExtension(path);}}

    private void CardList_GotFocus(object sender, RoutedEventArgs e)
    {
        if (ContextTargetFrom(e.OriginalSource as DependencyObject) is CardViewModel card)
        {
            int rowIndex = FindRowIndex(card);
            ViewModel.SelectItem(card, rowIndex);
            _ = ViewModel.PreviewFocusedArtworkAsync(card);
        }
    }

    private async Task OpenGalleryCardAsync(GalleryCardViewModel card)
    {
        _galleryFocusId=card.Id;if(card.Item.MediaType==GalleryMediaType.Video){await OpenNativeVideoAsync(card.Item);return;}_viewerItems=await GalleryViewModel.CurrentItemsAsync();await _galleryViewer.OpenAsync(_viewerItems,card.Id,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));GalleryViewerOverlay.Visibility=Visibility.Visible;GalleryRoot.Visibility=Visibility.Collapsed;DashboardContent.Visibility=Visibility.Collapsed;_inputContext.SetCurrent(InputContext.GalleryViewer);GalleryViewerOverlay.Focus(FocusState.Programmatic);await LoadGalleryViewerAsync();
    }
    private async Task OpenNativeVideoAsync(GalleryMediaItem item)
    {
        _activeVideo=item;_galleryFocusId=item.Id;IReadOnlyList<GalleryMediaItem> current=await GalleryViewModel.CurrentItemsAsync();MediaQueueItem[] queue=current.Where(value=>value.MediaType==GalleryMediaType.Video&&value.SourcePrivacyLevel==item.SourcePrivacyLevel).Select(value=>new MediaQueueItem(value.Id,value.FilePath,value.FileName,value.SourcePrivacyLevel==GalleryPrivacyLevel.Private?MediaPrivacyLevel.Private:MediaPrivacyLevel.Normal)).ToArray();
        NativePlayerOverlay.Visibility=Visibility.Visible;NativePlayerOverlay.IsHitTestVisible=true;_inputContext.SetCurrent(InputContext.MediaPlayer);MediaOpenResult opened=await NativePlayerPage.OpenAsync(new(){MediaItemId=item.Id,LocalPath=item.FilePath,DisplayTitle=item.FileName,SourceType=item.SourceKind==GallerySourceKind.Recordings?MediaSourceType.Recording:MediaSourceType.GalleryVideo,PrivacyLevel=item.SourcePrivacyLevel==GalleryPrivacyLevel.Private?MediaPrivacyLevel.Private:MediaPrivacyLevel.Normal,ResumePolicy=MediaResumePolicy.Ask,Queue=queue,ReturnFocusOrigin=new("MainWindow","Content",item.Id,"Video"),ActivityTargetId=item.Id});
        if(opened.Succeeded){await _galleryPersistence.MarkOpenedAsync(item.Id,DateTime.UtcNow);if(opened.ResumePosition is { } resume){ContentDialogResult choice=await Dialog("Resume video",new TextBlock{Text=$"Resume from {resume:mm\\:ss}?",TextWrapping=TextWrapping.Wrap},"Resume","Start from Beginning","Cancel").ShowAsync();if(choice==ContentDialogResult.Primary)await _nativeMediaPlayer.SeekAsync(resume);else if(choice==ContentDialogResult.None){await CloseNativePlayerAsync();return;}}}
    }
    private async void OnNativePlayerCloseRequested(object? sender,EventArgs e)=>await CloseNativePlayerAsync();
    private async Task CloseNativePlayerAsync()
    {
        await _nativeMediaPlayer.CloseAsync(MediaCloseReason.UserBack);NativePlayerOverlay.Visibility=Visibility.Collapsed;NativePlayerOverlay.IsHitTestVisible=false;_inputContext.SetCurrent(InputContext.Dashboard);string? restore=_activeVideo?.Id??_galleryFocusId;_activeVideo=null;await GalleryViewModel.RefreshAsync();DispatcherQueue.TryEnqueue(()=>FocusGalleryCard(restore));
    }
    private async void OnNativePlayerOpenWithRequested(object? sender,EventArgs e){if(_activeVideo is not null)await ShowOpenWithAsync(_activeVideo);}
    private async Task ShowOpenWithAsync(GalleryMediaItem item)
    {
        IReadOnlyList<ExternalMediaPlayerDescriptor> players=await _externalMediaPlayers.DiscoverAsync();ComboBox choices=new(){Header="Player",ItemsSource=players,DisplayMemberPath="DisplayName",SelectedIndex=players.Count>0?0:-1,MinWidth=360};StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text="Choose an external player for this launch. NexaStation does not change Windows file associations.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(choices);ContentDialogResult result=await Dialog("Open With",panel,players.Count>0?"Use Once":"Windows Default",null,"Cancel").ShowAsync();if(result!=ContentDialogResult.Primary)return;if(choices.SelectedItem is ExternalMediaPlayerDescriptor player){ExternalPlayerLaunchResult launched=await _externalMediaPlayers.OpenAsync(new(player,item.FilePath,item.Id));if(!launched.Succeeded)ViewModel.ReportStatus(launched.Error??"The external player did not start.");}else await OpenExternalMediaAsync(item);
    }
    private async void OnNativePlayerQueueMoveRequested(object? sender,int direction)
    {
        if(_activeVideo is null)return;IReadOnlyList<GalleryMediaItem> items=await GalleryViewModel.CurrentItemsAsync();GalleryMediaItem[] videos=items.Where(value=>value.MediaType==GalleryMediaType.Video&&value.SourcePrivacyLevel==_activeVideo.SourcePrivacyLevel).ToArray();int index=Array.FindIndex(videos,value=>value.Id==_activeVideo.Id);int next=index+Math.Sign(direction);if(next>=0&&next<videos.Length)await OpenNativeVideoAsync(videos[next]);
    }
    private async Task<bool> OpenExternalMediaAsync(GalleryMediaItem item)
    {
        GalleryMediaLaunchResult result=await _galleryLauncher.OpenAsync(new(item,true,AllowDefaultFallback:false));if(result.Succeeded)return true;
        if(result.UsedPreferredApplication&&await Dialog("Preferred player unavailable",new TextBlock{Text=(result.Error??"The preferred player could not open this item.")+"\n\nUse the Windows default association instead?",TextWrapping=TextWrapping.Wrap},"Use Windows Default",null,"Cancel").ShowAsync()==ContentDialogResult.Primary)result=await _galleryLauncher.OpenAsync(new(item,true,AllowDefaultFallback:true));
        if(!result.Succeeded)ViewModel.ReportStatus(result.Error??"The media could not be opened.");return result.Succeeded;
    }
    private async Task ConfigureMediaAssociationAsync(GalleryMediaItem item)
    {
        LibraryItem[] players=(await _library.QueryAsync(new LibraryQuery(IncludeHidden:false,MaximumResults:1000))).Where(x=>(x.ContentType is ContentType.Application or ContentType.Media)&&x.LaunchTarget is not null).OrderBy(x=>x.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();if(players.Length==0){ViewModel.ReportStatus("Import a media player application before creating a preferred-player rule.");return;}
        ComboBox scope=new(){Header="Apply preference to",ItemsSource=new[]{"This item","This Gallery source","This file extension","All media of this type"},SelectedIndex=0,MinWidth=380};ComboBox player=new(){Header="Player application",ItemsSource=players,DisplayMemberPath="Title",SelectedIndex=0,MinWidth=380};TextBox arguments=new(){Header="Arguments template",Text="{MEDIA_PATH}",MinWidth=380};TextBlock preview=new(){Text=$"Preview: {players[0].Title} {item.FilePath}",TextWrapping=TextWrapping.Wrap};player.SelectionChanged+=(_,_)=>{if(player.SelectedItem is LibraryItem selected)preview.Text=$"Preview: {selected.Title} {arguments.Text.Replace("{MEDIA_PATH}",item.FilePath,StringComparison.Ordinal)}";};arguments.TextChanged+=(_,_)=>{if(player.SelectedItem is LibraryItem selected)preview.Text=$"Preview: {selected.Title} {arguments.Text.Replace("{MEDIA_PATH}",item.FilePath,StringComparison.Ordinal)}";};StackPanel panel=new(){Spacing=9};panel.Children.Add(scope);panel.Children.Add(player);panel.Children.Add(arguments);panel.Children.Add(preview);panel.Children.Add(new TextBlock{Text="Only {MEDIA_PATH} is supported. Shell chaining and command interpreters are rejected.",TextWrapping=TextWrapping.Wrap,Opacity=.75});if(await Dialog("Preferred media player",panel,"Save Preference",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary||player.SelectedItem is not LibraryItem selectedPlayer||selectedPlayer.LaunchTarget is not LaunchTarget target)return;
        MediaAssociationScope selectedScope=scope.SelectedIndex switch{1=>MediaAssociationScope.GallerySource,2=>MediaAssociationScope.FileExtension,3=>MediaAssociationScope.GlobalMediaType,_=>MediaAssociationScope.MediaItem};string? scopeValue=selectedScope switch{MediaAssociationScope.MediaItem=>item.Id,MediaAssociationScope.GallerySource=>item.GallerySourceId,MediaAssociationScope.FileExtension=>item.Extension,_=>null};DateTime now=DateTime.UtcNow;string id=$"{selectedScope}:{scopeValue??item.MediaType.ToString()}";try{await _mediaAssociations.SaveAsync(new(id,selectedScope,scopeValue,selectedScope==MediaAssociationScope.GlobalMediaType?item.MediaType:null,selectedScope==MediaAssociationScope.FileExtension?item.Extension:null,selectedPlayer.Id,target.Id,arguments.Text,target.TrackingMode,target.HideFrontendWhileRunning,target.RestoreFrontendAfterExit,now,now));ViewModel.ReportStatus($"Preferred player saved for {scope.SelectedItem}.");}catch(ArgumentException ex){ViewModel.ReportStatus(ex.Message);}
    }
    private async Task LoadGalleryViewerAsync()
    {
        GalleryMediaItem? item=_galleryViewer.Current;if(item is null)return;_viewerLoad?.Cancel();_viewerLoad?.Dispose();_viewerLoad=new();CancellationToken token=_viewerLoad.Token;GalleryViewerLoading.IsActive=true;GalleryViewerCaption.Text=item.FileName;
        try{StorageFile file=await StorageFile.GetFileFromPathAsync(item.FilePath).AsTask(token);using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream=await file.OpenReadAsync().AsTask(token);BitmapImage image=new(){DecodePixelWidth=(int)Math.Clamp(ViewerSurface.ActualWidth<=0?1920:ViewerSurface.ActualWidth,640,3840)};await image.SetSourceAsync(stream).AsTask(token);if(!token.IsCancellationRequested)GalleryViewerImage.Source=image;ApplyGalleryViewerTransform();}
        catch(OperationCanceledException){}catch(Exception exception){ViewModel.ReportStatus($"The full-resolution image could not be loaded: {exception.Message}");}
        finally{if(!token.IsCancellationRequested)GalleryViewerLoading.IsActive=false;}
    }
    private void ApplyGalleryViewerTransform(){GalleryViewport? viewport=_galleryViewer.Viewport;GalleryMediaItem? item=_galleryViewer.Current;if(viewport is null||item?.Width is not >0||item.Height is not >0)return;double fit=Math.Min(Math.Max(1,ViewerSurface.ActualWidth)/item.Width.Value,Math.Max(1,ViewerSurface.ActualHeight)/item.Height.Value);double relative=viewport.Scale/fit;GalleryViewerTransform.ScaleX=relative;GalleryViewerTransform.ScaleY=relative;GalleryViewerTransform.TranslateX=viewport.PanX;GalleryViewerTransform.TranslateY=viewport.PanY;}
    private async void GalleryViewerBack_Click(object sender,RoutedEventArgs e)=>await CloseGalleryViewerAsync();
    private async Task CloseGalleryViewerAsync(){await _slideshow.StopAsync(GallerySlideshowStopReason.UserReturned);_viewerLoad?.Cancel();GalleryViewerImage.Source=null;GalleryViewerOverlay.Visibility=Visibility.Collapsed;GalleryRoot.Visibility=Visibility.Visible;_inputContext.SetCurrent(InputContext.Dashboard);await GalleryViewModel.RefreshAsync();UpdateGalleryEmptyState();DispatcherQueue.TryEnqueue(()=>FocusGalleryCard(_galleryFocusId));}
    private async void GalleryViewerPrevious_Click(object sender,RoutedEventArgs e){await _galleryViewer.MoveAsync(-1,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));await LoadGalleryViewerAsync();}
    private async void GalleryViewerNext_Click(object sender,RoutedEventArgs e){await _galleryViewer.MoveAsync(1,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));await LoadGalleryViewerAsync();}
    private void GalleryViewerZoomOut_Click(object sender,RoutedEventArgs e){_galleryViewer.Zoom(0.8);ApplyGalleryViewerTransform();}
    private void GalleryViewerZoomIn_Click(object sender,RoutedEventArgs e){_galleryViewer.Zoom(1.25);ApplyGalleryViewerTransform();}
    private void GalleryViewerFit_Click(object sender,RoutedEventArgs e){_galleryViewer.ResetZoom(GalleryZoomMode.Fit);ApplyGalleryViewerTransform();}
    private void GalleryViewerActual_Click(object sender,RoutedEventArgs e){_galleryViewer.ResetZoom(GalleryZoomMode.ActualSize);ApplyGalleryViewerTransform();}
    private async void GalleryViewerFavorite_Click(object sender,RoutedEventArgs e){await _galleryViewer.ToggleFavoriteAsync();GalleryViewerCaption.Text=(_galleryViewer.Current?.Favorite==true?"★ ":string.Empty)+_galleryViewer.Current?.FileName;}
    private async void GalleryViewerDetails_Click(object sender,RoutedEventArgs e){if(_galleryViewer.Current is { } item)await ShowGalleryDetailsAsync(new GalleryCardViewModel(item,_galleryThumbnails));}
    private async void GalleryViewerBackground_Click(object sender,RoutedEventArgs e){if(_galleryViewer.Current is not { } item)return;try{await _galleryBackgrounds.SetAsync(item,GalleryZoomMode.Fill);ViewModel.ReportStatus("NexaStation background updated from a managed Gallery cache copy.");}catch(Exception exception){ViewModel.ReportStatus($"Background could not be set: {exception.Message}");}}
    private async void GalleryViewerExternal_Click(object sender,RoutedEventArgs e){if(_galleryViewer.Current is { } item)await OpenExternalMediaAsync(item);}
    private void GalleryViewerLocation_Click(object sender,RoutedEventArgs e){if(_galleryViewer.Current is not { } item||!File.Exists(item.FilePath))return;ProcessStartInfo info=new("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add("/select,"+item.FilePath);Process.Start(info);}
    private async void GalleryViewerSlideshow_Click(object sender,RoutedEventArgs e){GalleryMediaItem? current=_galleryViewer.Current;if(current is null)return;GallerySource? source=await _galleryPersistence.GetSourceAsync(current.GallerySourceId);if(source?.AllowSlideshow==false){ViewModel.ReportStatus("Slideshow is disabled for this Gallery source.");return;}await _slideshow.StartAsync(new(_viewerItems,current.Id,5,Loop:true,Transition:GallerySlideshowTransition.Fade));}
    private void OnSlideshowItemChanged(object? sender,GalleryMediaItem item){DispatcherQueue.TryEnqueue(async()=>{if(GalleryViewerOverlay.Visibility!=Visibility.Visible)return;await _galleryViewer.OpenAsync(_viewerItems,item.Id,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));await LoadGalleryViewerAsync();});}
    private async void GalleryViewer_KeyDown(object sender,KeyRoutedEventArgs e){if(e.Key is VirtualKey.Escape or VirtualKey.Back){e.Handled=true;await CloseGalleryViewerAsync();}else if(e.Key==VirtualKey.Left){e.Handled=true;GalleryViewerPrevious_Click(sender,new RoutedEventArgs());}else if(e.Key==VirtualKey.Right){e.Handled=true;GalleryViewerNext_Click(sender,new RoutedEventArgs());}else if(e.Key is VirtualKey.Add or VirtualKey.GamepadRightTrigger){e.Handled=true;GalleryViewerZoomIn_Click(sender,new RoutedEventArgs());}else if(e.Key is VirtualKey.Subtract or VirtualKey.GamepadLeftTrigger){e.Handled=true;GalleryViewerZoomOut_Click(sender,new RoutedEventArgs());}else if(e.Key==VirtualKey.Number0){e.Handled=true;GalleryViewerFit_Click(sender,new RoutedEventArgs());}else if(e.Key==VirtualKey.F){e.Handled=true;GalleryViewerFavorite_Click(sender,new RoutedEventArgs());}else if(e.Key==VirtualKey.I){e.Handled=true;GalleryViewerDetails_Click(sender,new RoutedEventArgs());}else if(e.Key==VirtualKey.S){e.Handled=true;GalleryViewerSlideshow_Click(sender,new RoutedEventArgs());}}

    private async Task ShowQuickMenuSettingsAsync()
    {
        ToggleSwitch enabled=new(){Header="Quick Menu enabled",IsOn=await _settings.GetAsync("QuickMenu.Enabled",true)};ToggleSwitch showVolume=new(){Header="Show master volume",IsOn=await _settings.GetAsync("QuickMenu.Audio.ShowVolume",true)};ToggleSwitch showController=new(){Header="Show controller status",IsOn=await _settings.GetAsync("QuickMenu.ShowControllerStatus",true)};ToggleSwitch showNetwork=new(){Header="Show network status",IsOn=await _settings.GetAsync("QuickMenu.ShowNetworkStatus",true)};ToggleSwitch showClock=new(){Header="Show local clock",IsOn=await _settings.GetAsync("QuickMenu.ShowClock",true)};
        NumberBox recent=new(){Header="Recently Opened limit",Minimum=1,Maximum=100,Value=await _settings.GetAsync("Media.RecentLimit",30),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};NumberBox fineStep=new(){Header="Fine volume step",Minimum=.01,Maximum=.1,SmallChange=.01,Value=await _settings.GetAsync("QuickMenu.Audio.FineVolumeStep",.01),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};NumberBox hold=new(){Header="Restart / shutdown hold (milliseconds)",Minimum=1000,Maximum=5000,SmallChange=100,Value=await _settings.GetAsync("QuickMenu.Power.HoldDurationMilliseconds",1500),SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline};
        AudioOutputSnapshot outputs=await _audioOutputs.GetSnapshotAsync();SystemVolumeState audio=await _volume.GetStateAsync();SystemActionAvailability sleep=await _power.GetAvailabilityAsync(SystemPowerAction.Sleep);QuickMenuState state=_quickMenu.State;string diagnostics=$"Audio: {(audio.Available?audio.EndpointName:"Unavailable")} · endpoints {outputs.Devices.Count} · callback registered on first endpoint access\nControllers: {state.ControllerStatus.Count} · Windows.Gaming.Input · battery optional\nNetwork: {state.NetworkStatus.InternetAccessLevel} · metered {state.NetworkStatus.Metered?.ToString()??"Unknown"}\nMedia: {MediaViewModel.Rows.Count} rows · recent limit {recent.Value:F0}\nQuick Menu: {(state.IsOpen?"Open":"Closed")} · cached asynchronous refresh\nPower: Sleep {(sleep.Available?"available":"unavailable")} · last actions contain no credentials";
        StackPanel content=new(){Spacing=10,MaxWidth=560};content.Children.Add(new TextBlock{Text="Media",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});content.Children.Add(recent);content.Children.Add(new TextBlock{Text="Preferred players are configured per media type, extension, Gallery source, or item from Gallery launch settings.",TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text="Audio",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});content.Children.Add(showVolume);content.Children.Add(fineStep);content.Children.Add(new TextBlock{Text="Default-device switching uses Windows Sound Settings because Windows exposes no supported public switching API.",TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text="Quick Menu",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});content.Children.Add(enabled);content.Children.Add(showController);content.Children.Add(showNetwork);content.Children.Add(showClock);content.Children.Add(new TextBlock{Text="Power",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});content.Children.Add(hold);content.Children.Add(new TextBlock{Text="Restart and shutdown confirmation cannot be disabled.",TextWrapping=TextWrapping.Wrap});content.Children.Add(new TextBlock{Text="Diagnostics",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});content.Children.Add(new TextBlock{Text=diagnostics,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true});
        if(await Dialog("Media, Audio, Quick Menu, Power and Diagnostics",new ScrollViewer{Content=content,MaxHeight=650},"Save",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        await _settings.SetAsync("QuickMenu.Enabled",enabled.IsOn);await _settings.SetAsync("QuickMenu.Audio.ShowVolume",showVolume.IsOn);await _settings.SetAsync("QuickMenu.ShowControllerStatus",showController.IsOn);await _settings.SetAsync("QuickMenu.ShowNetworkStatus",showNetwork.IsOn);await _settings.SetAsync("QuickMenu.ShowClock",showClock.IsOn);await _settings.SetAsync("Media.RecentLimit",(int)recent.Value);await _settings.SetAsync("QuickMenu.Audio.FineVolumeStep",fineStep.Value);await _settings.SetAsync("QuickMenu.Power.HoldDurationMilliseconds",(int)hold.Value);ViewModel.ReportStatus("Phase 6 settings saved. Restart NexaStation to apply structural Quick Menu changes.");
    }

    private async Task ShowStartupRecoverySettingsAsync()
    {
        StartupTaskStatus startupStatus=await _startupTask.GetStatusAsync();
        CheckBox startup=new(){Content="Start NexaStation after Windows sign-in",IsChecked=startupStatus.Availability==StartupTaskAvailability.Enabled,IsEnabled=startupStatus.Availability!=StartupTaskAvailability.DisabledByPolicy};
        CheckBox includeLogs=new(){Content="Include redacted logs in diagnostics I explicitly export",IsChecked=await _settings.GetAsync("Privacy.DiagnosticsExportIncludesLogs",true)};
        TextBlock status=new(){Text=startupStatus.Message,TextWrapping=TextWrapping.Wrap,Foreground=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NexaMutedTextBrush"]};
        Button backup=new(){Content="Create validated library backup"};
        Button diagnostics=new(){Content="Export redacted diagnostics"};
        Button openBackups=new(){Content="Open backups folder"};
        Button setupAssistant=new(){Content="Run Setup Assistant"};
        StackPanel actions=new(){Orientation=Orientation.Horizontal,Spacing=8};actions.Children.Add(backup);actions.Children.Add(diagnostics);actions.Children.Add(openBackups);
        StackPanel panel=new(){Spacing=12,MinWidth=640};panel.Children.Add(startup);panel.Children.Add(status);panel.Children.Add(includeLogs);panel.Children.Add(setupAssistant);panel.Children.Add(new TextBlock{Text=$"Local data: {_paths.Root}\nDatabase: {_paths.Database}",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true,Opacity=.75});panel.Children.Add(actions);
        backup.Click+=async(_,_)=>{try{BackupResult result=await _databaseBackups.CreateAsync(_paths.Database,"manual-settings");status.Text=$"Validated backup created: {result.BackupPath}";}catch(Exception exception){status.Text=$"Backup failed: {exception.Message}";}};
        diagnostics.Click+=async(_,_)=>{try{string exported=await _diagnosticsExport.ExportAsync(_releaseInfo.InformationalVersion,_releaseInfo.Channel.ToString(),new(includeLogs.IsChecked==true,true));status.Text=$"Diagnostics exported: {exported}";}catch(Exception exception){status.Text=$"Diagnostics export failed: {exception.Message}";}};
        openBackups.Click+=(_,_)=>{_paths.EnsureCreated();Process.Start(new ProcessStartInfo(_paths.Backups){UseShellExecute=true});};
        setupAssistant.Click+=async(_,_)=>{await ShowSetupAssistantAsync();};
        if(await Dialog("Startup, Recovery and Diagnostics",panel,"Save",null,"Close").ShowAsync()!=ContentDialogResult.Primary)return;
        StartupTaskStatus updated=await _startupTask.SetEnabledAsync(startup.IsChecked==true);
        await _settings.SetAsync("Startup.Enabled",updated.Availability==StartupTaskAvailability.Enabled);
        await _settings.SetAsync("Privacy.DiagnosticsExportIncludesLogs",includeLogs.IsChecked==true);
        ViewModel.ReportStatus(updated.Message);
    }

    private async Task ShowSetupAssistantAsync()
    {
        if (_setupAssistant is not null) { _setupAssistant.Activate(); return; }
        FrontendProfile owner = await _frontendProfiles.GetOwnerAsync();
        OnboardingState initial = OnboardingState.Create(false, DateTimeOffset.UtcNow) with
        {
            Status = OnboardingStatus.InProgress,
            Preferences = OnboardingState.Create(false, DateTimeOffset.UtcNow).Preferences with
            {
                OwnerName = owner.Name,
                AvatarKey = owner.AvatarKey ?? "owner",
                Layout = _layouts.CurrentLayoutId switch { "classic-crossbar" => "Classic", "compact-handheld" => "Compact", _ => "Modern" },
                ThemeId = _themes.CurrentThemeId,
            },
        };
        OnboardingFlowService flow = new(new TransientOnboardingStateStore(initial));
        TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _setupAssistant = new(flow, _onboardingCoordinator, initial, _inputService, _inputGlyphs);
        _setupAssistant.SetupFinished += (_, _) => completion.TrySetResult(true);
        _setupAssistant.Closed += (_, _) => completion.TrySetResult(false);
        _setupAssistant.Activate();
        _setupAssistant.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        bool applied = await completion.Task;
        _setupAssistant = null;
        if (!applied) { ViewModel.ReportStatus("Setup Assistant closed. Its draft was discarded."); return; }
        FrontendProfile updated = await _frontendProfiles.GetOwnerAsync();
        _localUserName = updated.Name; _ownerAvatarKey = updated.AvatarKey ?? "owner";
        ApplyGuestPresentation();
        await ViewModel.RefreshAsync();
        ViewModel.ReportStatus("Setup Assistant choices applied.");
    }

    private async Task ShowCompatibilityFeedbackAsync()
    {
        IReadOnlyList<CompatibilityProfile> profiles=await _compatibilityProfiles.GetProfilesAsync();
        IReadOnlyList<CompatibilityReport> reports=await _compatibilityReports.GetRecentAsync(500);
        CheckBox audit=new(){Content="Enable semantic controller-navigation audit",IsChecked=_navigationAudit.Enabled};
        Button exportAudit=new(){Content="Export sanitized navigation report",MinWidth=260};
        exportAudit.Click+=async(_,_)=>{string path=await _navigationAudit.ExportAsync();ViewModel.ReportStatus($"Navigation report saved locally: {path}");};
        Button deleteImported=new(){Content="Delete imported feed profiles",MinWidth=260,IsEnabled=profiles.Any(profile=>profile.Source==CompatibilityProfileSource.ImportedSignedFeed)};
        deleteImported.Click+=async(_,_)=>{await _compatibilityProfiles.DeleteImportedFeedAsync();ViewModel.ReportStatus("Imported compatibility profiles were deleted. User profiles were preserved.");};
        StackPanel panel=new(){Spacing=10,MaxWidth=620};
        panel.Children.Add(new TextBlock{Text="Compatibility",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text=$"Profiles: {profiles.Count}\nUser overrides: {profiles.Count(profile=>profile.Source==CompatibilityProfileSource.User)}\nImported signed-feed profiles: {profiles.Count(profile=>profile.Source==CompatibilityProfileSource.ImportedSignedFeed)}\nLocal compatibility reports: {reports.Count}",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text=_compatibilityFeedSettings.Enabled?"Signed compatibility feed checks are enabled.":"Signed compatibility feeds are disabled. No signing key or trusted feed URL is configured; only built-in and local user profiles can apply.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        panel.Children.Add(audit);panel.Children.Add(exportAudit);panel.Children.Add(deleteImported);
        panel.Children.Add(new TextBlock{Text="Profiles are declarative. Scripts, commands, Registry changes, file deletion/moving, process killing, driver installation, and executable downloads are rejected.",TextWrapping=TextWrapping.Wrap,Opacity=.75});
        if(await Dialog("Settings · Compatibility and Alpha Feedback",panel,"Save",null,"Close").ShowAsync()==ContentDialogResult.Primary){_navigationAudit.Enabled=audit.IsChecked==true;await _settings.SetAsync("Quality.NavigationAuditEnabled",_navigationAudit.Enabled);ViewModel.ReportStatus("Compatibility diagnostics settings saved.");}
    }

    private async Task ShowReportProblemAsync(CompatibilityReport? compatibilityReport=null,CompatibilityProfile? activeProfile=null)
    {
        ComboBox category=new(){Header="Problem category",ItemsSource=new[]{"Startup","Packaging","Updates","Database","Steam","Windows Apps","Emulation","Gallery","Media","Controller","Navigation","Display","Audio","Network","Artwork","Accessibility","Performance","Recovery","Security"},SelectedIndex=0,MinWidth=320};
        TextBox expected=new(){Header="Expected behavior",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=70};
        TextBox actual=new(){Header="Actual behavior",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=70};
        TextBox steps=new(){Header="Reproduction steps",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=110};
        TextBox safeMode=new(){Header="Safe Mode result",Text="Not tested"};
        CheckBox bundle=new(){Content="Create a local redacted reproduction bundle",IsChecked=true};
        CheckBox exactConsent=new(){Content="Include an exact executable path after explicit review",IsChecked=false};
        TextBox exactPath=new(){Header="Optional exact executable path",IsEnabled=false};exactConsent.Checked+=(_,_)=>exactPath.IsEnabled=true;exactConsent.Unchecked+=(_,_)=>exactPath.IsEnabled=false;
        StackPanel panel=new(){Spacing=8,MaxWidth=650};panel.Children.Add(new TextBlock{Text="Nothing is uploaded or submitted automatically. Preview the copied template and ZIP before attaching anything to GitHub.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(category);panel.Children.Add(expected);panel.Children.Add(actual);panel.Children.Add(steps);panel.Children.Add(safeMode);panel.Children.Add(bundle);panel.Children.Add(exactConsent);panel.Children.Add(exactPath);
        if(await Dialog("Help · Report a Problem",new ScrollViewer{Content=panel,MaxHeight=650},"Prepare Report",null,"Cancel").ShowAsync()!=ContentDialogResult.Primary)return;
        if(string.IsNullOrWhiteSpace(steps.Text)){ViewModel.ReportStatus("Reproduction steps are required before preparing a report.");return;}
        ReproductionBundleResult? bundleResult=null;
        if(bundle.IsChecked==true)
        {
            IReadOnlyList<ControllerStatus> controllers=await _controllerStatus.GetControllersAsync();
            bundleResult=await _reproductionBundles.CreateAsync(new(){Category=(string)category.SelectedItem,ReproductionSteps=steps.Text,ExpectedBehavior=expected.Text,ActualBehavior=actual.Text,CompatibilityReport=compatibilityReport,ActiveProfile=activeProfile,ControllerFamily=controllers.Count>0?controllers[0].ControllerFamily:null,DisplaySummary=$"Window {Root.ActualWidth:0}x{Root.ActualHeight:0}",ExactPathConsent=exactConsent.IsChecked==true,ExactExecutablePath=exactConsent.IsChecked==true?exactPath.Text:null});
        }
        GitHubIssuePreparationResult prepared=_issuePreparation.Prepare(new(_releaseInfo.Version.ToString(),_releaseInfo.Channel.ToString(),Environment.OSVersion.VersionString,(string)category.SelectedItem,expected.Text,actual.Text,steps.Text,safeMode.Text,compatibilityReport?.DiagnosticCorrelationId,bundleResult?.Succeeded==true));
        DataPackage data=new();data.SetText(prepared.Markdown);Clipboard.SetContent(data);Clipboard.Flush();
        if(await Dialog("Report Prepared",new TextBlock{Text=$"The redacted issue template was copied to the clipboard.\n\n{(bundleResult?.Succeeded==true?$"Bundle: {bundleResult.BundlePath}":"No bundle was created.")}\n\n{prepared.PrivacyReminder}\n\nThis repository has no configured issue URL, so the GitHub home page can be opened manually.",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true},"Open GitHub",null,"Close").ShowAsync()==ContentDialogResult.Primary)Process.Start(new ProcessStartInfo{FileName=prepared.IssuePage.AbsoluteUri,UseShellExecute=true});
    }

    private async Task ShowAboutAsync()
    {
        string releaseMetadata=$"Display version: {_releaseInfo.DisplayVersion} | Package: {_releaseInfo.PackageVersion} | Architecture: {_releaseInfo.Architecture} | Diagnostics: {_releaseInfo.DiagnosticsFormatVersion} | DB schema: {DatabaseInitializer.CurrentSchemaVersion}";
        string? alphaNotice=_releaseInfo.Channel==ReleaseChannel.Alpha?"Private Alpha build. Install only from the trusted tester package and certificate supplied by the NexaStation developer.":null;
        StackPanel panel=new(){Spacing=13,MinWidth=620};
        panel.Children.Add(new TextBlock{Text=releaseMetadata,TextWrapping=TextWrapping.Wrap,Opacity=0.78});
        if(alphaNotice is not null)panel.Children.Add(new TextBlock{Text=alphaNotice,TextWrapping=TextWrapping.Wrap,Foreground=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NexaAccentBrush"]});
        panel.Children.Add(new TextBlock{Text=$"NexaStation {_releaseInfo.Version} · {_releaseInfo.Channel}",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text="A private, local-first Windows frontend for games, applications, emulators, and media. NexaStation has no mandatory telemetry. Network access occurs only for user-requested provider, store, web, or update actions.",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text="Help",FontSize=19,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        InputGlyph confirmGlyph=_inputGlyphs.Resolve(InputAction.Confirm,InputGlyphContext.Dialog);InputGlyph cancelGlyph=_inputGlyphs.Resolve(InputAction.Cancel,InputGlyphContext.Dialog);InputGlyph quickGlyph=_inputGlyphs.Resolve(InputAction.QuickMenu,InputGlyphContext.Dialog);
        panel.Children.Add(new TextBlock{Text=$"Current prompts: {confirmGlyph.TextFallback} selects, {cancelGlyph.TextFallback} goes back, and {quickGlyph.TextFallback} opens Quick Menu. Directional navigation, keyboard, mouse, and touch remain supported.",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text="Privacy",FontSize=19,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text="Library metadata, preferences, artwork caches, gallery indexes, logs, backups, and crash reports stay under the local NexaStation data folder. Original gallery media is opened read-only and is never uploaded by NexaStation.",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock{Text="Licenses",FontSize=19,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text="Project license: NOT YET SELECTED. Public-alpha packaging and distribution remain blocked until the owner explicitly chooses a license. Third-party notices are documented separately and do not grant a license to NexaStation itself.",TextWrapping=TextWrapping.Wrap,Foreground=(Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NexaWarningBrush"]});
        await Dialog("About NexaStation",new ScrollViewer{Content=panel,MaxHeight=650},"Close",null,string.Empty).ShowAsync();
    }

    public async Task<bool> ShowPrivateAlphaNoticeIfNeededAsync()
    {
        if(_releaseInfo.Channel!=ReleaseChannel.Alpha)return true;
        string noticeKey=$"AlphaNotice.Accepted.{_releaseInfo.PackageVersion}";
        if(await _settings.GetAsync(noticeKey,false))return true;
        await EnsureDialogXamlRootReadyAsync();
        while(true)
        {
            StackPanel panel=new(){Spacing=10,MaxWidth=620};
            panel.Children.Add(new TextBlock{Text="NexaStation is currently in private alpha.",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});
            panel.Children.Add(new TextBlock{Text="Some features may be incomplete or incompatible with specific games, applications, controllers, or launchers.",TextWrapping=TextWrapping.Wrap});
            panel.Children.Add(new TextBlock{Text="NexaStation does not delete installed games, applications, ROMs, photos, or videos during normal library operations.",TextWrapping=TextWrapping.Wrap});
            panel.Children.Add(new TextBlock{Text=$"Version {_releaseInfo.DisplayVersion} | Package {_releaseInfo.PackageVersion} | {_releaseInfo.Architecture}",TextWrapping=TextWrapping.Wrap,Opacity=0.75});
            bool viewKnownIssues=false;
            Button knownIssues=new(){Content="View Known Issues",HorizontalAlignment=HorizontalAlignment.Left};
            panel.Children.Add(knownIssues);
            ContentDialog noticeDialog=Dialog("Private Alpha Notice",panel,"Continue","Create Initial Backup","Exit");
            knownIssues.Click+=(_,_)=>{viewKnownIssues=true;noticeDialog.Hide();};
            ContentDialogResult result=await noticeDialog.ShowAsync();
            if(viewKnownIssues)
            {
                await Dialog("Known Issues",new TextBlock{Text="Private Alpha hardware, controller, launcher, emulator, update, clean-machine install, and Windows App Certification Kit results are only complete when the tester release notes say they were actually performed. Report any startup, install, update, uninstall, controller, provider, or diagnostics problem with an exported diagnostics ZIP.",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();
                continue;
            }
            if(result==ContentDialogResult.Primary)
            {
                await _settings.SetAsync(noticeKey,true);
                return true;
            }
            if(result==ContentDialogResult.Secondary)
            {
                await CreateInitialAlphaBackupAsync();
                continue;
            }
            await _settings.SetAsync(noticeKey,false);
            return false;
        }
    }

    private async Task EnsureDialogXamlRootReadyAsync()
    {
        if(Root.XamlRoot is not null)return;
        TaskCompletionSource<bool> loaded=new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender,RoutedEventArgs e){Root.Loaded-=OnLoaded;loaded.TrySetResult(true);}
        Root.Loaded+=OnLoaded;
        await Task.WhenAny(loaded.Task,Task.Delay(TimeSpan.FromSeconds(5)));
        Root.Loaded-=OnLoaded;
        if(Root.XamlRoot is null)await Task.Yield();
        if(Root.XamlRoot is null)throw new InvalidOperationException("Main window content was not attached to a XamlRoot before showing startup dialogs.");
    }

    private async Task CreateInitialAlphaBackupAsync()
    {
        try
        {
            if(!File.Exists(_paths.Database))
            {
                ViewModel.ReportStatus("No local database exists yet. Complete onboarding, then create a backup from Settings.");
                return;
            }
            BackupResult backup=await _databaseBackups.CreateAsync(_paths.Database,$"private-alpha-initial-{_releaseInfo.PackageVersion}");
            ViewModel.ReportStatus($"Initial Alpha backup created: {backup.BackupPath}");
        }
        catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ViewModel.ReportStatus($"Initial backup could not be created: {exception.Message}");
        }
    }
    private void GalleryViewerImage_PointerWheelChanged(object sender,PointerRoutedEventArgs e){int delta=e.GetCurrentPoint(GalleryViewerImage).Properties.MouseWheelDelta;_galleryViewer.Zoom(delta>0?1.15:0.87);ApplyGalleryViewerTransform();e.Handled=true;}
    private void GalleryViewerImage_ManipulationDelta(object sender,ManipulationDeltaRoutedEventArgs e){_galleryViewer.Pan(e.Delta.Translation.X,e.Delta.Translation.Y);ApplyGalleryViewerTransform();}

    private async Task ShowGalleryDetailsAsync(GalleryCardViewModel card)
    {
        GalleryMediaItem item=card.Item;string width=item.Width?.ToString(CultureInfo.InvariantCulture)??"Unknown";string height=item.Height?.ToString(CultureInfo.InvariantCulture)??"Unknown";string duration=item.DurationMilliseconds is null?"Unknown":TimeSpan.FromMilliseconds(item.DurationMilliseconds.Value).ToString(null,CultureInfo.CurrentCulture);string captured=item.CapturedAtUtc?.ToLocalTime().ToString("g",CultureInfo.CurrentCulture)??"Unknown";string details=$"{item.FileName}\n{item.MediaType}\n{item.ParentFolder}\nSource: {item.SourceName}\nDimensions: {width} × {height}\nDuration: {duration}\nSize: {item.FileSize/1024d/1024d:F2} MB\nCaptured: {captured}\nModified: {item.ModifiedAtUtc.ToLocalTime().ToString("g",CultureInfo.CurrentCulture)}\nAvailable: {item.Available}\n\nPrecise GPS metadata is neither stored nor displayed.";ContentDialogResult result=await Dialog("Media Details",new TextBlock{Text=details,TextWrapping=TextWrapping.Wrap},item.Favorite?"Unfavorite":"Favorite","Add to Album","Close").ShowAsync();if(result==ContentDialogResult.Primary){await GalleryViewModel.ToggleFavoriteAsync(card);UpdateGalleryEmptyState();}else if(result==ContentDialogResult.Secondary){GalleryAlbum[] albums=(await _galleryPersistence.GetAlbumsAsync()).ToArray();if(albums.Length==0){await CreateGalleryAlbumAsync();albums=(await _galleryPersistence.GetAlbumsAsync()).ToArray();}ComboBox albumBox=new(){ItemsSource=albums,DisplayMemberPath="Name",SelectedIndex=albums.Length>0?0:-1,MinWidth=320};if(albums.Length>0&&await Dialog("Add to Album",albumBox,"Add",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&albumBox.SelectedItem is GalleryAlbum album){await _galleryAlbums.AddAsync(album.Id,item.Id);ViewModel.ReportStatus($"Added {item.FileName} to {album.Name}.");}}}

    private void Error_Close(InfoBar sender, object args) => ViewModel.CloseError();

    private void Status_Close(InfoBar sender, object args) => ViewModel.CloseStatus();

    private async void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        _activeInputMode.ReportInput(new(InputMode.Keyboard, IsButtonPress:true));
        if(_onScreenKeyboard.IsOpen)return;
        if(e.Key==(VirtualKey)0x5D || e.Key==VirtualKey.F10&&IsKeyDown(VirtualKey.Shift))
        {
            e.Handled=true;
            await OpenContextMenuForFocusedAsync();
            return;
        }
        if(e.Key==VirtualKey.R&&AreControlAndShiftDown())
        {
            e.Handled=true;await ShowSafeInterfaceResetAsync();return;
        }
        if (e.Key == VirtualKey.F11)
        {
            e.Handled = true;
            await _windowService.ToggleFullscreenAsync();
            return;
        }

        if (e.Key == VirtualKey.D && AreControlAndShiftDown())
        {
            e.Handled = true;
            await _windowService.ExitFullscreenForDevelopmentAsync();
            return;
        }
        if(e.Key==VirtualKey.I&&AreControlAndShiftDown())
        {
            e.Handled=true;await RestoreInputDefaultsAsync();return;
        }
        if(FocusManager.GetFocusedElement(Root.XamlRoot) is TextBox or PasswordBox or AutoSuggestBox)return;
        string code=e.Key==VirtualKey.Q&&IsKeyDown(VirtualKey.Control)?"CTRL+Q":e.Key.ToString().ToUpperInvariant();
        InputAction? action=_inputBindings.ResolveAction(new(PhysicalInputKind.KeyboardKey,code),_inputContext.Current);

        if (action is not null)
        {
            e.Handled = true;
            await HandleInputActionAsync(action.Value);
        }
    }

    private async void Root_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        object? target=ContextTargetFrom(e.OriginalSource as DependencyObject);
        if(target is null)return;
        e.Handled=true;
        await OpenContextMenuAsync(target,FindFocusableOrigin(e.OriginalSource as DependencyObject));
    }

    private async void Root_Holding(object sender,HoldingRoutedEventArgs e)
    {
        if(e.HoldingState!=HoldingState.Started)return;
        object? target=ContextTargetFrom(e.OriginalSource as DependencyObject);
        if(target is null)return;
        e.Handled=true;
        await OpenContextMenuAsync(target,FindFocusableOrigin(e.OriginalSource as DependencyObject));
    }

    private async Task OpenContextMenuForFocusedAsync()
    {
        DependencyObject? focused=FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        UpdateActionTarget(focused);
        object? target=_actionTargets.Resolve(InputAction.ContextMenu,new("MainWindow","Content",ViewModel.SelectedSection.Id)).Target?.DataContext??ContextTargetFrom(focused);
        if(target is null){ViewModel.ReportStatus("Focus an item to open its actions.");return;}
        await OpenContextMenuAsync(target,FindFocusableOrigin(focused));
    }

    private async Task OpenContextMenuAsync(object target,Control? origin)
    {
        ItemContextRequest? request=await BuildItemContextAsync(target);
        if(request is null)return;
        IReadOnlyList<ItemActionDescriptor> actions=await _itemContextMenus.BuildAsync(request);
        if(actions.Count==0){ViewModel.ReportStatus("No actions are available for this item.");return;}
        _contextMenuOrigin=origin??FocusManager.GetFocusedElement(Root.XamlRoot) as Control;
        NexaContextMenu menu=new(_inputGlyphs){XamlRoot=Root.XamlRoot};
        try
        {
            ItemActionDescriptor? selected=await menu.ShowForAsync(request.Title,actions);
            if(selected is not null)await ExecuteContextActionAsync(target,request,selected);
        }
        catch(Exception exception)
        {
            _logger.LogError("ContextMenuFailed","An item context menu could not be displayed.",exception,new Dictionary<string,object?>{{"TargetType",request.TargetType.ToString()},{"TargetId",request.TargetId}});
            ViewModel.ReportStatus("Item actions could not be opened. The application remains available.");
            await _sounds.PlayAsync(UiSoundEvent.Error);
        }
        finally
        {
            RestoreContextMenuFocus(request);
        }
    }

    private async Task<ItemContextRequest?> BuildItemContextAsync(object target)
    {
        if(target is CardViewModel card)
        {
            LibraryItem item=await _library.GetByIdAsync(card.Id)??card.Item;
            LaunchTarget? launch=item.LaunchTarget;
            bool emulated=launch?.EmulatedGameSourceId is not null;
            string? sourcePath=launch?.ExecutablePath??launch?.ShortcutPath??launch?.ExpectedExecutablePath;
            if(emulated&&launch?.EmulatedGameSourceId is { } emulatedId)sourcePath=(await _emulation.GetEmulatedGameAsync(emulatedId))?.GamePath;
            HashSet<string> capabilities=new(StringComparer.Ordinal)
            {
                ItemActionCapabilities.Details,ItemActionCapabilities.Edit,ItemActionCapabilities.Collections,
            };
            if(launch is not null)capabilities.Add(ItemActionCapabilities.Launch);
            if(item.ContentType==ContentType.Game)capabilities.Add(ItemActionCapabilities.Artwork);
            if(item.ContentType==ContentType.Game)capabilities.Add(ItemActionCapabilities.LifecycleStatus);
            if(emulated)capabilities.Add(ItemActionCapabilities.EmulatorConfiguration);
            if(!string.IsNullOrWhiteSpace(sourcePath))capabilities.Add(ItemActionCapabilities.OpenLocation);
            if(item.ContentType==ContentType.Application||!string.IsNullOrWhiteSpace(launch?.ProviderId))capabilities.Add(ItemActionCapabilities.ManageInstallation);
            if(launch is not null)capabilities.Add(ItemActionCapabilities.Compatibility);
            return new()
            {
                TargetType=emulated?ItemContextTargetType.EmulatedGame:item.ContentType==ContentType.Application?ItemContextTargetType.Application:ItemContextTargetType.LibraryItem,
                TargetId=item.Id,Title=item.Title,IsFavorite=item.Favorite,IsHidden=item.Hidden,IsAvailable=LaunchTargetValidator.Validate(launch) is null,
                IsProviderManaged=!string.IsNullOrWhiteSpace(launch?.ProviderId),ProviderId=launch?.ProviderId,SourcePath=sourcePath,IsGuest=_guestMode.IsGuest,Capabilities=capabilities,LifecycleStatus=item.ContentType==ContentType.Game?await _contentLifecycle.GetStatusAsync(ContentLifecycleTarget.Library(item.Id)):ContentLifecycleStatus.Unassigned,
                Options=item.LaunchTargets.Where(value=>value.IsEnabled).ToDictionary(value=>"launchTarget."+value.Id,value=>value.Name,StringComparer.Ordinal),
            };
        }
        if(target is GalleryCardViewModel gallery)
        {
            GalleryMediaItem item=await _galleryPersistence.GetMediaItemAsync(gallery.Id)??gallery.Item;
            HashSet<string> capabilities=new([ItemActionCapabilities.Launch,ItemActionCapabilities.Details,ItemActionCapabilities.OpenLocation,ItemActionCapabilities.SetBackground,ItemActionCapabilities.PreferredPlayer],StringComparer.Ordinal);if(item.MediaType==GalleryMediaType.Video){capabilities.Add(ItemActionCapabilities.NativeMediaPlayer);capabilities.Add(ItemActionCapabilities.OpenWith);capabilities.Add(ItemActionCapabilities.PlaybackProgress);capabilities.Add(ItemActionCapabilities.LifecycleStatus);}return new(){TargetType=ItemContextTargetType.GalleryMedia,TargetId=item.Id,Title=item.FileName,IsFavorite=item.Favorite,IsHidden=item.Hidden,IsAvailable=item.Available,IsGuest=_guestMode.IsGuest,SourcePath=item.FilePath,Capabilities=capabilities,LifecycleStatus=item.MediaType==GalleryMediaType.Video?await _contentLifecycle.GetStatusAsync(ContentLifecycleTarget.GalleryMedia(item.Id)):ContentLifecycleStatus.Unassigned,Options=new Dictionary<string,string>{{"mediaType",item.MediaType.ToString()}}};
        }
        if(target is CollectionCardViewModel collection)return new(){TargetType=ItemContextTargetType.Collection,TargetId=collection.Id,Title=collection.Name,IsHidden=collection.Definition.Hidden,IsGuest=_guestMode.IsGuest};
        if(target is DiscoverAppCardViewModel discover)return new(){TargetType=discover.Category.Equals("Streaming",StringComparison.OrdinalIgnoreCase)?ItemContextTargetType.StreamingService:ItemContextTargetType.DiscoverApplication,TargetId=discover.Definition.Id,Title=discover.DisplayName,IsFavorite=discover.IsFavorite,IsAvailable=discover.PrimaryAction!="Unavailable",IsGuest=_guestMode.IsGuest};
        return null;
    }

    private async Task ExecuteContextActionAsync(object target,ItemContextRequest context,ItemActionDescriptor selected)
    {
        ItemActionExecutionResult accepted=await _itemContextMenus.ExecuteAsync(new(context,selected.Id,selected.ExecutionData));
        if(accepted.Status!=ItemActionExecutionStatus.Accepted){ViewModel.ReportStatus(accepted.Message??"That action is unavailable.");return;}
        if(selected.RequiresConfirmation&&!await ConfirmItemActionAsync(context,selected))return;
        try
        {
            if(selected.Id.StartsWith("lifecycle.",StringComparison.Ordinal)&&selected.ExecutionData.TryGetValue("status",out string? rawStatus)&&int.TryParse(rawStatus,out int statusValue)&&Enum.IsDefined((ContentLifecycleStatus)statusValue))
            {
                ContentLifecycleTarget lifecycleTarget=context.TargetType==ItemContextTargetType.GalleryMedia?ContentLifecycleTarget.GalleryMedia(context.TargetId):ContentLifecycleTarget.Library(context.TargetId);await _contentLifecycle.SetStatusAsync(lifecycleTarget,(ContentLifecycleStatus)statusValue);_smartCollections.Invalidate();ViewModel.ReportStatus($"Updated {context.Title}'s status.");return;
            }
            switch(target)
            {
                case CardViewModel card: await ExecuteLibraryActionAsync(card,context,selected);break;
                case GalleryCardViewModel gallery:await ExecuteGalleryActionAsync(gallery,selected.Id);break;
                case CollectionCardViewModel collection:await ExecuteCollectionActionAsync(collection,selected.Id);break;
                case DiscoverAppCardViewModel discover:await ExecuteDiscoverActionAsync(discover,selected.Id);break;
            }
        }
        catch(OperationCanceledException){ViewModel.ReportStatus("The action was cancelled.");}
        catch(Exception exception){_logger.LogError("ItemActionFailed","An item context action failed.",exception,new Dictionary<string,object?>{{"ActionId",selected.Id},{"TargetType",context.TargetType.ToString()},{"TargetId",context.TargetId}});ViewModel.ReportStatus($"{selected.Label} could not complete: {exception.Message}");}
    }

    private async Task<bool> ConfirmItemActionAsync(ItemContextRequest context,ItemActionDescriptor action)
    {
        string message=action.Id switch
        {
            "index.remove" when context.TargetType==ItemContextTargetType.GalleryMedia=>$"Remove {context.Title} from the Gallery index? The original source file will remain on disk.",
            "index.remove"=>$"Remove {context.Title} from NexaStation? The installed program, game files, ROM, and original artwork will remain on disk.",
            "collection.delete"=>$"Delete the Shelf {context.Title}? Games, apps, Gallery items, albums, and files will remain.",
            "installation.manage"=>$"Open installation management for {context.Title}? NexaStation will only open the provider or Windows Installed Apps and will not start an uninstaller.",
            _=>action.Description??$"Continue with {action.Label}?",
        };
        return await Dialog(action.Label,new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap},action.IsDestructive?"Remove":"Continue",null,"Cancel").ShowAsync()==ContentDialogResult.Primary;
    }

    private async Task ExecuteLibraryActionAsync(CardViewModel original,ItemContextRequest context,ItemActionDescriptor action)
    {
        LibraryItem item=await _library.GetByIdAsync(original.Id)??original.Item;CardViewModel card=new(item,original.Glyph);
        string actionId=action.Id;
        if(actionId.StartsWith("launch.play.",StringComparison.Ordinal)&&action.ExecutionData.TryGetValue("launchTargetId",out string? playTargetId)){await ViewModel.LaunchAsync(new CardViewModel(item with{DefaultLaunchTargetId=playTargetId},original.Glyph));return;}
        if(actionId.StartsWith("launch.default.",StringComparison.Ordinal)&&action.ExecutionData.TryGetValue("launchTargetId",out string? defaultTargetId)){await _library.UpdateAsync(item with{DefaultLaunchTargetId=defaultTargetId,UpdatedAtUtc=DateTime.UtcNow});if(await IsProviderManagedAsync(item.Id))await _providerPersistence.AddMetadataOverrideAsync(item.Id,"LaunchBehavior");await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Changed the default launch target for {item.Title}.");return;}
        switch(actionId)
        {
            case "open":await ViewModel.LaunchAsync(card);break;
            case "details":await ShowDetailsAsync(card);break;
            case "edit":await ShowItemEditorAsync(item);break;
            case "favorite.toggle":await ViewModel.ToggleFavoriteAsync(card);break;
            case "hidden.toggle":await ViewModel.SetHiddenAsync(card,!item.Hidden);break;
            case "pin.home":await _pins.PinAsync(new(PinTargetType.LibraryItem,item.Id,PinLocation.Home,DisplayName:item.Title));await ViewModel.RefreshAsync();await _sounds.PlayAsync(UiSoundEvent.Favorite);ViewModel.ReportStatus($"Pinned {item.Title} to Home.");break;
            case "collection.add":await AddCardToCollectionAsync(card);break;
            case "artwork.find":await ShowFindMissingArtworkAsync(item.Id);break;
            case "artwork.choose":await ShowItemEditorAsync(item);break;
            case "artwork.review":await ShowArtworkReviewQueueAsync();break;
            case "artwork.undo":
                foreach(ArtworkKind kind in new[]{ArtworkKind.CoverPortrait,ArtworkKind.GridWide,ArtworkKind.HeroBackground})await _artworkAssignments.UndoAsync(item.Id,kind);
                await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Restored the previous artwork state for {item.Title}.");break;
            case "compatibility.record":
                if(item.LaunchTarget is { } target){CompatibilityResolutionResult resolution=await _compatibilityResolver.ResolveAsync(CompatibilityLaunchPolicy.CreateTarget(item,target));await RecordCompatibilityAsync(item,resolution);}break;
            case "location.open":OpenLocalLocation(context.SourcePath??SourcePath(item));break;
            case "provider.open":await OpenProviderAsync(item);break;
            case "installation.manage":await OpenInstallationManagementAsync(item);break;
            case "emulator.configure":await ShowEmulationSettingsAsync();break;
            case "emulator.relink":
                if(item.LaunchTarget?.EmulatedGameSourceId is { } sourceId&&(await PickGameFileAsync()) is { } replacement){await _emulation.RelinkGameAsync(sourceId,replacement);await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Relinked {item.Title}. The old ROM was not modified.");}break;
            case "application.card-style":ApplicationCardStyle next=item.CardStyle switch{ApplicationCardStyle.Wide=>ApplicationCardStyle.Square,ApplicationCardStyle.Square=>ApplicationCardStyle.Compact,ApplicationCardStyle.Compact=>ApplicationCardStyle.Icon,_=>ApplicationCardStyle.Wide};await _library.UpdateAsync(item with{CardStyle=next,UpdatedAtUtc=DateTime.UtcNow});await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Application card style changed to {next}.");break;
            case "application.category":
                IReadOnlyList<ApplicationCategory> categories=await _windowsPersistence.GetCategoriesAsync();ComboBox categoryBox=new(){ItemsSource=categories,DisplayMemberPath="Name",SelectedValuePath="Name",SelectedValue=item.PrimaryCategoryName,MinWidth=280};if(await Dialog("Application Category",categoryBox,"Save",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&categoryBox.SelectedItem is ApplicationCategory category){await _windowsPersistence.SetCategoriesAsync(item.Id,[new(item.Id,category.Id,true,ApplicationCategorySource.User)]);await ViewModel.RefreshAsync();}break;
            case "application.desktop-control":await ShowDesktopControlSettingsAsync();break;
            case "index.remove":await ViewModel.DeleteAsync(card);ViewModel.ReportStatus($"Removed {item.Title} from NexaStation. Original files were not changed.");break;
        }
    }

    private async Task AddCardToCollectionAsync(CardViewModel card)
    {
        CollectionDefinition[] choices=(await _collections.GetAsync()).Where(value=>value.CollectionType==CollectionType.Manual&&!value.Hidden).ToArray();
        if(choices.Length==0){await CreateCollectionAsync();choices=(await _collections.GetAsync()).Where(value=>value.CollectionType==CollectionType.Manual&&!value.Hidden).ToArray();}
        ComboBox box=new(){Header="Shelf",ItemsSource=choices,DisplayMemberPath="Name",SelectedIndex=choices.Length>0?0:-1,MinWidth=360};
        if(choices.Length>0&&await Dialog("Add to Shelf",box,"Add",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&box.SelectedItem is CollectionDefinition collection){await _collections.AddItemAsync(new(collection.Id,CollectionTargetType.LibraryItem,card.Id,0,DateTime.UtcNow));_smartCollections.Invalidate();await CustomizationViewModel.RefreshCollectionsAsync();ViewModel.ReportStatus($"Added {card.Title} to {collection.Name}.");}
    }

    private async Task ExecuteGalleryActionAsync(GalleryCardViewModel card,string actionId)
    {
        GalleryMediaItem item=await _galleryPersistence.GetMediaItemAsync(card.Id)??card.Item;
        switch(actionId)
        {
            case "open":await OpenGalleryCardAsync(card);break;
            case "details":await ShowGalleryDetailsAsync(card);break;
            case "favorite.toggle":await GalleryViewModel.ToggleFavoriteAsync(card);break;
            case "hidden.toggle":await _galleryPersistence.SetHiddenAsync(item.Id,!item.Hidden);await GalleryViewModel.RefreshAsync();ViewModel.ReportStatus(item.Hidden?$"Unhid {item.FileName}.":$"Hid {item.FileName} from normal Gallery views.");break;
            case "gallery.background":await _galleryBackgrounds.SetAsync(item,GalleryZoomMode.Fill);ViewModel.ReportStatus($"Set {item.FileName} as the NexaStation background.");break;
            case "gallery.player":await ConfigureMediaAssociationAsync(item);break;
            case "media.play.native":
            case "media.resume.native":await OpenNativeVideoAsync(item);break;
            case "media.start.native":await OpenNativeVideoAsync(item);await _nativeMediaPlayer.SeekAsync(TimeSpan.Zero);break;
            case "media.open-with":await ShowOpenWithAsync(item);break;
            case "media.progress.forget":await _nativeMediaPlayer.ForgetProgressAsync(item.Id);ViewModel.ReportStatus($"Forgot playback progress for {item.FileName}.");break;
            case "location.open":OpenLocalLocation(item.FilePath);break;
            case "index.remove":await _galleryPersistence.RemoveMediaIndexAsync(item.Id);await GalleryViewModel.RefreshAsync();UpdateGalleryEmptyState();ViewModel.ReportStatus($"Removed {item.FileName} from the Gallery index. The source file remains on disk.");break;
        }
    }

    private async Task ExecuteCollectionActionAsync(CollectionCardViewModel card,string actionId)
    {
        CollectionDefinition collection=await _collections.GetByIdAsync(card.Id)??card.Definition;
        switch(actionId)
        {
            case "collection.pin":await _pins.PinAsync(new(PinTargetType.Collection,collection.Id,PinLocation.Home,DisplayName:collection.Name));await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Pinned {collection.Name} to Home.");break;
            case "collection.home-row":await AddCollectionRowAsync(collection);break;
            case "collection.rename":
                TextBox name=new(){Header="Shelf name",Text=collection.Name,MinWidth=360};if(await Dialog("Rename Shelf",name,"Save",null,"Cancel").ShowAsync()==ContentDialogResult.Primary&&!string.IsNullOrWhiteSpace(name.Text)){await _collections.SaveAsync(collection with{Name=name.Text.Trim(),UpdatedAtUtc=DateTime.UtcNow});await CustomizationViewModel.RefreshCollectionsAsync();ViewModel.ReportStatus($"Renamed the Shelf to {name.Text.Trim()}.");}break;
            case "collection.delete":await _collections.DeleteAsync(collection.Id);_smartCollections.Invalidate();await CustomizationViewModel.RefreshCollectionsAsync();await ViewModel.RefreshAsync();ViewModel.ReportStatus($"Deleted {collection.Name}. Source items were not removed.");break;
        }
    }

    private async Task ExecuteDiscoverActionAsync(DiscoverAppCardViewModel card,string actionId)
    {
        switch(actionId)
        {
            case "discover.primary":await DiscoverAppsViewModel.ExecutePrimaryAsync(card);break;
            case "discover.details":await Dialog(card.DisplayName,new TextBlock{Text=$"{card.Description}\n\nCategory: {card.Category}\nState: {card.StateBadge}\nPrimary action: {card.PrimaryAction}",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();break;
            case "discover.hide":IReadOnlyList<string> hidden=await _settings.GetAsync("DiscoverApps.HiddenIds",Array.Empty<string>());await _settings.SetAsync("DiscoverApps.HiddenIds",hidden.Append(card.Definition.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());await DiscoverAppsViewModel.InitializeAsync();ViewModel.ReportStatus($"Hid {card.DisplayName} from Discover Apps.");break;
        }
    }

    private static async Task OpenProviderAsync(LibraryItem item)
    {
        LaunchTarget? target=item.LaunchTargets.FirstOrDefault(value=>string.Equals(value.ProviderId,"steam",StringComparison.OrdinalIgnoreCase)&&value.Uri?.Scheme=="steam");
        if(target?.Uri is { } steamUri){await Launcher.LaunchUriAsync(steamUri);return;}
        await OpenInstallationManagementAsync(item);
    }

    private static async Task OpenInstallationManagementAsync(LibraryItem item)
    {
        LaunchTarget? steam=item.LaunchTargets.FirstOrDefault(value=>string.Equals(value.ProviderId,"steam",StringComparison.OrdinalIgnoreCase)&&value.Uri?.Scheme=="steam");
        await Launcher.LaunchUriAsync(steam?.Uri??new Uri("ms-settings:appsfeatures"));
    }

    private static string? SourcePath(LibraryItem item)=>item.LaunchTarget?.ExecutablePath??item.LaunchTarget?.ShortcutPath??item.LaunchTarget?.ExpectedExecutablePath;
    private static void OpenLocalLocation(string? path)
    {
        if(string.IsNullOrWhiteSpace(path))return;
        string full=Path.GetFullPath(path);
        if(Directory.Exists(full)){Process.Start(new ProcessStartInfo(full){UseShellExecute=true});return;}
        if(!File.Exists(full))return;
        ProcessStartInfo info=new("explorer.exe"){UseShellExecute=false};info.ArgumentList.Add("/select,"+full);Process.Start(info);
    }

    private void RestoreContextMenuFocus(ItemContextRequest context)
    {
        Control? origin=Interlocked.Exchange(ref _contextMenuOrigin,null);
        if(origin?.XamlRoot is not null&&origin.Focus(FocusState.Programmatic))return;
        if(context.TargetType==ItemContextTargetType.GalleryMedia)FocusGalleryCard(context.TargetId);
        else if(context.TargetType is ItemContextTargetType.LibraryItem or ItemContextTargetType.EmulatedGame or ItemContextTargetType.Application)FocusCard(context.TargetId);
        else FocusSelectedSectionSurface();
    }

    private static object? ContextTargetFrom(DependencyObject? source)
    {
        for(DependencyObject? current=source;current is not null;current=VisualTreeHelper.GetParent(current))
        {
            if(current is FrameworkElement element&&element.DataContext is CardViewModel or GalleryCardViewModel or CollectionCardViewModel or DiscoverAppCardViewModel)return element.DataContext;
            if(current is ContentControl{Content:CardViewModel or GalleryCardViewModel or CollectionCardViewModel or DiscoverAppCardViewModel} content)return content.Content;
        }
        return null;
    }

    private static Control? FindFocusableOrigin(DependencyObject? source)
    {
        Control? fallback=null;
        for(DependencyObject? current=source;current is not null;current=VisualTreeHelper.GetParent(current))
        {
            if(current is ListViewItem or GridViewItem)return (Control)current;
            if(current is Control control&&control.IsTabStop)fallback=control;
        }
        return fallback;
    }

    private void Root_PointerMoved(object sender,PointerRoutedEventArgs e)
    {
        Windows.Foundation.Point position=e.GetCurrentPoint(Root).Position;Microsoft.UI.Input.PointerDeviceType type=e.Pointer.PointerDeviceType;
        if(type==Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            double magnitude=_lastPointerPosition is { } previous?Math.Sqrt(Math.Pow(position.X-previous.X,2)+Math.Pow(position.Y-previous.Y,2)):0;_lastPointerPosition=position;
            _activeInputMode.ReportInput(new(InputMode.Mouse,Magnitude:magnitude));
        }
    }

    private void Root_PointerPressed(object sender,PointerRoutedEventArgs e)
    {
        InputMode mode=e.Pointer.PointerDeviceType switch{Microsoft.UI.Input.PointerDeviceType.Touch=>InputMode.Touch,Microsoft.UI.Input.PointerDeviceType.Mouse=>InputMode.Mouse,_=>InputMode.Mouse};
        _activeInputMode.ReportInput(new(mode,Magnitude:8,IsButtonPress:true));
    }

    private async void InputSettings_Click(object sender, RoutedEventArgs e)
    {
        KeyValuePair<PromptStyle,string>[] promptStyles=Enum.GetValues<PromptStyle>().Select(value=>new KeyValuePair<PromptStyle,string>(value,value switch{PromptStyle.PlayStation=>"PlayStation Shapes",PromptStyle.MinimalText=>"Minimal Text",_=>value.ToString()})).ToArray();
        ComboBox promptStyle = new() { Header = "Prompt style", ItemsSource = promptStyles, DisplayMemberPath="Value", SelectedValuePath="Key", SelectedValue = _promptPreferences.PromptStyle, MinWidth = 420 };
        ComboBox nintendoConvention = new() { Header = "Nintendo confirm convention", ItemsSource = Enum.GetValues<NintendoConfirmConvention>(), SelectedItem = _promptPreferences.NintendoConvention, MinWidth = 420 };
        ComboBox action = new() { Header = "Action to remap", ItemsSource = Enum.GetValues<InputAction>().Distinct().ToArray(), SelectedItem = InputAction.Confirm, MinWidth = 420 };
        string[] controllerButtons = ["FACE_SOUTH", "FACE_EAST", "FACE_WEST", "FACE_NORTH", "MENU", "VIEW", "LEFT_SHOULDER", "RIGHT_SHOULDER", "LEFT_TRIGGER", "RIGHT_TRIGGER", "LEFT_STICK", "RIGHT_STICK", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT", "LEFT_SHOULDER+RIGHT_SHOULDER+FACE_WEST", "LEFT_SHOULDER+RIGHT_SHOULDER+LEFT_STICK+RIGHT_STICK"];
        ComboBox physical = new() { Header = "Controller input", ItemsSource = controllerButtons, SelectedItem = "FACE_SOUTH", MinWidth = 420 };
        NumberBox deadzone = new() { Header = "Navigation deadzone", Minimum = .1, Maximum = .95, SmallChange = .05, Value = .55, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox repeatDelay = new() { Header = "Initial repeat delay (ms)", Minimum = 150, Maximum = 1000, SmallChange = 25, Value = 350, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox repeatRate = new() { Header = "Repeat rate (ms)", Minimum = 50, Maximum = 500, SmallChange = 10, Value = 120, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        CheckBox analog = new() { Content = "Enable analog-stick navigation", IsChecked = true };
        CheckBox dpad = new() { Content = "Enable D-pad navigation", IsChecked = true };
        CheckBox wrap = new() { Content = "Wrap navigation at row edges", IsChecked = false };
        TextBlock safety = new() { Text = "Recovery shortcut: Ctrl+Shift+I. Controller emergency reset remains reserved and cannot be removed.", TextWrapping = TextWrapping.Wrap, Opacity = .75 };
        StackPanel panel = new() { Spacing = 9 };
        foreach (UIElement control in new UIElement[] { promptStyle, nintendoConvention, action, physical, deadzone, repeatDelay, repeatRate, analog, dpad, wrap, safety }) panel.Children.Add(control);

        ContentDialogResult result = await Dialog("Input and Controller Mapping", new ScrollViewer { Content = panel, MaxHeight = 620 }, "Save", "Restore Defaults", "Cancel").ShowAsync();
        if (result == ContentDialogResult.Secondary)
        {
            await RestoreInputDefaultsAsync();
            return;
        }
        if (result != ContentDialogResult.Primary || action.SelectedItem is not InputAction selectedAction || physical.SelectedItem is not string selectedCode) return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        const string profileId = "user.controller.global";
        List<InputBinding> bindings = _bindingService.ActiveBindings
            .Where(binding => !(binding.Action == selectedAction && binding.Context == InputContext.Global && binding.PhysicalInput.Kind is PhysicalInputKind.ControllerButton or PhysicalInputKind.ControllerAxis or PhysicalInputKind.ControllerChord))
            .Select(binding => binding with { InputProfileId = profileId })
            .ToList();
        PhysicalInputKind selectedKind = selectedCode.Contains('+', StringComparison.Ordinal) ? PhysicalInputKind.ControllerChord : PhysicalInputKind.ControllerButton;
        bindings.Add(new($"user.{selectedAction}.{selectedCode}", profileId, selectedAction, new(selectedKind, selectedCode), InputContext.Global, bindings.Count, now, now));
        PromptStyle selectedPrompt = promptStyle.SelectedValue is PromptStyle ps ? ps : PromptStyle.Automatic;
        InputProfile profile = new(profileId, "My Controller Mapping", InputDeviceScope.ControllerFamily, null, selectedPrompt.ToString(), true, false, now, now);
        InputMappingValidationResult validation = await _inputMappings.SaveAsync(profile, bindings);
        if (!validation.IsValid)
        {
            await Dialog("Mapping conflict", new TextBlock { Text = string.Join(Environment.NewLine, validation.Conflicts.Select(conflict => conflict.Message)), TextWrapping = TextWrapping.Wrap }, "Close", null, string.Empty).ShowAsync();
            return;
        }

        NintendoConfirmConvention selectedConvention = nintendoConvention.SelectedItem is NintendoConfirmConvention nc ? nc : NintendoConfirmConvention.PhysicalLabels;
        _promptPreferences.Apply(selectedPrompt, selectedConvention);
        await _settings.SetAsync("Input.PromptStyle", selectedPrompt.ToString());
        await _settings.SetAsync("Input.NintendoConvention", selectedConvention.ToString());
        IReadOnlyList<ControllerStatus> controllers = await _controllerStatus.GetControllersAsync();
        string family = (controllers.Count > 0 ? controllers[0].ControllerFamily : null)?.ToLowerInvariant() ?? "generic";
        await _controllerPreferences.SaveAsync(new($"family:{family}", selectedPrompt.ToString(), (int)selectedConvention, deadzone.Value, (int)repeatDelay.Value, (int)repeatRate.Value, analog.IsChecked == true, dpad.IsChecked == true, wrap.IsChecked == true, now));
        ControllerFooter.Configure(_inputGlyphs);CrossbarControllerFooter.Configure(_inputGlyphs);
        ViewModel.ReportStatus($"Saved {selectedAction} as {selectedCode}. Input prompts now use {selectedPrompt}.");
    }

    private async void DisplaySettings_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<DisplayDescriptor> displays = await _displaySelection.GetDisplaysAsync();
        DisplayPreferences current = await _displayPreferences.GetAsync();
        ComboBox display = new() { Header = "Preferred display", ItemsSource = displays, DisplayMemberPath = "FriendlyName", MinWidth = 440 };
        display.SelectedItem = displays.FirstOrDefault(item => item.Id == current.PreferredDisplayId) ?? displays.FirstOrDefault(item => item.IsCurrent) ?? (displays.Count > 0 ? displays[0] : null);
        ComboBox fullscreen = new() { Header = "Window mode", ItemsSource = Enum.GetValues<FullscreenMode>(), SelectedItem = current.FullscreenMode, MinWidth = 440 };
        NumberBox scale = new() { Header = "Interface scale (80–150%)", Minimum = 80, Maximum = 150, SmallChange = 5, Value = current.InterfaceScale * 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox horizontalInset = new() { Header = "TV safe area — left and right (px)", Minimum = 0, Maximum = 300, SmallChange = 4, Value = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        NumberBox verticalInset = new() { Header = "TV safe area — top and bottom (px)", Minimum = 0, Maximum = 180, SmallChange = 4, Value = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        Slider overlay = new() { Header = "Artwork text-overlay darkness", Minimum = 0, Maximum = 85, Value = 35, MinWidth = 440 };
        CheckBox startPreferred = new() { Content = "Start on the preferred display", IsChecked = current.StartOnPreferredDisplay };
        CheckBox battery = new() { Content = "Reduce effects while on battery", IsChecked = current.ReduceEffectsOnBattery };
        TextBlock details = new() { Text = "Calibration stays inside NexaStation. If a saved display disappears, Windows primary is used without erasing your preference.", TextWrapping = TextWrapping.Wrap, Opacity = .75 };
        if (display.SelectedItem is DisplayDescriptor initial)
        {
            DisplayCalibration? calibration = await _displayCalibrations.GetAsync(initial.Id);
            if (calibration is not null) { horizontalInset.Value = calibration.SafeArea.Left; verticalInset.Value = calibration.SafeArea.Top; overlay.Value = calibration.ArtworkOverlayDarkness * 100; }
        }
        StackPanel panel = new() { Spacing = 9 };
        foreach (UIElement control in new UIElement[] { display, fullscreen, scale, horizontalInset, verticalInset, overlay, startPreferred, battery, details }) panel.Children.Add(control);
        if (await Dialog("Display and TV Calibration", new ScrollViewer { Content = panel, MaxHeight = 620 }, "Apply", null, "Cancel").ShowAsync() != ContentDialogResult.Primary || display.SelectedItem is not DisplayDescriptor selected) return;

        double interfaceScale = Math.Clamp(scale.Value / 100d, .8, 1.5);
        await _displayPreferences.SaveAsync(new(PreferredDisplayMode.SpecificDisplay, selected.Id, startPreferred.IsChecked == true, fullscreen.SelectedItem is FullscreenMode mode ? mode : FullscreenMode.BorderlessFullscreen, interfaceScale, battery.IsChecked == true));
        DisplayCalibration saved = await _safeAreaCalibration.SaveAsync(selected.Id, new((int)horizontalInset.Value, (int)verticalInset.Value, (int)horizontalInset.Value, (int)verticalInset.Value), selected.Resolution.Width, selected.Resolution.Height, interfaceScale, overlay.Value / 100d);
        DisplaySelectionResult moved = await _displaySelection.SelectAsync(selected.Id);
        Root.Padding = new Thickness(saved.SafeArea.Left, saved.SafeArea.Top, saved.SafeArea.Right, saved.SafeArea.Bottom);
        ApplyInterfaceScale(interfaceScale);
        await UpdateBatteryStatusAsync();
        ViewModel.ReportStatus(moved.Succeeded ? $"Display calibration saved for {selected.FriendlyName}." : moved.Message ?? "Display calibration was saved; the window was not moved.");
    }

    private async void GuestSettings_Click(object sender, RoutedEventArgs e)
    {
        GuestModeSettings current = await _frontendProfiles.GetGuestSettingsAsync();
        if (_guestMode.IsGuest)
        {
            bool verified = !current.RequireOwnerPinToLeave;
            if (!verified)
            {
                TextInputResult pin = await _onScreenKeyboard.ShowAsync(new() { Title = "Owner PIN", Description = "Enter the owner PIN to leave Guest Mode.", InputType = TextInputType.Pin, MaximumLength = 8, AllowEmpty = false, IsSensitive = true, SubmitLabel = "Unlock" });
                verified = pin.Submitted && pin.Text is not null && (await _ownerPin.VerifyAsync(pin.Text.AsMemory())).Status == PinVerificationStatus.Succeeded;
            }
            if (!await _guestMode.ExitAsync(verified)) { ViewModel.ReportStatus("Guest Mode remains active: owner PIN verification was not completed."); return; }
            _guestSettings = current;
            ApplyGuestPresentation();
            await ViewModel.RefreshAsync();
            ViewModel.ReportStatus("Returned to the Owner profile. Temporary Guest activity was cleared according to your settings.");
            return;
        }
        ToggleSwitch enabled = new() { Header = "Guest Mode", IsOn = current.Enabled };
        TextBox name = new() { Header = "Guest name", Text = current.GuestName, MinWidth = 420, MaxLength = 48 };
        CheckBox games = new() { Content = "Allow Games", IsChecked = current.AllowGames };
        CheckBox applications = new() { Content = "Allow Applications", IsChecked = current.AllowApplications };
        CheckBox gallery = new() { Content = "Allow Gallery", IsChecked = current.AllowGallery };
        CheckBox media = new() { Content = "Allow Media", IsChecked = current.AllowMedia };
        CheckBox discover = new() { Content = "Allow Discover Apps", IsChecked = current.AllowDiscoverApps };
        CheckBox desktop = new() { Content = "Allow return to desktop", IsChecked = current.AllowDesktopExit };
        CheckBox power = new() { Content = "Allow Quick Menu power actions", IsChecked = current.AllowQuickMenuPowerActions };
        CheckBox clear = new() { Content = "Clear temporary Guest activity on exit", IsChecked = current.ClearActivityOnExit };
        CheckBox pinExit = new() { Content = "Require owner PIN to leave Guest Mode", IsChecked = current.RequireOwnerPinToLeave };
        CheckBox updatePin = new() { Content = current.RequireOwnerPinToLeave ? "Change the owner PIN when saving" : "Set an owner PIN when saving", IsChecked = !current.RequireOwnerPinToLeave };
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(enabled); panel.Children.Add(name);
        foreach (CheckBox control in new[] { games, applications, gallery, media, discover, desktop, power, clear, pinExit, updatePin }) panel.Children.Add(control);
        panel.Children.Add(new TextBlock { Text = "Guest restrictions are local to NexaStation. Hidden items do not expose titles, filenames, or artwork in Guest surfaces.", TextWrapping = TextWrapping.Wrap, Opacity = .75 });
        const string modeAction = "Enter Guest Mode";
        ContentDialogResult result = await Dialog("Guest Mode and Restrictions", new ScrollViewer { Content = panel, MaxHeight = 620 }, "Save", modeAction, "Cancel").ShowAsync();
        if (result == ContentDialogResult.None) return;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        GuestModeSettings updated = current with { Enabled = enabled.IsOn, GuestName = string.IsNullOrWhiteSpace(name.Text) ? "Guest" : name.Text.Trim(), AllowGames = games.IsChecked == true, AllowApplications = applications.IsChecked == true, AllowGallery = gallery.IsChecked == true, AllowMedia = media.IsChecked == true, AllowDiscoverApps = discover.IsChecked == true, AllowDesktopExit = desktop.IsChecked == true, AllowQuickMenuPowerActions = power.IsChecked == true, ClearActivityOnExit = clear.IsChecked == true, RequireOwnerPinToLeave = pinExit.IsChecked == true };
        FrontendProfile guest = new("guest", updated.GuestName, FrontendProfileType.Guest, null, updated.ThemeId, updated.LayoutId, false, false, updated.Enabled, now, now);
        await _frontendProfiles.SaveGuestAsync(guest, updated);
        if (updated.RequireOwnerPinToLeave && updatePin.IsChecked == true)
        {
            TextInputResult pin = await _onScreenKeyboard.ShowAsync(new() { Title = "Set or update owner PIN", Description = "Enter 4–8 digits. The PIN is salted and hashed locally.", InputType = TextInputType.Pin, MaximumLength = 8, AllowEmpty = false, IsSensitive = true, SubmitLabel = "Save PIN" });
            if (pin.Submitted && pin.Text is not null) await _ownerPin.SetAsync(pin.Text.AsMemory());
        }
        if (result == ContentDialogResult.Secondary)
        {
            if (!await _guestMode.EnterAsync()) { ViewModel.ReportStatus("Guest Mode is disabled. Enable and save it before entering."); return; }
            await ViewModel.RefreshAsync();
        }
        _guestSettings = updated;
        ApplyGuestPresentation();
        ViewModel.ReportStatus(_guestMode.IsGuest ? $"Guest Mode active as {_guestMode.ActiveProfile.Name}." : "Guest Mode settings saved for this PC.");
    }

    private async Task ApplyPersistedDisplayAsync()
    {
        try
        {
            DisplayPreferences preferences = await _displayPreferences.GetAsync();
            ApplyInterfaceScale(preferences.InterfaceScale);
            DisplayDescriptor? preferred = await _displaySelection.GetPreferredAsync();
            if (preferred is null) return;
            if (preferences.StartOnPreferredDisplay) await _displaySelection.SelectAsync(preferred.Id);
            DisplayCalibration? calibration = await _displayCalibrations.GetAsync(preferred.Id);
            if (calibration is null) return;
            SafeAreaInsets insets = SafeAreaCalibrationService.ScaleForResolution(calibration, preferred.Resolution.Width, preferred.Resolution.Height);
            Root.Padding = new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
        }
        catch (Exception exception)
        {
            _logger.LogError("DisplayPreferenceApplyFailed", "Saved display preferences could not be applied; the current window remains usable.", exception);
        }
    }

    private void ApplyInterfaceScale(double scale)
    {
        _interfaceScale = Math.Clamp(scale, .8, 1.5);
        if (!_xamlInitialized || WindowRoot.ActualWidth <= 0 || WindowRoot.ActualHeight <= 0) return;
        Root.Width = WindowRoot.ActualWidth / _interfaceScale;
        Root.Height = WindowRoot.ActualHeight / _interfaceScale;
        Root.RenderTransform = new ScaleTransform { ScaleX = _interfaceScale, ScaleY = _interfaceScale };
    }

    private bool IsSectionAllowedForGuest(NavigationSection section) => section switch
    {
        NavigationSection.Games => _guestSettings.AllowGames,
        NavigationSection.Apps => _guestSettings.AllowApplications,
        NavigationSection.Media => _guestSettings.AllowMedia,
        NavigationSection.Gallery => _guestSettings.AllowGallery,
        NavigationSection.Collections => false,
        NavigationSection.Settings => false,
        _ => true,
    };

    private void ApplyGuestPresentation()
    {
        bool guest = _guestMode.IsGuest;
        string profileName = guest ? _guestMode.ActiveProfile.Name : _localUserName;
        UserInitialsText.Text = UserBadgeFormatter.CreateInitials(profileName);
        OwnerAvatarBorder.Background = new SolidColorBrush(guest ? Microsoft.UI.Colors.DimGray : _ownerAvatarKey switch { "aurora" => Microsoft.UI.ColorHelper.FromArgb(255,91,224,197), "violet" => Microsoft.UI.ColorHelper.FromArgb(255,167,118,255), "ember" => Microsoft.UI.ColorHelper.FromArgb(255,255,132,84), _ => Microsoft.UI.ColorHelper.FromArgb(255,79,141,255) });
        string profileDescription = guest ? $"{profileName} — Guest profile" : $"{profileName} — Local owner profile";
        ToolTipService.SetToolTip(GuestProfileButton, profileDescription);
        AutomationProperties.SetName(GuestProfileButton, profileDescription);
        QuickGuestButton.Content = guest ? "Leave Guest Mode" : "Guest Mode Settings";
        ManageButton.Visibility = guest ? Visibility.Collapsed : Visibility.Visible;
        foreach (DashboardSectionViewModel section in ViewModel.Sections) section.Visibility = !guest || IsSectionAllowedForGuest(section.Section) ? Visibility.Visible : Visibility.Collapsed;
        if (guest)
        {
            ContinuePanel.Visibility = _guestSettings.AllowGames ? ViewModel.ContinueSectionVisibility : Visibility.Collapsed;
            ApplicationsPanel.Visibility = _guestSettings.AllowApplications ? ViewModel.ApplicationSectionVisibility : Visibility.Collapsed;
            RecentPhotosPanel.Visibility = _guestSettings.AllowGallery ? GalleryViewModel.HomePreviewVisibility : Visibility.Collapsed;
            HomeMediaPanel.Visibility = _guestSettings.AllowMedia ? Visibility.Visible : Visibility.Collapsed;
            FavoritesPanel.Visibility = Visibility.Collapsed;
            RecentlyAddedPanel.Visibility = Visibility.Collapsed;
            if (!IsSectionAllowedForGuest(ViewModel.SelectedSection.Section)) ViewModel.SelectSection(ViewModel.Sections[0]);
        }
        else
        {
            ContinuePanel.Visibility = ViewModel.ContinueSectionVisibility;
            ApplicationsPanel.Visibility = ViewModel.ApplicationSectionVisibility;
            RecentPhotosPanel.Visibility = GalleryViewModel.HomePreviewVisibility;
            HomeMediaPanel.Visibility = Visibility.Visible;
            FavoritesPanel.Visibility = ViewModel.FavoriteSectionVisibility;
            RecentlyAddedPanel.Visibility = Visibility.Visible;
        }
        UpdateSectionSurface();
    }

    private async void OnFrontendProfileChanged(object? sender, FrontendProfileChangedEventArgs e)
    {
        _guestSettings = await _frontendProfiles.GetGuestSettingsAsync();
        DispatcherQueue.TryEnqueue(ApplyGuestPresentation);
    }

    private async void TouchQuickMenu_Click(object sender, RoutedEventArgs e) => await ToggleQuickMenuAsync();

    private async Task UpdateBatteryStatusAsync()
    {
        try
        {
            int percentage = Windows.System.Power.PowerManager.RemainingChargePercent;
            HeaderBatteryStatus.Text = percentage is >= 0 and <= 100 ? $"{percentage}%" : string.Empty;
            Windows.System.Power.BatteryStatus batteryStatus = Windows.System.Power.PowerManager.BatteryStatus;
            DisplayPreferences preferences = await _displayPreferences.GetAsync();
            HandheldPowerStatus status = new(percentage, batteryStatus switch { Windows.System.Power.BatteryStatus.Charging => BatteryChargeState.Charging, Windows.System.Power.BatteryStatus.Discharging => BatteryChargeState.Discharging, Windows.System.Power.BatteryStatus.Idle => BatteryChargeState.Full, _ => BatteryChargeState.Unknown }, batteryStatus == Windows.System.Power.BatteryStatus.Discharging ? PowerSourceKind.Battery : PowerSourceKind.Ac, null, DateTimeOffset.UtcNow);
            EffectiveBatteryVisualState visual = BatteryVisualPolicy.Resolve(new(preferences.ReduceEffectsOnBattery, VisualQualityPreset.Balanced, _activeLayout == InterfaceLayoutType.CompactHandheld), status);
            DashboardBackgroundImage.Opacity = visual.BlurEnabled ? .2 : .08;
        }
        catch (Exception) { HeaderBatteryStatus.Text = string.Empty; }
    }

    private async Task ShowSafeInterfaceResetAsync()
    {
        ContentDialogResult result=await Dialog("Recover Interface Defaults",new TextBlock{Text="Reset layouts, dashboard rows, themes, sounds, and visual performance to safe defaults? A configuration backup is created first. Your library, play history, Gallery index, and original files are not changed.",TextWrapping=TextWrapping.Wrap},"Create Backup and Reset",null,"Cancel").ShowAsync();if(result!=ContentDialogResult.Primary)return;try{await _interfaceConfiguration.ResetAllAsync();await CustomizationViewModel.InitializeAsync();await CustomizationViewModel.ApplyThemeAsync(CustomizationViewModel.CurrentThemeId);await ViewModel.RefreshAsync();UpdateSectionSurface();await _sounds.PlayAsync(UiSoundEvent.Confirm);ViewModel.ReportStatus("Interface defaults restored. A recovery backup was retained.");}catch(Exception ex){await _sounds.PlayAsync(UiSoundEvent.Error);ViewModel.ReportStatus($"Interface reset was not completed: {ex.Message}");}
    }

    private void OnInputActionTriggered(object? sender, InputActionEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () => await HandleInputActionAsync(e.Action));
    }

    private async Task HandleInputActionAsync(InputAction action)
    {
        if(NativePlayerOverlay.Visibility==Visibility.Visible){await NativePlayerPage.HandleInputActionAsync(action);return;}
        if(SettingsOverlay.Visibility==Visibility.Visible)
        {
            if(action==InputAction.Cancel){await CloseSettingsHomeAsync();return;}
            if(action==InputAction.QuickMenu){await ToggleQuickMenuAsync();return;}
            if(action==InputAction.Search){FindDescendant<TextBox>(SettingsViewHost)?.Focus(FocusState.Programmatic);return;}
            if(action==InputAction.Confirm&&FocusManager.GetFocusedElement(Root.XamlRoot) is Button button){if(new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)invoke.Invoke();return;}
            if(action is InputAction.NavigateUp or InputAction.NavigateDown or InputAction.NavigateLeft or InputAction.NavigateRight){MoveFocus(action switch{InputAction.NavigateUp=>FocusNavigationDirection.Up,InputAction.NavigateDown=>FocusNavigationDirection.Down,InputAction.NavigateLeft=>FocusNavigationDirection.Left,_=>FocusNavigationDirection.Right});return;}
            return;
        }
        if(action==InputAction.CaptureScreenshot){await CaptureScreenshotAsync(CaptureSourceMode.NexaStationWindow);return;}
        if(action==InputAction.QuickMenu){await ToggleQuickMenuAsync();return;}
        if(_quickMenu.IsOpen)
        {
            if(action==InputAction.Cancel){await _quickMenu.CloseAsync();return;}
            if(action is InputAction.NavigateLeft or InputAction.NavigateRight){SystemVolumeState state=await _volume.GetStateAsync();if(state.Available)await _volume.SetVolumeAsync(state.Volume+(action==InputAction.NavigateRight?0.05:-0.05));return;}
            if(action==InputAction.Confirm&&FocusManager.GetFocusedElement(Root.XamlRoot) is Button{Tag:string actionId}){await _quickMenu.ExecuteActionAsync(actionId);return;}
            MoveFocus(action switch{InputAction.NavigateUp=>FocusNavigationDirection.Up,InputAction.NavigateDown=>FocusNavigationDirection.Down,InputAction.NavigateLeft=>FocusNavigationDirection.Left,_=>FocusNavigationDirection.Right});return;
        }
        if(GalleryViewerOverlay.Visibility==Visibility.Visible)
        {
            switch(action){case InputAction.NavigateLeft:await _galleryViewer.MoveAsync(-1,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));await LoadGalleryViewerAsync();break;case InputAction.NavigateRight:await _galleryViewer.MoveAsync(1,Math.Max(1,ViewerSurface.ActualWidth),Math.Max(1,ViewerSurface.ActualHeight));await LoadGalleryViewerAsync();break;case InputAction.Cancel:await CloseGalleryViewerAsync();break;case InputAction.NavigateUp:_galleryViewer.Zoom(1.15);ApplyGalleryViewerTransform();break;case InputAction.NavigateDown:_galleryViewer.Zoom(0.87);ApplyGalleryViewerTransform();break;}return;
        }
        if(ShelfDetailRoot.Visibility==Visibility.Visible)
        {
            if(action==InputAction.Cancel){ShelfBack_Click(this,new RoutedEventArgs());return;}
            if(action==InputAction.Search){ShelfSearchBox.Focus(FocusState.Programmatic);return;}
            if(action is InputAction.NextSection or InputAction.PreviousSection)return;
        }
        switch (action)
        {
            case InputAction.NavigateUp:
                MoveFocus(FocusNavigationDirection.Up);
                break;
            case InputAction.NavigateDown:
                MoveFocus(FocusNavigationDirection.Down);
                break;
            case InputAction.NavigateLeft:
                MoveFocus(FocusNavigationDirection.Left);
                break;
            case InputAction.NavigateRight:
                MoveFocus(FocusNavigationDirection.Right);
                break;
            case InputAction.NextSection:
                ViewModel.MoveSection(1);
                UpdateSectionSurface();
                DispatcherQueue.TryEnqueue(FocusRememberedOrFirstCard);
                break;
            case InputAction.PreviousSection:
                ViewModel.MoveSection(-1);
                UpdateSectionSurface();
                DispatcherQueue.TryEnqueue(FocusRememberedOrFirstCard);
                break;
            case InputAction.Confirm:
                if(FocusManager.GetFocusedElement(Root.XamlRoot) is Button focusedButton)
                {
                    if(new ButtonAutomationPeer(focusedButton).GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)invoke.Invoke();
                    break;
                }
                await ExecuteResolvedActionAsync(InputAction.Confirm);
                break;
            case InputAction.Cancel:
                if (ViewModel.IsErrorOpen)
                {
                    ViewModel.CloseError();
                }
                else if (ViewModel.IsStatusOpen)
                {
                    ViewModel.CloseStatus();
                }
                else
                {
                    ViewModel.SelectSection(ViewModel.Sections[0]);
                    UpdateSectionSurface();
                    DispatcherQueue.TryEnqueue(FocusRememberedOrFirstCard);
                }

                break;
            case InputAction.Search:
                await OpenSearchKeyboardAsync();
                break;
            case InputAction.Favorite:
                await ExecuteResolvedActionAsync(InputAction.Favorite);
                break;
            case InputAction.ContextMenu:
                await ExecuteResolvedActionAsync(InputAction.ContextMenu);
                break;
            case InputAction.ToggleDetails:
                await ExecuteResolvedActionAsync(InputAction.ToggleDetails);
                break;
            case InputAction.Options:
                if(!await ExecuteResolvedActionAsync(InputAction.Options,false))ManageButton.Flyout?.ShowAt(ManageButton);
                break;
            case InputAction.ReturnToDesktop:
                if (_guestMode.IsGuest && !_guestSettings.AllowDesktopExit) ViewModel.ReportStatus("Returning to the desktop is disabled in Guest Mode.");
                else await _windowActions.ReturnToDesktopAsync();
                break;
            case InputAction.EmergencyDisable:
                await RestoreInputDefaultsAsync();
                break;
            case InputAction.QuickMenu:break;
            case InputAction.PreviousPage:
            case InputAction.NextPage:
            case InputAction.ToggleKeyboard:
            case InputAction.DeleteCharacter:
            case InputAction.SubmitText:
            case InputAction.SwitchKeyboardLayout:
            case InputAction.Shift:
            case InputAction.Space:
            case InputAction.CursorLeft:
            case InputAction.CursorRight:
            case InputAction.Home:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T:DependencyObject
    {
        int count=VisualTreeHelper.GetChildrenCount(root);for(int index=0;index<count;index++){DependencyObject child=VisualTreeHelper.GetChild(root,index);if(child is T value)return value;T? nested=FindDescendant<T>(child);if(nested is not null)return nested;}return null;
    }

    private async Task<bool> ExecuteResolvedActionAsync(InputAction action,bool reportFailure=true)
    {
        ActionTargetResolution resolution=ResolveActionTarget(action);
        if(!resolution.Succeeded){if(reportFailure&&resolution.Status!=ActionTargetResolutionStatus.UnsupportedAction)ViewModel.ReportStatus(resolution.Message??"No actionable item currently has focus.");return false;}
        PrimaryActionResult result=await _primaryActions.ExecuteAsync(action,resolution.Target!);
        if(!result.Succeeded&&reportFailure)ViewModel.ReportStatus(result.Message??"The focused action could not be completed.");
        return result.Succeeded;
    }

    private async Task OpenSearchKeyboardAsync()
    {
        Control? origin=FocusManager.GetFocusedElement(Root.XamlRoot) as Control;string initial=ViewModel.SelectedSection.Section switch{NavigationSection.Library=>LibrarySearch.Text,NavigationSection.Gallery=>GallerySearch.Text,_=>string.Empty};
        _inputContext.SetCurrent(InputContext.TextEntry);Stopwatch timer=Stopwatch.StartNew();
        try
        {
            TextInputResult result=await _onScreenKeyboard.ShowAsync(new(){Title=$"Search {ViewModel.SelectedSection.Title}",Description="Searches only your local NexaStation library and catalogue.",InitialText=initial,Placeholder="Title, application, Shelf, platform, or provider",InputType=TextInputType.Search,MaximumLength=160,AllowEmpty=true,SuggestionsSource=SuggestionsSource.AllLocal,SubmitLabel="Search",CancelLabel="Cancel"});
            timer.Stop();_logger.Information("OnScreenKeyboardCompleted","The local on-screen keyboard session completed.",new Dictionary<string,object?>{{"Submitted",result.Submitted},{"OpenMilliseconds",timer.Elapsed.TotalMilliseconds},{"Sensitive",false}});
            if(!result.Submitted){origin?.Focus(FocusState.Programmatic);return;}
            string query=result.Text??string.Empty;
            if(ViewModel.SelectedSection.Section==NavigationSection.Gallery){GallerySearch.Text=query;GalleryViewModel.Search(query);}else{if(ViewModel.SelectedSection.Section==NavigationSection.Library)LibrarySearch.Text=query;ViewModel.Search(query);}
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,FocusRememberedOrFirstCard);
        }
        finally{_inputContext.SetCurrent(CurrentInputContext());}
    }

    private async Task RestoreInputDefaultsAsync()
    {
        ContentDialogResult result=await Dialog("Restore Input Defaults",new TextBlock{Text="Restore the active NexaStation input profile to safe keyboard and controller defaults? This does not change Windows or game controls.",TextWrapping=TextWrapping.Wrap},"Restore Defaults",null,"Cancel").ShowAsync();if(result!=ContentDialogResult.Primary)return;await _inputMappings.RestoreDefaultsAsync();ControllerFooter.Configure(_inputGlyphs);CrossbarControllerFooter.Configure(_inputGlyphs);ViewModel.ReportStatus("Input mappings restored. Ctrl+Shift+I remains available for recovery.");
    }

    private InputContext CurrentInputContext()=>_quickMenu.IsOpen?InputContext.QuickMenu:GalleryViewerOverlay.Visibility==Visibility.Visible?InputContext.GalleryViewer:InputContext.Dashboard;

    public async Task ConfigureCaptureHotkeyAsync(bool safeMode)
    {
        await _globalHotkeys.UnregisterAllAsync();if(safeMode||!await _settings.GetAsync("Capture.GlobalHotkeyEnabled",false))return;HotkeyRegistrationResult result=await _globalHotkeys.RegisterAsync(new("capture.screenshot",6,0x7B,true,true));if(!result.Succeeded)ViewModel.ReportStatus(result.Message);
    }

    private void OnGlobalHotkeyPressed(object? sender,GlobalHotkeyPressedEventArgs e)
    {if(e.HotkeyId=="capture.screenshot")DispatcherQueue.TryEnqueue(async()=>await CaptureScreenshotAsync(CaptureSourceMode.ActiveTrackedWindow));}

    private async Task CaptureScreenshotAsync(CaptureSourceMode mode)
    {
        if(!await _settings.GetAsync("Capture.ExplanationAccepted",false))
        {
            ContentDialogResult explanation=await Dialog("Screenshot capture",new TextBlock{Text="NexaStation never captures automatically. This action creates exactly one screenshot now. Game launch, exit, achievements, activity, timers, providers, startup, and background monitoring can never trigger a capture.",TextWrapping=TextWrapping.Wrap},"Capture Once",null,"Cancel").ShowAsync();if(explanation!=ContentDialogResult.Primary)return;await _settings.SetAsync("Capture.ExplanationAccepted",true);
        }
        string folder=await _settings.GetAsync("Capture.ScreenshotFolder",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"NexaStation Captures"));CaptureOutputFormat format=await _settings.GetAsync("Capture.Format",CaptureOutputFormat.Png);bool cursor=await _settings.GetAsync("Capture.IncludeCursor",false);bool gallery=await _settings.GetAsync("Capture.AddToGallery",true);CaptureGesture gesture=_captureConsent.IssueForDirectUserAction("CaptureScreenshot");string? targetId=_quickMenu.State.OriginItemId;LibraryItem? item=targetId is null?null:await _library.GetByIdAsync(targetId);ActivityPrivacyLevel privacy=_guestMode.IsGuest?ActivityPrivacyLevel.GuestTemporary:item?.Hidden==true?ActivityPrivacyLevel.Private:ActivityPrivacyLevel.Normal;
        ScreenshotCaptureResult result=await _screenshotCapture.CaptureAsync(new(mode,mode==CaptureSourceMode.NexaStationWindow?WinRT.Interop.WindowNative.GetWindowHandle(this).ToString(CultureInfo.InvariantCulture):null,null,false,cursor,format,await _settings.GetAsync("Capture.JpegQuality",92),folder,targetId,null,privacy,gallery,true,gesture.Token,item?.Title));if(result.Succeeded){_notifications.Publish(new(Guid.NewGuid().ToString("N"),NotificationType.Success,"Screenshot saved",result.Message??"Screenshot saved.",DateTime.UtcNow));await GalleryViewModel.RefreshAsync();}else if(!result.Cancelled)_notifications.Publish(new(Guid.NewGuid().ToString("N"),NotificationType.Error,"Screenshot failed",result.Message??"The screenshot was not created.",DateTime.UtcNow));
    }

    private async void ActivityTimeline_Click(object sender,RoutedEventArgs e)
    {
        ComboBox filter=new(){Header="Filter",ItemsSource=new[]{"All","Games","Applications","Captures","Remote Play","Achievements"},SelectedIndex=0,MinWidth=240};ListView list=new(){MinHeight=280,MaxHeight=500,SelectionMode=ListViewSelectionMode.Single};async Task Refresh(){HashSet<ActivityEventType>? types=filter.SelectedIndex switch{1=>[ActivityEventType.GameStarted,ActivityEventType.GameEnded],2=>[ActivityEventType.ApplicationOpened,ActivityEventType.ApplicationEnded],3=>[ActivityEventType.ScreenshotCaptured,ActivityEventType.RecordingCreated],4=>[ActivityEventType.RemotePlayStarted,ActivityEventType.RemotePlayEnded],5=>[ActivityEventType.AchievementUnlocked],_=>null};IReadOnlyList<NexaStation.Domain.ActivityEvent> events=await _activityTimeline.QueryAsync(new(types,MaximumResults:200));list.ItemsSource=events.Select(value=>$"{Group(value.OccurredAtUtc)}  ·  {value.OccurredAtUtc.ToLocalTime():g}\n{value.TitleSnapshot??value.EventType.ToString()}  —  {value.EventType}").ToArray();}filter.SelectionChanged+=async(_,_)=>await Refresh();await Refresh();StackPanel panel=new(){Spacing=10};panel.Children.Add(filter);panel.Children.Add(new TextBlock{Text="Private, hidden, and Guest-temporary events are excluded from this global timeline.",TextWrapping=TextWrapping.Wrap,Opacity=.7});panel.Children.Add(list);ContentDialogResult result=await Dialog("Activity Timeline",panel,"Close","Export JSON",string.Empty).ShowAsync();if(result==ContentDialogResult.Secondary){FileSavePicker picker=new();picker.SuggestedFileName="NexaStation-Activity";picker.FileTypeChoices.Add("JSON",new List<string>{".json"});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));StorageFile? file=await picker.PickSaveFileAsync();if(file is not null){await _activityExport.ExportAsync(new("json",file.Path,new(MaximumResults:20_000)));ViewModel.ReportStatus("Activity exported locally with private entries and file paths excluded.");}}
    }
    private static string Group(DateTime utc){DateTime local=utc.ToLocalTime().Date;DateTime today=DateTime.Today;if(local==today)return "Today";if(local==today.AddDays(-1))return "Yesterday";if(local>=today.AddDays(-7))return "This Week";return "Earlier";}

    private async void CaptureSettings_Click(object sender,RoutedEventArgs e)
    {
        string folder=await _settings.GetAsync("Capture.ScreenshotFolder",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"NexaStation Captures"));TextBox folderBox=new(){Header="Screenshot folder",Text=folder,MinWidth=480,IsReadOnly=true};ComboBox format=new(){Header="Image format",ItemsSource=Enum.GetNames<CaptureOutputFormat>().Where(value=>value!="Video").ToArray(),SelectedIndex=(int)await _settings.GetAsync("Capture.Format",CaptureOutputFormat.Png)};Slider quality=new(){Header="JPEG quality",Minimum=50,Maximum=100,Value=await _settings.GetAsync("Capture.JpegQuality",92),StepFrequency=1};CheckBox cursor=new(){Content="Include cursor",IsChecked=await _settings.GetAsync("Capture.IncludeCursor",false)};CheckBox gallery=new(){Content="Add successful captures to Gallery when this approved folder is indexed",IsChecked=await _settings.GetAsync("Capture.AddToGallery",true)};ToggleSwitch hotkey=new(){Header="Global screenshot hotkey: Ctrl + Shift + F12",OffContent="Disabled (default)",OnContent="Explicitly enabled",IsOn=await _settings.GetAsync("Capture.GlobalHotkeyEnabled",false)};StackPanel panel=new(){Spacing=9};panel.Children.Add(new TextBlock{Text="Every screenshot still requires a direct button, controller chord, or hotkey press in the current session. No setting permits automatic capture or automatic recording.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(folderBox);panel.Children.Add(format);panel.Children.Add(quality);panel.Children.Add(cursor);panel.Children.Add(gallery);panel.Children.Add(hotkey);ContentDialogResult result=await Dialog("Capture and Recording",panel,"Save","Choose Folder","Cancel").ShowAsync();if(result==ContentDialogResult.Secondary){string? selected=await PickFolderAsync();if(selected is not null){await _settings.SetAsync("Capture.ScreenshotFolder",selected);ViewModel.ReportStatus("Screenshot folder selected. Save Capture settings to approve Gallery indexing and hotkey preferences.");}return;}if(result!=ContentDialogResult.Primary)return;await _settings.SetAsync("Capture.ScreenshotFolder",folderBox.Text);await _settings.SetAsync("Capture.Format",(CaptureOutputFormat)Math.Max(0,format.SelectedIndex));await _settings.SetAsync("Capture.JpegQuality",(int)quality.Value);await _settings.SetAsync("Capture.IncludeCursor",cursor.IsChecked==true);await _settings.SetAsync("Capture.AddToGallery",gallery.IsChecked==true);await _settings.SetAsync("Capture.GlobalHotkeyEnabled",hotkey.IsOn);if(gallery.IsChecked==true){string approved=folderBox.Text;IReadOnlyList<GallerySource> sources=await _galleryPersistence.GetSourcesAsync();if(!sources.Any(value=>Path.GetFullPath(approved).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(value.FolderPath))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||string.Equals(Path.GetFullPath(approved),Path.GetFullPath(value.FolderPath),StringComparison.OrdinalIgnoreCase))){DateTime now=DateTime.UtcNow;await _galleryLibrary.AddSourceAsync(new(){Id=Guid.NewGuid().ToString("N"),Name="NexaStation Captures",FolderPath=approved,NormalizedFolderPath=approved,SourceKind=GallerySourceKind.Screenshots,WatchForChanges=true,IncludeImages=true,IncludeVideos=false,PrivacyLevel=GalleryPrivacyLevel.Normal,CreatedAtUtc=now,UpdatedAtUtc=now},true,false);await _galleryWatchers.ReloadAsync();}}await ConfigureCaptureHotkeyAsync(false);ViewModel.ReportStatus("Capture settings saved. Automatic screenshots and recordings remain prohibited.");
    }

    private async void RemotePlaySettings_Click(object sender,RoutedEventArgs e)
    {
        IReadOnlyList<LibraryItem> apps=await _library.QueryAsync(new(ContentType:ContentType.Application,MaximumResults:1000));IReadOnlyList<RemotePlayProfile> existing=await _remotePlayProfiles.GetAsync();ComboBox application=new(){Header="Remote-play application",ItemsSource=apps,DisplayMemberPath="Title",SelectedIndex=apps.Count>0?0:-1,MinWidth=420};TextBox name=new(){Header="Profile name",Text="Remote Play",MinWidth=420};TextBox target=new(){Header="Target label",PlaceholderText="Living room PC"};ComboBox service=new(){Header="Service",ItemsSource=Enum.GetNames<RemotePlayServiceType>(),SelectedIndex=(int)RemotePlayServiceType.Custom};TextBox arguments=new(){Header="Safe argument template",PlaceholderText="Only {TARGET_LABEL}, {HOST_HINT}, {DISPLAY_WIDTH}, {DISPLAY_HEIGHT}"};StackPanel panel=new(){Spacing=8};panel.Children.Add(new TextBlock{Text=$"Configured profiles: {existing.Count}. NexaStation launches clients only; it never pairs hosts or stores remote-service passwords.",TextWrapping=TextWrapping.Wrap});panel.Children.Add(application);panel.Children.Add(name);panel.Children.Add(target);panel.Children.Add(service);panel.Children.Add(arguments);if(await Dialog("Remote Play",panel,"Save Profile",null,"Close").ShowAsync()!=ContentDialogResult.Primary||application.SelectedItem is not LibraryItem item)return;DateTime now=DateTime.UtcNow;RemotePlayProfile profile=new(Guid.NewGuid().ToString("N"),string.IsNullOrWhiteSpace(name.Text)?item.Title:name.Text.Trim(),item.Id,item.DefaultLaunchTargetId,(RemotePlayServiceType)Math.Max(0,service.SelectedIndex),string.IsNullOrWhiteSpace(target.Text)?null:target.Text.Trim(),null,string.IsNullOrWhiteSpace(arguments.Text)?null:arguments.Text,null,true,true,ApplicationTrackingMode.LaunchedProcess,null,false,true,true,true,now,now);try{_ = RemotePlayArgumentTemplate.Render(profile.ArgumentsTemplate,profile);await _remotePlayProfiles.SaveAsync(profile);ViewModel.ReportStatus("Remote-play profile saved with a shell-free argument template.");}catch(InvalidOperationException ex){ViewModel.ReportStatus(ex.Message);}
    }

    private async void EcosystemSettings_Click(object sender,RoutedEventArgs e)
    {
        await _ecosystemCapabilities.RefreshAsync(EcosystemCapabilityRefreshReason.UserRequested);EcosystemCapabilitySnapshot snapshot=await _ecosystemCapabilities.GetSnapshotAsync();string achievements=string.Join(Environment.NewLine,snapshot.Achievements.Select(value=>$"{value.ProviderId}: {value.CapabilityStatus}"));string downloads=string.Join(Environment.NewLine,snapshot.DownloadVisibility.Select(value=>$"{value.ProviderId}: {value.Status}"));TextBlock text=new(){Text=$"Screenshot capture: {snapshot.ScreenshotCapture.State}\nDiscord presence: {snapshot.DiscordPresence.State}\nMedia sessions: {(snapshot.MediaSessionsAvailable?"Supported":"Unavailable")}\nMobile remote listener: {(snapshot.MobileRemotePrototypeEnabled?"Enabled":"Not present")}\n\nAchievements\n{achievements}\n\nDownload visibility\n{downloads}\n\nNo unsupported provider produces achievement cards or download percentages.",TextWrapping=TextWrapping.Wrap};await Dialog("Ecosystem Integrations",new ScrollViewer{Content=text,MaxHeight=600},"Close",null,string.Empty).ShowAsync();
    }

    private void RegisterQuickMenuActions()
    {
        _quickMenu.RegisterAction(new("audio.mute","Mute","Toggle master endpoint mute.","Volume",QuickMenuActionType.Toggle),async token=>{SystemVolumeState state=await _volume.GetStateAsync(token);if(state.Available)await _volume.SetMutedAsync(!state.Muted,token);});
        _quickMenu.RegisterAction(new("audio.settings","Sound Settings","Open supported Windows Sound Settings.","Settings",QuickMenuActionType.Navigation),async token=>{AudioOutputSnapshot snapshot=await _audioOutputs.GetSnapshotAsync(token);await _audioOutputs.SetDefaultOutputAsync(snapshot.DefaultDeviceId??"default",token);});
        _quickMenu.RegisterAction(new("media.previous","Previous","Previous system media item.","Previous",QuickMenuActionType.Normal),async token=>{await _systemMedia.PreviousAsync(token);});
        _quickMenu.RegisterAction(new("media.playpause","Play / Pause","Toggle the active Windows media session.","Play",QuickMenuActionType.Toggle),async token=>{await _systemMedia.PlayPauseAsync(token);});
        _quickMenu.RegisterAction(new("media.next","Next","Next system media item.","Next",QuickMenuActionType.Normal),async token=>{await _systemMedia.NextAsync(token);});
        _quickMenu.RegisterAction(new("capture.screenshot","Capture Screenshot","Create exactly one user-requested screenshot.","Camera",QuickMenuActionType.Normal),async _=>await CaptureScreenshotAsync(CaptureSourceMode.CurrentDisplay));
        _quickMenu.RegisterAction(new("capture.snipping","Open Snipping Tool","Open the visible Windows capture UI. NexaStation does not claim its output.","Camera",QuickMenuActionType.Navigation),async token=>{if(!await _snippingTool.OpenAsync(false,token))ViewModel.ReportStatus("Windows Snipping Tool is unavailable.");});
        _quickMenu.RegisterAction(new("window.minimize","Minimize","Minimize NexaStation normally.","Minimize",QuickMenuActionType.System),_=>_windowActions.MinimizeAsync());
        _quickMenu.RegisterAction(new("window.desktop","Return to Desktop","Minimize NexaStation without changing the Windows shell.","Desktop",QuickMenuActionType.System),_=>_windowActions.ReturnToDesktopAsync());
        _quickMenu.RegisterAction(new("window.restart","Restart NexaStation","Start one replacement instance, then close this one.","Restart",QuickMenuActionType.System,true),_=>_windowActions.RestartApplicationAsync());
        RegisterPower("power.sleep","Sleep",SystemPowerAction.Sleep,false);
        RegisterPower("power.signout","Sign Out",SystemPowerAction.SignOut,false);
        RegisterPower("power.restart","Restart PC",SystemPowerAction.Restart,true);
        RegisterPower("power.shutdown","Shut Down",SystemPowerAction.ShutDown,true);
    }

    private void RegisterPower(string id,string label,SystemPowerAction action,bool hold)=>_quickMenu.RegisterAction(new(id,label,$"Request Windows {label} using a documented API.","Power",QuickMenuActionType.Destructive,true,hold,hold?1500:0),async token=>{string historyId=Guid.NewGuid().ToString("N");DateTime requested=DateTime.UtcNow;await _systemActionHistory.RecordAsync(new(historyId,action,requested,null,0,null),token);try{SystemPowerResult result=await _power.ExecuteAsync(action,token);await _systemActionHistory.RecordAsync(new(historyId,action,requested,DateTime.UtcNow,result.Succeeded?1:2,result.FailureReason),CancellationToken.None);if(!result.Succeeded)ViewModel.ReportStatus(result.FailureReason??$"{label} was not completed.");}catch(Exception ex){await _systemActionHistory.RecordAsync(new(historyId,action,requested,DateTime.UtcNow,2,ex.Message),CancellationToken.None);throw;}});

    private async Task ToggleQuickMenuAsync()
    {
        if(_quickMenu.IsOpen){await _quickMenu.CloseAsync();return;}
        if(!await _settings.GetAsync("QuickMenu.Enabled",true)){ViewModel.ReportStatus("Quick Menu is disabled in Settings.");return;}
        _quickMenuOrigin=FocusManager.GetFocusedElement(Root.XamlRoot) as Control;string? itemId=(_quickMenuOrigin as FrameworkElement)?.DataContext switch{CardViewModel card=>card.Id,GalleryCardViewModel card=>card.Id,_=>null};string? key=(_quickMenuOrigin as FrameworkElement)?.Name;
        await _quickMenu.OpenAsync(new(ViewModel.SelectedSection.Section.ToString(),itemId,key,RestoreQuickMenuFocusAsync));
    }

    private Task RestoreQuickMenuFocusAsync(){Control? origin=Interlocked.Exchange(ref _quickMenuOrigin,null);if(origin?.XamlRoot is not null&&origin.Focus(FocusState.Programmatic))return Task.CompletedTask;FocusRememberedOrFirstCard();return Task.CompletedTask;}
    private async void QuickMenuClose_Click(object sender,RoutedEventArgs e)=>await _quickMenu.CloseAsync();
    private async void QuickAction_Click(object sender,RoutedEventArgs e){if(sender is Button{Tag:string id}){if(_guestMode.IsGuest&&(id.StartsWith("power.",StringComparison.Ordinal)||id=="window.desktop")&&(!_guestSettings.AllowQuickMenuPowerActions||id=="window.desktop"&&!_guestSettings.AllowDesktopExit)){ViewModel.ReportStatus("That system action is disabled in Guest Mode.");return;}try{await _quickMenu.ExecuteActionAsync(id);}catch(Exception ex){ViewModel.ReportStatus(ex.Message);_notifications.Publish(new(Guid.NewGuid().ToString("N"),NotificationType.Error,"Quick Menu action failed","The requested action could not be completed.",DateTime.UtcNow));}}}
    private async void QuickHold_PointerPressed(object sender,PointerRoutedEventArgs e){if(sender is Button{Tag:string id}){e.Handled=true;if(_holdInputActive)return;_holdInputActive=true;await ExecuteQuickHoldAsync(id);}}
    private void QuickHold_PointerReleased(object sender,PointerRoutedEventArgs e){EndQuickHoldInput();e.Handled=true;}
    private async void QuickHold_KeyDown(object sender,KeyRoutedEventArgs e){if(e.Key is not (VirtualKey.Enter or VirtualKey.Space or VirtualKey.GamepadA)||sender is not Button{Tag:string id})return;e.Handled=true;if(_holdInputActive)return;_holdInputActive=true;await ExecuteQuickHoldAsync(id);}
    private void QuickHold_KeyUp(object sender,KeyRoutedEventArgs e){if(e.Key is not (VirtualKey.Enter or VirtualKey.Space or VirtualKey.GamepadA))return;e.Handled=true;EndQuickHoldInput();}
    private async Task ExecuteQuickHoldAsync(string id)
    {
        if(_guestMode.IsGuest&&!_guestSettings.AllowQuickMenuPowerActions){ViewModel.ReportStatus("Power actions are disabled in Guest Mode.");return;}
        PendingQuickMenuConfirmation? pending=_quickMenu.State.PendingConfirmation;
        bool showProgress=pending is{RequiresHold:true}&&pending.ActionId==id;
        if(showProgress)BeginHoldProgress(pending!.HoldDurationMilliseconds);
        try{await _quickMenu.ExecuteActionAsync(id);}catch(Exception ex){ViewModel.ReportStatus(ex.Message);_notifications.Publish(new(Guid.NewGuid().ToString("N"),NotificationType.Error,"Power action failed","The requested power action could not be completed.",DateTime.UtcNow));}finally{if(showProgress)StopHoldProgress();}
    }
    private void BeginHoldProgress(int durationMilliseconds)
    {
        StopHoldProgress();_holdProgressStartedUtc=DateTime.UtcNow;_holdProgressDurationMilliseconds=Math.Max(1,durationMilliseconds);QuickHoldProgress.Value=0;QuickHoldProgress.Visibility=Visibility.Visible;_holdProgressTimer=DispatcherQueue.CreateTimer();_holdProgressTimer.Interval=TimeSpan.FromMilliseconds(50);_holdProgressTimer.Tick+=HoldProgress_Tick;_holdProgressTimer.Start();
    }
    private void HoldProgress_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender,object args)=>QuickHoldProgress.Value=Math.Clamp((DateTime.UtcNow-_holdProgressStartedUtc).TotalMilliseconds/_holdProgressDurationMilliseconds*100d,0d,100d);
    private void StopHoldProgress(){if(_holdProgressTimer is not null){_holdProgressTimer.Stop();_holdProgressTimer.Tick-=HoldProgress_Tick;_holdProgressTimer=null;}if(_xamlInitialized){QuickHoldProgress.Value=0;QuickHoldProgress.Visibility=Visibility.Collapsed;}}
    private void EndQuickHoldInput(){_holdInputActive=false;_holdSafety.OnButtonReleased();StopHoldProgress();}
    private void QuickConfirmation_Close(InfoBar sender,object args)=>_quickMenu.CancelPendingAction();
    private async void QuickVolumeSlider_ValueChanged(object sender,RangeBaseValueChangedEventArgs e){if(!_xamlInitialized||!_isLoaded||_updatingQuickVolume||!_quickMenu.IsOpen)return;try{await _volume.SetVolumeAsync(e.NewValue/100d);}catch(Exception ex){ViewModel.ReportStatus($"Volume could not be changed: {ex.Message}");}}
    private void OnQuickMenuStateChanged(object? sender,QuickMenuStateChangedEventArgs e)=>DispatcherQueue.TryEnqueue(()=>ApplyQuickMenuState(e.State));
    private void ApplyQuickMenuState(QuickMenuState state)
    {
        QuickMenuOverlay.Visibility=state.IsOpen?Visibility.Visible:Visibility.Collapsed;_inputContext.SetCurrent(CurrentInputContext());if(!state.IsOpen)return;
        QuickActivityText.Text=state.CurrentActivity.Duration is null?state.CurrentActivity.Title:$"{state.CurrentActivity.Title} · {state.CurrentActivity.Duration.Value:hh\\:mm\\:ss}";
        QuickTimeText.Text=$"{_time.Current.DisplayTime}  {_time.Current.DisplayDate}";QuickAudioDeviceText.Text=state.AudioStatus.Available?$"{state.AudioStatus.EndpointName} · {(state.AudioStatus.Muted?"Muted":$"{state.AudioStatus.Volume:P0}")}":$"Audio unavailable: {state.AudioStatus.FailureReason}";
        _updatingQuickVolume=true;QuickVolumeSlider.Value=state.AudioStatus.Volume*100;_updatingQuickVolume=false;QuickVolumeSlider.IsEnabled=state.AudioStatus.Available;
        QuickControllerText.Text=state.ControllerStatus.Count==0?"No controller connected":string.Join(", ",state.ControllerStatus.Select(x=>$"{x.DisplayName} · {x.BatteryState}"));QuickNetworkText.Text=state.NetworkStatus.NetworkAvailable?$"{state.NetworkStatus.ConnectionType??"Connected"} · {state.NetworkStatus.InternetAccessLevel}":"No network connection";
        QuickConfirmationBar.IsOpen=state.PendingConfirmation is not null;QuickConfirmationBar.Message=state.PendingConfirmation is null?string.Empty:state.PendingConfirmation.RequiresHold?$"Press again and hold {state.PendingConfirmation.HoldDurationMilliseconds/1000d:0.0} seconds to confirm {state.PendingConfirmation.Label}.":$"Press {state.PendingConfirmation.Label} again to confirm.";
        QuickNotificationsText.Text=_notifications.Items.Count==0?"No NexaStation notifications":string.Join(Environment.NewLine,_notifications.Items.Take(4).Select(x=>$"{x.Title}: {x.Message}"));
        FrameworkElement? focused=FocusManager.GetFocusedElement(Root.XamlRoot) as FrameworkElement;if(state.IsOpen&&(focused is null||!IsDescendantOf(focused,QuickMenuOverlay)))QuickMenuOverlay.Focus(FocusState.Programmatic);
    }
    private static bool IsDescendantOf(DependencyObject child,DependencyObject parent){DependencyObject? current=child;while(current is not null){if(ReferenceEquals(current,parent))return true;current=VisualTreeHelper.GetParent(current);}return false;}
    private async void OnWindowActivated(object sender,Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if(args.WindowActivationState==Microsoft.UI.Xaml.WindowActivationState.Deactivated){ViewModel.PauseBackgroundPresentation();_holdSafety.OnLostForeground();if(_quickMenu.IsOpen)_quickMenu.CancelPendingAction();return;}
        if(_isLoaded&&DiscoverAppsViewModel.HasPendingInstallCheck)await DiscoverAppsViewModel.RefreshAfterReturnAsync();
    }
    private void OnDiscoverStatusChanged(string message)=>DispatcherQueue.TryEnqueue(()=>ViewModel.ReportStatus(message));
    private async void DiscoverCategory_Click(object sender,RoutedEventArgs e){if(sender is FrameworkElement{Tag:string category})await DiscoverAppsViewModel.SelectCategoryAsync(category);}
    private void AppsMode_Click(object sender,RoutedEventArgs e){bool discover=sender is FrameworkElement{Tag:string value}&&value=="Discover";InstalledAppsModeButton.IsChecked=!discover;DiscoverAppsModeButton.IsChecked=discover;InstalledAppsPanel.Visibility=discover?Visibility.Collapsed:Visibility.Visible;DiscoverAppsPanel.Visibility=discover?Visibility.Visible:Visibility.Collapsed;}
    private void ShowDiscoverApps_Click(object sender,RoutedEventArgs e){AppsMode_Click(DiscoverAppsModeButton,e);if(ViewModel.SelectedSection.Section!=NavigationSection.Apps){ViewModel.SelectSection(ViewModel.Sections.First(section=>section.Section==NavigationSection.Apps));UpdateSectionSurface();}}
    private void ViewAllGames_Click(object sender,RoutedEventArgs e)=>NavigateToSection(NavigationSection.Games);
    private void ViewAllApps_Click(object sender,RoutedEventArgs e)=>NavigateToSection(NavigationSection.Apps);
    private void ViewAllPhotos_Click(object sender,RoutedEventArgs e)=>NavigateToSection(NavigationSection.Gallery);
    private void ViewAllMedia_Click(object sender,RoutedEventArgs e)=>NavigateToSection(NavigationSection.Media);
    private void ManageFavorites_Click(object sender,RoutedEventArgs e){ViewModel.SetLibraryView("Favorites");NavigateToSection(NavigationSection.Library);}
    private void ViewAllRecent_Click(object sender,RoutedEventArgs e){ViewModel.SetLibraryView("Recently Added");NavigateToSection(NavigationSection.Library);}
    public void NavigateToSection(string section)
    {
        if (Enum.TryParse(section, true, out NavigationSection target) && ViewModel.Sections.Any(value => value.Section == target))
        {
            NavigateToSection(target);
        }
    }
    private void NavigateToSection(NavigationSection section){ViewModel.SelectSection(ViewModel.Sections.First(value=>value.Section==section));UpdateSectionSurface();DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,FocusSelectedSectionSurface);_=_sounds.PlayAsync(UiSoundEvent.Navigate);}
    private async void DiscoverCard_ItemClick(object sender,ItemClickEventArgs e){if(e.ClickedItem is DiscoverAppCardViewModel card)await DiscoverAppsViewModel.ExecutePrimaryAsync(card);}
    private async void DiscoverPrimary_Click(object sender,RoutedEventArgs e){if(sender is FrameworkElement{DataContext:DiscoverAppCardViewModel card})await DiscoverAppsViewModel.ExecutePrimaryAsync(card);}
    private async void DiscoverDetails_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not FrameworkElement{DataContext:DiscoverAppCardViewModel card})return;
        await Dialog(card.DisplayName,new TextBlock{Text=$"{card.Description}\n\nCategory: {card.Category}\nState: {card.StateBadge}\nIdentity: {card.State.MatchedIdentity??"No exact installed identity matched"}\nStore Product ID: {card.Definition.StoreProductId??"Not configured"}\nWeb fallback: {card.Definition.WebFallbackUri?.AbsoluteUri??"Not configured"}",TextWrapping=TextWrapping.Wrap},"Close",null,string.Empty).ShowAsync();
    }
    private sealed record ProviderDiagnostic(string Provider,string Operation,string FailedEntity,string Constraint,string SuggestedRecovery,string CorrelationId);
    private async void OnControllerStatusChanged(object? sender,ControllerStatusChangedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(()=>HeaderControllerStatus.Text=args.Controllers.Count==0?"LOCAL":$"P{args.Controllers.FirstOrDefault(controller=>controller.ActiveInputDevice)?.PlayerIndex??1}");
        if(args.Controllers.Count>0&&_inputService is IInputCompatibilitySink inputCompatibility)
        {
            try
            {
                ControllerStatus controller=args.Controllers[0];
                CompatibilityResolutionResult resolution=await ControllerCompatibilityPolicy.ResolveAsync(_compatibilityResolver,new(controller.ControllerFamily,ConnectionType:controller.Wireless==true?"wireless":controller.Wireless==false?"wired":null));
                InputCompatibilityOverrides input=ControllerCompatibilityPolicy.EffectiveInput(resolution);
                inputCompatibility.ApplyCompatibility(new(input.NavigationDeadzone??0.55,input.DisableAnalogNavigation==true,input.UseDpadOnly==true,input.RepeatDelayMilliseconds??350,input.RepeatRateMilliseconds??120));
                string matchKey=$"family:{controller.ControllerFamily.ToLowerInvariant()}";ControllerPreference? preference=await _controllerPreferences.GetAsync(matchKey);
                if(preference is not null)
                {
                    inputCompatibility.ApplyCompatibility(new(preference.Deadzone,!preference.AnalogNavigationEnabled,!preference.AnalogNavigationEnabled,preference.RepeatDelayMilliseconds,preference.RepeatRateMilliseconds));
                    if(Enum.TryParse(preference.PreferredGlyphSetId,true,out PromptStyle promptStyle))_promptPreferences.Apply(promptStyle,(NintendoConfirmConvention)preference.ConfirmConvention);
                }
            }
            catch(Exception exception){_logger.LogError("ControllerCompatibilityFailed","A controller compatibility profile could not be applied; safe defaults remain active.",exception);}
        }
        if(args.Controllers.Count==0){_navigationAudit.Record(NavigationAuditEventType.ControllerDisconnected,ViewModel.SelectedSection.Id,detail:"The active controller disconnected.");_holdSafety.OnControllerDisconnected();if(_quickMenu.IsOpen)_quickMenu.CancelPendingAction();_notifications.Publish(new(Guid.NewGuid().ToString("N"),NotificationType.Warning,"Controller disconnected","A controller was disconnected.",DateTime.UtcNow));}
    }
    private void OnTimeChanged(object? sender,LocalTimeChangedEventArgs args)=>DispatcherQueue.TryEnqueue(()=>HeaderClock.Text=args.Snapshot.DisplayTime);
    private async Task RefreshHeaderControllerAsync()
    {
        try{IReadOnlyList<ControllerStatus> controllers=await _controllerStatus.GetControllersAsync();HeaderControllerStatus.Text=controllers.Count==0?"LOCAL":$"P{controllers.FirstOrDefault(controller=>controller.ActiveInputDevice)?.PlayerIndex??1}";}catch(Exception){HeaderControllerStatus.Text="LOCAL";}
    }
    private void OnNotificationsChanged(object? sender,EventArgs e){if(_quickMenu.IsOpen)DispatcherQueue.TryEnqueue(()=>ApplyQuickMenuState(_quickMenu.State));}

    private void MoveFocus(FocusNavigationDirection direction)
    {
        string? before=SemanticFocusId(FocusManager.GetFocusedElement(Root.XamlRoot));
        if(TryMoveFocusWithinItems(direction))
        {
            string? itemAfter=SemanticFocusId(FocusManager.GetFocusedElement(Root.XamlRoot));
            _navigationAudit.Record(NavigationAuditEventType.FocusMoved,ViewModel.SelectedSection.Id,before,itemAfter,direction.ToString());
            return;
        }
        bool moved = FocusManager.TryMoveFocus(
            direction,
            new FindNextElementOptions { SearchRoot = Root, XYFocusNavigationStrategyOverride = XYFocusNavigationStrategyOverride.Projection });
        if (!moved)
        {
            _navigationAudit.Record(NavigationAuditEventType.InvalidDirectionalTarget,ViewModel.SelectedSection.Id,before,null,direction.ToString());
            FocusRememberedOrFirstCard();
        }
        string? after=SemanticFocusId(FocusManager.GetFocusedElement(Root.XamlRoot));
        _navigationAudit.Record(after is null?NavigationAuditEventType.FocusMissing:NavigationAuditEventType.FocusMoved,ViewModel.SelectedSection.Id,before,after,direction.ToString());
    }

    private bool TryMoveFocusWithinItems(FocusNavigationDirection direction)
    {
        DependencyObject? focused=FocusManager.GetFocusedElement(Root.XamlRoot) as DependencyObject;
        SelectorItem? container=FindAncestor<SelectorItem>(focused);if(container is null)return false;
        if(ItemsControl.ItemsControlFromItemContainer(container) is not ListViewBase owner)return false;
        int current=owner.IndexFromContainer(container);if(current<0)return false;
        int target=current;
        if(owner is GridView)
        {
            int columns=RealizedColumnCount(owner);
            int column=current%columns;
            target=direction switch
            {
                FocusNavigationDirection.Left when column>0=>current-1,
                FocusNavigationDirection.Right when column<columns-1=>current+1,
                FocusNavigationDirection.Up=>current-columns,
                FocusNavigationDirection.Down=>current+columns,
                _=>current,
            };
        }
        else if(owner.ItemsPanelRoot is ItemsStackPanel{Orientation:Orientation.Horizontal})
        {
            target=direction switch{FocusNavigationDirection.Left=>current-1,FocusNavigationDirection.Right=>current+1,_=>current};
            if(direction is FocusNavigationDirection.Up or FocusNavigationDirection.Down)return false;
        }
        else
        {
            target=direction switch{FocusNavigationDirection.Up=>current-1,FocusNavigationDirection.Down=>current+1,_=>current};
            if(direction is FocusNavigationDirection.Left or FocusNavigationDirection.Right)return false;
        }
        if(target==current||target<0||target>=owner.Items.Count)return true;
        object item=owner.Items[target];owner.ScrollIntoView(item,ScrollIntoViewAlignment.Default);owner.UpdateLayout();
        if(owner.ContainerFromIndex(target) is Control realized&&realized.Focus(FocusState.Keyboard))return true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,()=>
        {
            owner.UpdateLayout();if(owner.ContainerFromIndex(target) is Control pending)pending.Focus(FocusState.Keyboard);
        });
        return true;
    }

    private static int RealizedColumnCount(ListViewBase owner)
    {
        List<double> tops=[];
        for(int index=0;index<owner.Items.Count;index++)
        {
            if(owner.ContainerFromIndex(index) is not FrameworkElement element)continue;
            try{tops.Add(element.TransformToVisual(owner).TransformPoint(new Windows.Foundation.Point()).Y);}catch(InvalidOperationException){}
        }
        if(tops.Count==0)return 1;double first=tops[0];int columns=tops.Count(value=>Math.Abs(value-first)<8);return Math.Max(1,columns);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T:DependencyObject
    {
        for(DependencyObject? current=source;current is not null;current=VisualTreeHelper.GetParent(current))if(current is T match)return match;
        return null;
    }

    private void OnFocusRestoreRequested(object? sender, string? itemId)
    {
        DispatcherQueue.TryEnqueue(() => FocusCard(itemId));
    }

    private void OnGalleryFocusRestoreRequested(object? sender,string? itemId)=>DispatcherQueue.TryEnqueue(()=>FocusGalleryCard(itemId));

    private void FocusRememberedOrFirstCard()
    {
        if(ViewModel.SelectedSection.Section==NavigationSection.Gallery){FocusGalleryCard(_galleryFocusId??GalleryViewModel.Rows.SelectMany(row=>row.Items).FirstOrDefault()?.Id);return;}
        string? itemId = ViewModel.Rows
            .SelectMany(row => row.Items)
            .Select(card => card.Id)
            .FirstOrDefault();
        FocusCard(itemId);
    }

    private void FocusSelectedSectionSurface()
    {
        // Page visibility and the GridView's virtualized panel settle asynchronously.
        // Focusing the surface avoids forcing container realization during that layout pass;
        // directional input moves into the first realized card naturally.
        FrameworkElement target=_activeLayout==InterfaceLayoutType.ClassicCrossbar?ViewModel.SelectedSection.Section switch
        {
            NavigationSection.Gallery=>CrossbarGalleryContent,
            NavigationSection.Media=>CrossbarMediaContent,
            NavigationSection.Collections=>CrossbarCollectionsContent,
            _=>CrossbarLibraryContent,
        }:ViewModel.SelectedSection.Section switch
        {
            NavigationSection.Gallery=>GalleryRoot,
            NavigationSection.Media=>MediaRoot,
            NavigationSection.Collections=>CollectionsRoot,
            NavigationSection.Apps=>AppsCatalogueRoot,
            NavigationSection.Home=>ModernHomeSurface,
            _=>SectionAdaptiveGrid,
        };
        target.Focus(FocusState.Programmatic);
    }

    private void FocusGalleryCard(string? itemId)
    {
        FrameworkElement surface=_activeLayout==InterfaceLayoutType.ClassicCrossbar?CrossbarGalleryContent:GalleryRoot;GalleryCardViewModel? card=GalleryViewModel.Rows.SelectMany(row=>row.Items).FirstOrDefault(value=>value.Id==itemId)??GalleryViewModel.Rows.SelectMany(row=>row.Items).FirstOrDefault();if(card is null){surface.Focus(FocusState.Programmatic);return;}ListView? list=FindOwningGalleryListView(surface,card);if(list is null){surface.Focus(FocusState.Programmatic);return;}list.ScrollIntoView(card,ScrollIntoViewAlignment.Leading);list.UpdateLayout();if(list.ContainerFromItem(card) is ListViewItem container)container.Focus(FocusState.Keyboard);
    }
    private static ListView? FindOwningGalleryListView(DependencyObject parent,GalleryCardViewModel card){int count=VisualTreeHelper.GetChildrenCount(parent);for(int index=0;index<count;index++){DependencyObject child=VisualTreeHelper.GetChild(parent,index);if(child is ListView list&&list.Items.Contains(card))return list;ListView? match=FindOwningGalleryListView(child,card);if(match is not null)return match;}return null;}

    private void FocusCard(string? itemId) => FocusCard(itemId, 0);

    private void FocusCard(string? itemId, int attempt)
    {
        CardViewModel? card = ViewModel.Rows
            .SelectMany(row => row.Items)
            .FirstOrDefault(candidate => candidate.Id == itemId)
            ?? ViewModel.Rows.SelectMany(row => row.Items).FirstOrDefault();
        if (card is null)
        {
            Root.Focus(FocusState.Programmatic);
            return;
        }

        ListViewBase? listView = FindOwningListView(Root, card);
        if (listView is null)
        {
            if (attempt < 4)
            {
                _ = RetryFocusAsync(itemId, attempt + 1);
                return;
            }
            Root.Focus(FocusState.Programmatic);
            _logger.LogError(
                "FocusRestoreFailed",
                "The selected card's list was not available.",
                properties: new Dictionary<string, object?> { ["LibraryItemId"] = card.Id });
            _navigationAudit.Record(NavigationAuditEventType.FocusMissing,ViewModel.SelectedSection.Id,detail:"Selected card list was unavailable after bounded retries.");
            return;
        }

        listView.ScrollIntoView(card, ScrollIntoViewAlignment.Leading);
        if (!TryFocusContainer(listView, card))
        {
            if (attempt < 4)
            {
                _ = RetryFocusAsync(itemId, attempt + 1);
                return;
            }

            Root.Focus(FocusState.Programmatic);
            _logger.LogError(
                "FocusRestoreFailed",
                "The selected card container could not be realized.",
                properties: new Dictionary<string, object?> { ["LibraryItemId"] = card.Id });
            _navigationAudit.Record(NavigationAuditEventType.FocusMissing,ViewModel.SelectedSection.Id,detail:"Selected card container was unavailable after bounded retries.");
        }
    }

    private static string? SemanticFocusId(object? value)=>value switch{FrameworkElement element when !string.IsNullOrWhiteSpace(element.Name)=>element.Name,FrameworkElement element=>element.GetType().Name,_=>null};

    private async Task RetryFocusAsync(string? itemId, int attempt)
    {
        await Task.Delay(100 * attempt);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => FocusCard(itemId, attempt));
    }

    private bool TryFocusContainer(ListViewBase listView, CardViewModel card)
    {
        if (listView.ContainerFromItem(card) is not Control container)
        {
            return false;
        }

        bool focused = container.Focus(FocusState.Keyboard);
        _logger.Information(
            "FocusRestored",
            "Dashboard focus was applied to the selected card.",
            new Dictionary<string, object?>
            {
                ["LibraryItemId"] = card.Id,
                ["Section"] = ViewModel.SelectedSection.Id,
                ["Row"] = FindRowIndex(card),
                ["Focused"] = focused,
            });
        return focused;
    }

    private static ListViewBase? FindOwningListView(DependencyObject parent, CardViewModel card)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is ListViewBase listView && listView.Items.Contains(card))
            {
                return listView;
            }

            ListViewBase? match = FindOwningListView(child, card);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private int FindRowIndex(CardViewModel card)
    {
        for (int index = 0; index < ViewModel.Rows.Count; index++)
        {
            if (ViewModel.Rows[index].Items.Contains(card))
            {
                return index;
            }
        }

        return 0;
    }

    private static bool AreControlAndShiftDown() =>
        IsKeyDown(VirtualKey.Control) && IsKeyDown(VirtualKey.Shift);

    private static bool IsKeyDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _inputService.ActionTriggered -= OnInputActionTriggered;
        ViewModel.FocusRestoreRequested -= OnFocusRestoreRequested;
        GalleryViewModel.FocusRestoreRequested -= OnGalleryFocusRestoreRequested;
        _slideshow.ItemChanged -= OnSlideshowItemChanged;
        _quickMenu.StateChanged -= OnQuickMenuStateChanged;
        _controllerStatus.StatusChanged -= OnControllerStatusChanged;
        _time.TimeChanged -= OnTimeChanged;
        _notifications.Changed -= OnNotificationsChanged;
        _presentation.PresentationChanged -= OnPresentationChanged;
        _themes.ThemeChanged -= OnThemeChanged;
        _guestMode.ProfileChanged -= OnFrontendProfileChanged;
        DiscoverAppsViewModel.StatusChanged-=OnDiscoverStatusChanged;
        DiscoverAppsViewModel.StreamingItems.CollectionChanged-=OnHomeCollectionChanged;
        GalleryViewModel.HomePreview.CollectionChanged-=OnHomeCollectionChanged;
        _globalHotkeys.Pressed-=OnGlobalHotkeyPressed;
        NativePlayerPage.CloseRequested-=OnNativePlayerCloseRequested;
        NativePlayerPage.OpenWithRequested-=OnNativePlayerOpenWithRequested;
        NativePlayerPage.QueueMoveRequested-=OnNativePlayerQueueMoveRequested;
        NativePlayerPage.Dispose();
        _captureConsent.RevokeAll();
        _globalHotkeys.Dispose();
        Activated -= OnWindowActivated;
        _viewerLoad?.Cancel();
        _viewerLoad?.Dispose();
        _artworkJobMonitor?.Cancel();
        _artworkJobMonitor?.Dispose();
        _logger.Information("MainWindowClosed", "The main window closed.");
    }
}
