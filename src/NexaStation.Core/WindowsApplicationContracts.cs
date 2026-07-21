using NexaStation.Domain;

namespace NexaStation.Core;

public interface IStartMenuKnownFolderResolver { IReadOnlyList<(string ProviderSourceId, string Path)> Resolve(bool currentUser, bool common); }
public interface IShellLinkReader { Task<ShellLinkReadResult> ReadAsync(string shortcutPath, CancellationToken cancellationToken = default); }
public interface IStartMenuApplicationScanner { Task<WindowsApplicationScanResult> ScanAsync(StartMenuScanRequest request, CancellationToken cancellationToken = default); }
public interface IAppsFolderApplicationReader { Task<IReadOnlyList<AppsFolderApplication>> ReadAsync(CancellationToken cancellationToken = default); }
public interface IAppsFolderScanner { Task<WindowsApplicationScanResult> ScanAsync(CancellationToken cancellationToken = default); }
public interface IWindowsApplicationClassifier { WindowsApplicationClassification Classify(WindowsApplicationCandidate candidate); }
public interface IPwaDetector { PwaCandidate? Detect(WindowsApplicationCandidate candidate); }
public interface IPackagedApplicationActivator { Task<PackagedApplicationActivationResult> ActivateAsync(string appUserModelId, string? arguments, CancellationToken cancellationToken = default); }
public interface IWindowsApplicationActivationService { Task<WindowsActivationResult> ActivateAsync(WindowsApplicationLaunchRequest request, CancellationToken cancellationToken = default); }
public interface IApplicationIconExtractor { Task<ExtractedApplicationIcon?> ExtractAsync(ApplicationIconRequest request, CancellationToken cancellationToken = default); }
public interface IWindowsApplicationPersistence : IDesktopControlProfileProvider
{
    Task<WindowsApplicationSource?> FindSourceAsync(string providerId, string providerItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WindowsApplicationSource>> GetSourcesAsync(string? providerId = null, CancellationToken cancellationToken = default);
    Task<WindowsApplicationImportPreview> CreatePreviewAsync(IReadOnlyCollection<WindowsApplicationCandidate> candidates, IWindowsApplicationClassifier classifier, CancellationToken cancellationToken = default);
    Task<WindowsApplicationImportResult> ImportAsync(IReadOnlyCollection<WindowsApplicationImportSelection> selections, CancellationToken cancellationToken = default);
    Task ApplyRefreshAsync(WindowsApplicationScanResult scan, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task SetCategoriesAsync(string libraryItemId, IReadOnlyCollection<LibraryItemApplicationCategory> categories, CancellationToken cancellationToken = default);
    Task SetDesktopControlAssignmentAsync(DesktopControlAssignment? assignment, string libraryItemId, CancellationToken cancellationToken = default);
    Task SaveDesktopControlProfileAsync(DesktopControlProfile profile, CancellationToken cancellationToken = default);
    Task<WindowsApplicationSource?> GetSourceForItemAsync(string libraryItemId, CancellationToken cancellationToken = default);
}
public interface IWindowsApplicationLibraryService
{
    Task<WindowsApplicationScanResult> ScanAllAsync(StartMenuScanRequest request, bool includeAppsFolder, bool detectPwas, CancellationToken cancellationToken = default);
    Task<WindowsApplicationImportPreview> CreatePreviewAsync(WindowsApplicationScanResult scan, CancellationToken cancellationToken = default);
    Task<WindowsApplicationImportResult> ImportAsync(IReadOnlyCollection<WindowsApplicationImportSelection> selections, CancellationToken cancellationToken = default);
}

public interface IForegroundApplicationTracker { Task<ForegroundApplicationDetectionResult> DetectAsync(ForegroundApplicationDetectionRequest request, CancellationToken cancellationToken = default); }
public interface IDesktopControlProfileProvider { Task<DesktopControlProfileResolution> ResolveForItemAsync(string libraryItemId, CancellationToken cancellationToken = default); }
public interface IDesktopControllerStateSource { DesktopControllerReading Read(); }
public interface IDesktopInputInjector
{
    DesktopInputInjectionResult MoveCursor(double deltaX, double deltaY);
    DesktopInputInjectionResult Scroll(int verticalDelta, int horizontalDelta);
    DesktopInputInjectionResult Click(DesktopMouseButton button);
    DesktopInputInjectionResult SendApprovedKey(DesktopApprovedKey key);
    void ReleaseAll();
}
public interface IDesktopControlSafetyGuard { DesktopControlSafetyResult CanControl(int? processId); }
public interface IPhysicalMouseActivityMonitor { bool ShouldPause(DateTime nowUtc); void RecordControllerMovement(DateTime atUtc); }
public interface IDesktopControlSession
{
    bool IsActive { get; }
    event EventHandler<DesktopControlStopReason>? Stopped;
    Task StartAsync(DesktopControlSessionRequest request, CancellationToken cancellationToken = default);
    Task StopAsync(DesktopControlStopReason reason, CancellationToken cancellationToken = default);
}
