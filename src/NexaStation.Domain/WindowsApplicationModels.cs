namespace NexaStation.Domain;

public enum WindowsApplicationSourceType { ShellLink, PackagedApplication, ProgressiveWebApplication, Executable, UriShortcut, SystemApplication, Unknown }
public enum ApplicationDiscoveryConfidence { Low, Medium, High, Verified }
public enum WindowsLaunchMethod { ShellLink, Executable, AppUserModelId, Uri, BrowserApplication, ShellItem }
public enum WindowsCandidateKind { Application, RelatedTool, PossibleUninstaller, Documentation, WebShortcut, Unknown }
public enum WindowsImportStatus { New, AlreadyImported, Changed, PossibleDuplicate, RelatedTool, PossibleUninstaller, Missing, Invalid, Unsupported }
public enum ApplicationCategorySource { Automatic, Provider, User }
public enum ApplicationCardStyle { Square, Wide, Compact, Icon }
public enum ApplicationTrackingMode { None, LaunchedProcess, ActivatedProcessHint, ExpectedProcessName, ExpectedExecutablePath, ForegroundWindow, ManualReturn }
public enum ActivitySessionKind { Game, Application, Media, Unknown }

public sealed record WindowsApplicationSource
{
    public required string Id { get; init; }
    public required string LibraryItemId { get; init; }
    public required string ProviderId { get; init; }
    public required string ProviderItemId { get; init; }
    public WindowsApplicationSourceType SourceType { get; init; }
    public string? ShortcutPath { get; init; }
    public string? TargetPath { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? WorkingDirectory { get; init; }
    public string? AppUserModelId { get; init; }
    public string? PackageFamilyName { get; init; }
    public string? PackageFullName { get; init; }
    public string? ApplicationId { get; init; }
    public Uri? ProtocolUri { get; init; }
    public string? BrowserFamily { get; init; }
    public string? BrowserAppId { get; init; }
    public Uri? WebAppUrl { get; init; }
    public string? StartMenuFolder { get; init; }
    public string? CategoryHint { get; init; }
    public ApplicationDiscoveryConfidence DiscoveryConfidence { get; init; }
    public bool Installed { get; init; } = true;
    public DateTime FirstSeenAtUtc { get; init; }
    public DateTime LastSeenAtUtc { get; init; }
    public IReadOnlyDictionary<string, string> ProviderData { get; init; } = new Dictionary<string, string>();
}

public sealed record WindowsApplicationCandidate
{
    public required string ProviderId { get; init; }
    public required string ProviderItemId { get; init; }
    public required string DisplayName { get; init; }
    public WindowsApplicationSourceType SourceType { get; init; }
    public WindowsLaunchMethod LaunchMethod { get; init; }
    public string? ShortcutPath { get; init; }
    public string? TargetPath { get; init; }
    public IReadOnlyList<string> Arguments { get; init; } = [];
    public string? UnresolvedArguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Description { get; init; }
    public string? IconLocation { get; init; }
    public string? AppUserModelId { get; init; }
    public string? PackageFamilyName { get; init; }
    public string? PackageFullName { get; init; }
    public string? ApplicationId { get; init; }
    public Uri? ProtocolUri { get; init; }
    public string? ShellParsingPath { get; init; }
    public string? Publisher { get; init; }
    public string? BrowserFamily { get; init; }
    public string? BrowserAppId { get; init; }
    public Uri? WebAppUrl { get; init; }
    public string? StartMenuFolder { get; init; }
    public string? CategoryHint { get; init; }
    public ApplicationDiscoveryConfidence DiscoveryConfidence { get; init; }
    public bool Installed { get; init; } = true;
    public IReadOnlyDictionary<string, string> ProviderData { get; init; } = new Dictionary<string, string>();
    public ArtworkCandidate? IconCandidate { get; init; }
}

public sealed record StartMenuScanRequest(bool ScanCurrentUser = true, bool ScanCommon = true, bool IncludeRelatedTools = true, IProgress<WindowsApplicationScanProgress>? Progress = null);
public sealed record WindowsApplicationScanProgress(int Enumerated, int Processed, string? CurrentItem = null);
public sealed record WindowsApplicationScanResult(string ProviderId, DateTime StartedAtUtc, DateTime CompletedAtUtc, IReadOnlyList<WindowsApplicationCandidate> Candidates, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors, bool CompletedSuccessfully = true, bool WasCancelled = false);
public sealed record WindowsApplicationClassification(WindowsCandidateKind Kind, string SuggestedCategory, ApplicationDiscoveryConfidence Confidence, bool SelectedByDefault, string Reason);

public sealed record ShellLinkReadResult(bool Success, string ShortcutPath, string? DisplayName = null, string? TargetPath = null, IReadOnlyList<string>? Arguments = null, string? RawArguments = null, string? WorkingDirectory = null, string? Description = null, string? IconLocation = null, string? AppUserModelId = null, int? RunState = null, string? RelativePath = null, string? ShellItemIdentity = null, string? Error = null);
public sealed record AppsFolderApplication(string DisplayName, string? AppUserModelId, string? PackageFamilyName, string? PackageFullName, string? ApplicationId, string? IconPath, string? Publisher, string? ShellParsingPath, bool Installed = true);
public sealed record PwaCandidate(string BrowserFamily, string? BrowserExecutable, string? BrowserApplicationId, Uri? ApplicationUrl, string? ProfileHint, string? OriginalShortcutPath, string? AppUserModelId, ApplicationDiscoveryConfidence DetectionConfidence);

public sealed record WindowsApplicationLaunchRequest(WindowsLaunchMethod LaunchMethod, string? ShortcutPath = null, string? ExecutablePath = null, IReadOnlyList<string>? Arguments = null, string? WorkingDirectory = null, string? AppUserModelId = null, Uri? Uri = null, string? ShellParsingPath = null);
public sealed record WindowsActivationResult(bool Succeeded, int? ProcessIdHint, string Message, int? ErrorCode = null);
public sealed record PackagedApplicationActivationResult(bool Succeeded, int? ProcessIdHint, string Message, int? ErrorCode = null);
public sealed record ApplicationIconRequest(string Identity, string? ShortcutPath = null, string? ExecutablePath = null, string? IconLocation = null, string? AppUserModelId = null, int DesiredSize = 256, string? ShellParsingPath = null);
public sealed record ExtractedApplicationIcon(string CachePath, int? Width, int? Height, string SourceIdentity, DateTime SourceModifiedAtUtc);

public sealed record WindowsApplicationPreviewCandidate(WindowsApplicationCandidate Candidate, WindowsApplicationClassification Classification, WindowsImportStatus Status, bool SelectedByDefault, string? ExistingLibraryItemId = null, string? Message = null);
public sealed record WindowsApplicationImportPreview(IReadOnlyList<WindowsApplicationPreviewCandidate> Candidates, IReadOnlyList<string> Warnings, DateTime CreatedAtUtc);
public enum WindowsImportAction { Import, LinkToExisting, AddAlternateLaunchTarget, ImportSeparately, Skip }
public sealed record WindowsApplicationImportSelection(WindowsApplicationPreviewCandidate Candidate, WindowsImportAction Action, string? ExistingLibraryItemId = null, string? CategoryId = null, string? DesktopControlProfileId = null, bool EnableDesktopControlOnLaunch = false);
public sealed record WindowsApplicationImportResult(int Added, int Updated, int Linked, int AlternateTargets, int Skipped, int Failed, IReadOnlyList<string> Errors);

public sealed record ApplicationCategory(string Id, string Name, int SortOrder, bool BuiltIn, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record LibraryItemApplicationCategory(string LibraryItemId, string CategoryId, bool IsPrimary, ApplicationCategorySource Source);
public sealed record ApplicationLaunchTracking(string LibraryItemId, ApplicationTrackingMode TrackingMode, string? ExpectedProcessName, string? ExpectedExecutablePath, string? ExpectedAppUserModelId, string? ExpectedWindowClass, int DetectionTimeoutSeconds, int ExitGracePeriodSeconds, bool UserManaged, DateTime UpdatedAtUtc);
