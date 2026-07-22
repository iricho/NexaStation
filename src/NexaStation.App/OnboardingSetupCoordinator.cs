using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Domain;
using NexaStation.EmulatorDiscovery.Discovery;
using NexaStation.Onboarding;
using NexaStation.Profiles.Sessions;
using NexaStation.SystemIntegration.Services;

namespace NexaStation.App;

public sealed record OnboardingSourcePreview(string Id, string Title, int CandidateCount, IReadOnlyList<string> CandidateNames, string Message, bool Succeeded);

public sealed class OnboardingSetupCoordinator(
    IProviderRegistry providers,
    IProviderImportService providerImports,
    IWindowsApplicationLibraryService windowsApplications,
    IEmulatorDiscoveryService emulatorDiscovery,
    IEmulationLibraryService emulationLibrary,
    IGalleryPersistence galleryPersistence,
    IGalleryLibraryService galleryLibrary,
    IFrontendProfileStore profiles,
    IInterfaceLayoutService layouts,
    IThemeService themes,
    IPerformancePresetService performance,
    ISettingsService settings,
    IStartupTaskService startupTask)
{
    private static readonly string[] OtherLauncherIds = ["epic", "gog", "ubisoft", "ea", "battlenet"];
    private readonly Dictionary<string, (ProviderScanResult Scan, ImportPreview Preview)> _providerPreviews = new(StringComparer.OrdinalIgnoreCase);
    private WindowsApplicationImportPreview? _windowsPreview;
    private EmulatorDiscoveryResult? _emulatorPreview;

    public async Task<OnboardingPreferences> SeedUpgradePreferencesAsync(OnboardingPreferences draft, CancellationToken cancellationToken = default)
    {
        FrontendProfile owner = await profiles.GetOwnerAsync(cancellationToken);
        PerformanceSettings currentPerformance = await performance.GetAsync(cancellationToken);
        IReadOnlyList<GallerySource> gallerySources = await galleryPersistence.GetSourcesAsync(cancellationToken);
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        GallerySource? pictureSource = string.IsNullOrWhiteSpace(pictures) ? null : gallerySources.FirstOrDefault(source => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.FolderPath)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(pictures)), StringComparison.OrdinalIgnoreCase));
        return draft with
        {
            OwnerName = owner.Name, AvatarKey = owner.AvatarKey ?? "owner",
            Layout = layouts.CurrentLayoutId switch { "classic-crossbar" => "Classic", "compact-handheld" => "Compact", _ => "Modern" },
            ThemeId = themes.CurrentThemeId,
            PerformanceProfile = currentPerformance.Preset switch { VisualPerformancePreset.Cinematic => "Quality", VisualPerformancePreset.Minimal => "Efficient", _ => "Balanced" },
            GallerySetupRequested = gallerySources.Count > 0,
            IncludePicturesFolder = pictureSource is not null,
            PicturesPrivate = pictureSource?.PrivacyLevel == GalleryPrivacyLevel.Private,
            ShowPicturesOnHome = pictureSource?.ShowOnHome ?? true,
            PicturesIncludeSubfolders = pictureSource?.IncludeSubfolders ?? true,
            StartWithWindows = await settings.GetAsync("Startup.Enabled", false, cancellationToken),
            DiagnosticsConsent = await settings.GetAsync("Privacy.DiagnosticsExportIncludesLogs", true, cancellationToken),
        };
    }

    public async Task<OnboardingSourcePreview> PreviewAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return id switch
            {
                "steam" => await PreviewProviderAsync("steam", "Steam", cancellationToken),
                "launchers" => await PreviewLaunchersAsync(cancellationToken),
                "windows" => await PreviewWindowsAsync(cancellationToken),
                "emulation" => await PreviewEmulationAsync(cancellationToken),
                _ => new(id, id, 0, [], "This source is not supported.", false),
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return new(id, SourceTitle(id), 0, [], $"Scan failed: {exception.Message}", false);
        }
    }

    public async Task<IReadOnlyList<OnboardingTaskResult>> ApplyAsync(OnboardingPreferences preferences, IProgress<string>? progress = null, Func<OnboardingTaskResult, Task>? taskCompleted = null, CancellationToken cancellationToken = default)
    {
        List<OnboardingTaskResult> results = [];
        await RunAsync("profile", "Saving local profile", async () =>
        {
            FrontendProfile current = await profiles.GetOwnerAsync(cancellationToken);
            await profiles.SaveOwnerAsync(current with
            {
                Name = preferences.OwnerName.Trim(), AvatarKey = preferences.AvatarKey,
                ThemeOverrideId = preferences.ThemeId, UpdatedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);
            return $"Owner profile saved as {preferences.OwnerName.Trim()}.";
        });
        await RunAsync("appearance", "Applying appearance", async () =>
        {
            string layoutId = preferences.Layout switch { "Classic" => "classic-crossbar", "Compact" => "compact-handheld", _ => "modern-dashboard" };
            await layouts.ApplyAsync(layoutId, cancellationToken);
            if ((await themes.GetAvailableAsync(cancellationToken)).Any(theme => string.Equals(theme.Id, preferences.ThemeId, StringComparison.OrdinalIgnoreCase)))
                await themes.ApplyAsync(preferences.ThemeId, cancellationToken);
            VisualPerformancePreset preset = preferences.PerformanceProfile switch { "Quality" => VisualPerformancePreset.Cinematic, "Efficient" => VisualPerformancePreset.Minimal, _ => VisualPerformancePreset.Balanced };
            await performance.ApplyAsync(preset, cancellationToken: cancellationToken);
            return "Appearance choices applied.";
        });

        if (preferences.SteamDecision == OnboardingDecisionState.OptedIn)
            await RunAsync("steam", "Importing Steam library", () => ImportProviderAsync("steam", cancellationToken));
        else await AddResultAsync(Skipped("steam", "Steam was explicitly skipped."));

        if (preferences.OtherLaunchersDecision == OnboardingDecisionState.OptedIn)
            await RunAsync("launchers", "Importing launcher libraries", () => ImportLaunchersAsync(cancellationToken));
        else await AddResultAsync(Skipped("launchers", "Other launchers were explicitly skipped."));

        if (preferences.WindowsAppsDecision == OnboardingDecisionState.OptedIn)
            await RunAsync("windows", "Importing Windows applications", () => ImportWindowsAsync(cancellationToken));
        else await AddResultAsync(Skipped("windows", "Windows applications were explicitly skipped."));

        if (preferences.EmulationDecision == OnboardingDecisionState.OptedIn)
            await RunAsync("emulation", "Adding detected emulators", () => ImportEmulatorsAsync(cancellationToken));
        else await AddResultAsync(Skipped("emulation", "Emulation setup was explicitly skipped."));

        if (preferences.GalleryDecision == OnboardingDecisionState.OptedIn)
            await RunAsync("gallery", "Applying Gallery choices", async () =>
            {
                bool includePictures = preferences.PicturesDecision == OnboardingDecisionState.OptedIn;
                await settings.SetAsync("Gallery.PicturesProposalState", includePictures ? "Accepted" : "Declined", cancellationToken);
                if (!includePictures) return "Gallery enabled without adding the Pictures folder.";
                string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrWhiteSpace(pictures) || !Directory.Exists(pictures)) return "The Windows Pictures folder is unavailable; no source was added.";
                string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(pictures));
                IReadOnlyList<GallerySource> existing = await galleryPersistence.GetSourcesAsync(cancellationToken);
                if (existing.Any(source => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.FolderPath)), normalized, StringComparison.OrdinalIgnoreCase)))
                    return "The existing Pictures source was kept without duplication.";
                DateTime now = DateTime.UtcNow;
                await galleryLibrary.AddSourceAsync(new()
                {
                    Id = Guid.NewGuid().ToString("N"), Name = "Pictures", FolderPath = pictures, NormalizedFolderPath = normalized,
                    SourceKind = GallerySourceKind.Photos, IncludeSubfolders = preferences.PicturesIncludeSubfolders,
                    Enabled = true, WatchForChanges = true, IncludeImages = true, IncludeVideos = true,
                    PrivacyLevel = preferences.PicturesPrivate ? GalleryPrivacyLevel.Private : GalleryPrivacyLevel.Normal,
                    ShowOnHome = preferences.ShowPicturesOnHome, CreatedAtUtc = now, UpdatedAtUtc = now,
                }, true, true, cancellationToken);
                return "Pictures was added and scanned without modifying source files.";
            });
        else await AddResultAsync(Skipped("gallery", "Gallery setup was explicitly skipped."));

        await RunAsync("system", "Saving system choices", async () =>
        {
            string startupMessage = "Existing launch-at-sign-in setting kept.";
            if (preferences.StartupDecision != OnboardingDecisionState.KeepExisting)
            {
                bool startup = preferences.StartupDecision == OnboardingDecisionState.OptedIn;
                StartupTaskStatus startupStatus = await startupTask.SetEnabledAsync(startup, cancellationToken);
                await settings.SetAsync("Startup.Enabled", startupStatus.Availability == StartupTaskAvailability.Enabled, cancellationToken);
                startupMessage = startupStatus.Message;
            }
            if (preferences.DiagnosticsDecision != OnboardingDecisionState.KeepExisting)
                await settings.SetAsync("Privacy.DiagnosticsExportIncludesLogs", preferences.DiagnosticsDecision == OnboardingDecisionState.OptedIn, cancellationToken);
            await settings.SetAsync("Onboarding.Pending.SteamImport", false, cancellationToken);
            await settings.SetAsync("Onboarding.Pending.EmulationSetup", false, cancellationToken);
            await settings.SetAsync("Onboarding.Pending.WindowsAppScan", false, cancellationToken);
            await settings.SetAsync("Onboarding.Pending.GallerySetup", false, cancellationToken);
            return startupMessage;
        });
        return results;

        async Task RunAsync(string id, string message, Func<Task<string>> operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(message);
            try { await AddResultAsync(new(id, OnboardingTaskStatus.Succeeded, await operation())); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { await AddResultAsync(new(id, OnboardingTaskStatus.Failed, null, exception.Message)); }
        }

        async Task AddResultAsync(OnboardingTaskResult result)
        {
            results.RemoveAll(existing => string.Equals(existing.Id, result.Id, StringComparison.OrdinalIgnoreCase));
            results.Add(result);
            if (taskCompleted is not null) await taskCompleted(result);
        }
    }

    private async Task<OnboardingSourcePreview> PreviewProviderAsync(string id, string title, CancellationToken token)
    {
        ILibraryProvider provider = providers.GetById(id) ?? throw new InvalidOperationException($"{title} is not registered.");
        ProviderAvailabilityResult availability = await provider.DetectAsync(token);
        if (!availability.Detected && availability.RequiresUserConfiguration)
            return new(id, title, 0, [], availability.DisplayMessage, false);
        ProviderScanResult scan = await provider.ScanAsync(new(FullRefresh: true, ConfiguredInstallationPath: availability.InstallationPath), token);
        ImportPreview preview = await providerImports.CreatePreviewAsync(id, scan, token);
        _providerPreviews[id] = (scan, preview);
        string[] names = preview.Candidates.Where(candidate => candidate.Status is ImportCandidateStatus.New or ImportCandidateStatus.Changed).Select(candidate => candidate.Item.Title).Take(100).ToArray();
        return new(id, title, names.Length, names, $"{names.Length} importable item(s) found.", scan.Errors.Count == 0 || names.Length > 0);
    }

    private async Task<OnboardingSourcePreview> PreviewLaunchersAsync(CancellationToken token)
    {
        List<string> names = [];
        foreach (string id in OtherLauncherIds)
        {
            ILibraryProvider? provider = providers.GetById(id); if (provider is null) continue;
            ProviderScanResult scan = await provider.ScanAsync(new(), token);
            ImportPreview preview = await providerImports.CreatePreviewAsync(id, scan, token);
            _providerPreviews[id] = (scan, preview);
            names.AddRange(preview.Candidates.Where(candidate => candidate.Status is ImportCandidateStatus.New or ImportCandidateStatus.Changed).Select(candidate => candidate.Item.Title));
        }
        return new("launchers", "Other launchers", names.Count, names.Take(100).ToArray(), names.Count == 0 ? "No supported launcher games were found." : $"{names.Count} importable item(s) found.", true);
    }

    private async Task<OnboardingSourcePreview> PreviewWindowsAsync(CancellationToken token)
    {
        WindowsApplicationScanResult scan = await windowsApplications.ScanAllAsync(new(true, true, true), true, true, token);
        _windowsPreview = await windowsApplications.CreatePreviewAsync(scan, token);
        string[] names = _windowsPreview.Candidates.Where(candidate => candidate.SelectedByDefault).Select(candidate => candidate.Candidate.DisplayName).Take(100).ToArray();
        return new("windows", "Windows applications", names.Length, names, $"{names.Length} recommended application(s) found.", scan.Errors.Count == 0 || names.Length > 0);
    }

    private async Task<OnboardingSourcePreview> PreviewEmulationAsync(CancellationToken token)
    {
        _emulatorPreview = await emulatorDiscovery.DiscoverAsync(new() { RefreshMode = EmulatorDiscoveryRefreshMode.Refresh }, token);
        string[] names = _emulatorPreview.Candidates.Where(candidate => candidate.Status == EmulatorCandidateStatus.DetectedAndSupported).Select(candidate => candidate.DisplayName).Take(100).ToArray();
        return new("emulation", "Emulation", names.Length, names, names.Length == 0 ? "No supported emulator installations were found in bounded locations." : $"{names.Length} supported emulator(s) found.", true);
    }

    private async Task<string> ImportProviderAsync(string id, CancellationToken token)
    {
        if (!_providerPreviews.TryGetValue(id, out var value)) { await PreviewProviderAsync(id, providers.GetById(id)?.DisplayName ?? id, token); value = _providerPreviews[id]; }
        ImportSelection[] selections = value.Preview.Candidates.Where(candidate => candidate.SelectedByDefault).Select(candidate => new ImportSelection(candidate.Item, candidate.Status == ImportCandidateStatus.PossibleDuplicate && candidate.ExistingLibraryItemId is not null ? ImportSelectionAction.Link : ImportSelectionAction.Import, candidate.ExistingLibraryItemId)).ToArray();
        ProviderImportResult result = await providerImports.ImportAsync(id, selections, token);
        return $"{result.Summary.Added} added, {result.Summary.Updated} updated, {result.Summary.Failed} failed.";
    }

    private async Task<string> ImportLaunchersAsync(CancellationToken token)
    {
        if (!OtherLauncherIds.Any(id => _providerPreviews.ContainsKey(id))) await PreviewLaunchersAsync(token);
        int added = 0, updated = 0, failed = 0;
        foreach (string id in OtherLauncherIds.Where(id => _providerPreviews.ContainsKey(id)))
        {
            ImportPreview preview = _providerPreviews[id].Preview;
            ImportSelection[] selections = preview.Candidates.Where(candidate => candidate.SelectedByDefault)
                .Select(candidate => new ImportSelection(candidate.Item,
                    candidate.Status == ImportCandidateStatus.PossibleDuplicate && candidate.ExistingLibraryItemId is not null ? ImportSelectionAction.Link : ImportSelectionAction.Import,
                    candidate.ExistingLibraryItemId)).ToArray();
            ProviderImportResult result = await providerImports.ImportAsync(id, selections, token);
            added += result.Summary.Added; updated += result.Summary.Updated; failed += result.Summary.Failed;
        }
        return $"Launcher imports completed. {added} added, {updated} updated, {failed} failed.";
    }

    private async Task<string> ImportWindowsAsync(CancellationToken token)
    {
        if (_windowsPreview is null) await PreviewWindowsAsync(token);
        WindowsApplicationImportSelection[] selections = _windowsPreview!.Candidates.Where(candidate => candidate.SelectedByDefault).Select(candidate => new WindowsApplicationImportSelection(candidate, candidate.Status == WindowsImportStatus.PossibleDuplicate && candidate.ExistingLibraryItemId is not null ? WindowsImportAction.LinkToExisting : WindowsImportAction.Import, candidate.ExistingLibraryItemId)).ToArray();
        WindowsApplicationImportResult result = await windowsApplications.ImportAsync(selections, token);
        return $"{result.Added} added, {result.Updated + result.Linked} updated or linked, {result.Failed} failed.";
    }

    private async Task<string> ImportEmulatorsAsync(CancellationToken token)
    {
        if (_emulatorPreview is null) await PreviewEmulationAsync(token);
        int added = 0;
        foreach (EmulatorCandidate candidate in _emulatorPreview!.Candidates.Where(candidate => candidate.Status == EmulatorCandidateStatus.DetectedAndSupported && candidate.ExistingEmulatorId is null))
        {
            await emulationLibrary.AddInstallationAsync(candidate.DefinitionId, candidate.ExecutablePath, candidate.DisplayName, candidate.IsPortable, token);
            added++;
        }
        return $"{added} detected emulator installation(s) added. ROM folders still require explicit approval.";
    }

    private static OnboardingTaskResult Skipped(string id, string summary) => new(id, OnboardingTaskStatus.Skipped, summary);
    private static string SourceTitle(string id) => id switch { "steam" => "Steam", "launchers" => "Other launchers", "windows" => "Windows applications", "emulation" => "Emulation", _ => id };
}
