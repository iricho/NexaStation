namespace NexaStation.Domain;

public sealed record EmulatedIdentityRepairResult(int Inspected,int Updated,int TitlesCleaned,int Failed,IReadOnlyList<string> Errors);

[Flags]
public enum EmulatorCapability
{
    None = 0,
    DirectGameLaunch = 1 << 0,
    BatchMode = 1 << 1,
    FullscreenArgument = 1 << 2,
    CustomUserDirectory = 1 << 3,
    CustomConfiguration = 1 << 4,
    CoreSelection = 1 << 5,
    DiscImageLaunch = 1 << 6,
    FolderGameLaunch = 1 << 7,
    TitleIdLaunch = 1 << 8,
    VersionReporting = 1 << 9,
    HelpOutput = 1 << 10,
    ExitAfterGame = 1 << 11,
    MultipleInstances = 1 << 12,
}

public enum EmulatorValidationStatus { Unknown, Valid, Missing, Invalid, ProbeFailed, Disabled }
public enum EmulatorProcessDetectionMode { LaunchedProcess, ProcessName, ExecutablePath, ChildProcess, Manual }
public enum GameIdentificationConfidence { None, FilenameOnly, ContainerMetadata, DiscSerial, TitleId, HighConfidence }
public enum EmulatedImportStatus { New, AlreadyImported, Changed, PossibleDuplicate, MissingEmulator, UnsupportedFile, PossibleMultiDiscGame, Invalid }

public sealed record EmulatorInstallation
{
    public required string Id { get; init; }
    public required string AdapterId { get; init; }
    public required string DisplayName { get; init; }
    public required string ExecutablePath { get; init; }
    public string? Version { get; init; }
    public string? Architecture { get; init; }
    public required string InstallationDirectory { get; init; }
    public string? UserDataDirectory { get; init; }
    public bool Enabled { get; init; } = true;
    public bool IsPortable { get; init; }
    public string? ArgumentsOverride { get; init; }
    public EmulatorCapability DetectedCapabilities { get; init; }
    public EmulatorValidationStatus ValidationStatus { get; init; }
    public DateTime AddedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastValidatedAtUtc { get; init; }
}

public sealed record EmulatorPlatform(
    string Id,
    string Name,
    string ShortName,
    string? Manufacturer,
    int? Generation,
    IReadOnlyList<string> DefaultExtensions,
    IReadOnlyList<string> DefaultAdapterIds,
    string? IconAsset = null,
    bool Enabled = true);

public sealed record GameSource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FolderPath { get; init; }
    public required string PlatformId { get; init; }
    public bool IncludeSubfolders { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public bool FollowSymbolicLinks { get; init; }
    public bool ScanHiddenFiles { get; init; }
    public string? AdapterPreferenceId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastSuccessfulScanAtUtc { get; init; }
}

public sealed record EmulatedGameSource
{
    public required string Id { get; init; }
    public required string LibraryItemId { get; init; }
    public required string PlatformId { get; init; }
    public required string GamePath { get; init; }
    public required string NormalizedGamePath { get; init; }
    public string? FileExtension { get; init; }
    public long? FileSize { get; init; }
    public DateTime? FileModifiedAtUtc { get; init; }
    public string? GameIdentifier { get; init; }
    public string? Region { get; init; }
    public string? Revision { get; init; }
    public int? DiscNumber { get; init; }
    public int? DiscCount { get; init; }
    public string? GameSourceId { get; init; }
    public string? PreferredEmulatorInstallationId { get; init; }
    public string? LaunchProfileId { get; init; }
    public string? RetroArchCoreId { get; init; }
    public DateTime FirstSeenAtUtc { get; init; }
    public DateTime LastSeenAtUtc { get; init; }
    public bool Available { get; init; } = true;
}

public sealed record EmulatorLaunchProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string EmulatorInstallationId { get; init; }
    public required string PlatformId { get; init; }
    public required string ArgumentsTemplate { get; init; }
    public string? WorkingDirectory { get; init; }
    public bool Fullscreen { get; init; } = true;
    public bool ExitEmulatorWhenGameStops { get; init; } = true;
    public EmulatorProcessDetectionMode ProcessDetectionMode { get; init; }
    public string? ExpectedProcessName { get; init; }
    public IReadOnlyDictionary<string, string> AdditionalEnvironment { get; init; } = new Dictionary<string, string>();
    public bool UserManaged { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record RetroArchCore(
    string Id,
    string EmulatorInstallationId,
    string DisplayName,
    string CorePath,
    string CoreFileName,
    IReadOnlyList<string> SupportedPlatformIds,
    bool SupportsNoGame,
    bool Available,
    DateTime? LastValidatedAtUtc);

public sealed record GameDisc(string Id, string LibraryItemId, string GamePath, int DiscNumber, string? Label = null);

public sealed record EmulatorProbeOptions(
    IReadOnlyList<string> ApprovedArguments,
    TimeSpan Timeout,
    int MaximumOutputCharacters = 32768,
    bool SafeToLaunchWithoutGame = false);

public sealed record EmulatorProbeResult(
    bool Succeeded,
    string? DetectedName,
    string? Version,
    EmulatorCapability Capabilities,
    IReadOnlyList<string> SupportedPlatformIds,
    IReadOnlyList<string> Warnings,
    string? Output = null,
    bool TimedOut = false);

public sealed record EmulatorValidationResult(bool IsValid, EmulatorValidationStatus Status, string Message, IReadOnlyList<string> Diagnostics);

public sealed record GameIdentificationRequest(string PlatformId, string GamePath, string FileExtension, long? MaximumReadBytes = 1048576);
public sealed record GameIdentificationResult(
    string SuggestedTitle,
    string OriginalFileName,
    string? GameIdentifier,
    string? Region,
    string? Revision,
    int? DiscNumber,
    int? DiscCount,
    GameIdentificationConfidence Confidence,
    IReadOnlyList<string> Languages);

public sealed record EmulatorLaunchRequest(
    EmulatorInstallation Installation,
    EmulatorPlatform Platform,
    EmulatedGameSource Game,
    EmulatorLaunchProfile? Profile = null,
    RetroArchCore? Core = null,
    IReadOnlyList<string>? ExtraArguments = null);

public sealed record EmulatorLaunchDefinition(
    string Executable,
    IReadOnlyList<string> ArgumentList,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentOverrides,
    string? ExpectedProcess,
    EmulatorProcessDetectionMode ProcessDetectionMode,
    IReadOnlyList<string> Warnings);

public sealed record DiscoveredGame(
    string PlatformId,
    string GamePath,
    string NormalizedGamePath,
    string SuggestedTitle,
    string OriginalFileName,
    string FileExtension,
    long FileSize,
    DateTime FileModifiedAtUtc,
    GameIdentificationResult Identification,
    EmulatedImportStatus Status = EmulatedImportStatus.New,
    string? ExistingLibraryItemId = null,
    string? PreferredEmulatorInstallationId = null,
    IReadOnlyList<ArtworkCandidate>? ArtworkCandidates = null);

public sealed record GameSourceScanProgress(int FilesEnumerated, int CandidatesFound, string? CurrentDirectory);
public sealed record GameSourceScanResult(
    GameSource Source,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    IReadOnlyList<DiscoveredGame> Games,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool WasCancelled,
    bool CompletedSuccessfully);

public sealed record EmulatedGameImportSelection(DiscoveredGame Game, bool Import, string? LinkToLibraryItemId = null, bool GroupWithPreviousDisc = false);
public sealed record EmulatedGameImportResult(int Added, int Updated, int Skipped, int Failed, IReadOnlyList<string> Errors);
public sealed record EmulatedGameLaunchRequest(string EmulatedGameSourceId, string? EmulatorInstallationId = null, string? LaunchProfileId = null, bool FallbackConfirmed = false);
