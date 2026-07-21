namespace NexaStation.Domain;

public sealed record LaunchTarget(
    LaunchType LaunchType,
    string? ExecutablePath = null,
    string? WorkingDirectory = null,
    string? Arguments = null,
    Uri? Uri = null,
    string? ExpectedProcessName = null,
    bool HideFrontendWhileRunning = true,
    bool RestoreFrontendAfterExit = true)
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string LibraryItemId { get; init; } = string.Empty;
    public string Name { get; init; } = "Default";
    public string? ProviderId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool UserManaged { get; init; } = true;
    public IReadOnlyList<string> ArgumentList { get; init; } = [];
    public IReadOnlyDictionary<string, string> EnvironmentOverrides { get; init; } = new Dictionary<string, string>();
    public string? EmulatedGameSourceId { get; init; }
    public string? EmulatorInstallationId { get; init; }
    public string? EmulatorAdapterId { get; init; }
    public string? EmulatorVersion { get; init; }
    public string? EmulatedPlatformId { get; init; }
    public string? LaunchProfileId { get; init; }
    public string? ShortcutPath { get; init; }
    public string? WindowsApplicationSourceId { get; init; }
    public WindowsLaunchMethod? WindowsLaunchMethod { get; init; }
    public string? AppUserModelId { get; init; }
    public string? ShellParsingPath { get; init; }
    public ApplicationTrackingMode TrackingMode { get; init; } = ApplicationTrackingMode.LaunchedProcess;
    public string? ExpectedExecutablePath { get; init; }
    public string? ExpectedWindowClass { get; init; }
    public int DetectionTimeoutSeconds { get; init; } = 30;
    public int ExitGracePeriodSeconds { get; init; } = 3;
    public int FrontendRestoreDelayMilliseconds { get; init; }
    public bool DoNotForceFrontendForeground { get; init; }
    public bool RestoreFrontendManually { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;
}
