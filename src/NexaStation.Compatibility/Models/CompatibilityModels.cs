using System.Text.Json.Serialization;

namespace NexaStation.Compatibility.Models;

public enum CompatibilityTargetType
{
    SteamGame,
    StandaloneGame,
    EmulatedGame,
    DesktopApplication,
    PackagedApplication,
    Pwa,
    MediaApplication,
    Controller,
    Display,
    Provider,
    Unknown,
}

public enum CompatibilityProfileSource { BuiltIn, User, ImportedSignedFeed, GeneratedDiagnosticSuggestion }
public enum CompatibilityStatus { Untested, Launches, LaunchesWithLimitations, FullyCompatible, Broken, Unsupported, NeedsRetest }
public enum CompatibilityResult { Unknown, Passed, PassedWithLimitations, Failed, NotApplicable }
public enum CompatibilityPrivacyLevel { SummaryOnly, Standard, ExplicitPathsApproved }
public enum ProcessTrackingStrategy { Default, None, LaunchedProcess, ForegroundWindow, ActivatedPidHint, ExpectedExecutable, ManualReturn }

public sealed record CompatibilityTarget(
    CompatibilityTargetType TargetType,
    string? ProviderId = null,
    string? ProviderItemId = null,
    string? PlatformId = null,
    string? EmulatorAdapterId = null,
    string? ExecutableIdentity = null,
    string? AppUserModelId = null,
    string? ApplicationVersion = null,
    string? WindowsVersion = null,
    string? ControllerFamily = null,
    string? DisplayIdentity = null);

public sealed record CompatibilityTargetMatcher
{
    public CompatibilityTargetType TargetType { get; init; } = CompatibilityTargetType.Unknown;
    public string? ProviderId { get; init; }
    public string? ProviderItemId { get; init; }
    public string? PlatformId { get; init; }
    public string? EmulatorAdapterId { get; init; }
    public string? ExecutableIdentity { get; init; }
    public string? AppUserModelId { get; init; }
    public string? ControllerFamily { get; init; }
    public string? DisplayIdentity { get; init; }
}

public sealed record CompatibilityReport
{
    public required string Id { get; init; }
    public CompatibilityTargetType TargetType { get; init; }
    public string? TargetIdentity { get; init; }
    public string? DisplayName { get; init; }
    public string? ProviderId { get; init; }
    public string? PlatformId { get; init; }
    public string? EmulatorAdapterId { get; init; }
    public string? ApplicationVersion { get; init; }
    public required string NexaStationVersion { get; init; }
    public string? WindowsVersion { get; init; }
    public string? Architecture { get; init; }
    public string? GraphicsAdapterSummary { get; init; }
    public string? ControllerFamily { get; init; }
    public string? DisplayConfigurationSummary { get; init; }
    public CompatibilityResult LaunchResult { get; init; }
    public CompatibilityResult TrackingResult { get; init; }
    public CompatibilityResult RestoreResult { get; init; }
    public CompatibilityResult ArtworkResult { get; init; }
    public CompatibilityResult InputResult { get; init; }
    public CompatibilityResult PerformanceResult { get; init; }
    public string? UserNotes { get; init; }
    public string? DiagnosticCorrelationId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public CompatibilityPrivacyLevel PrivacyLevel { get; init; }
    public CompatibilityStatus Status { get; init; } = CompatibilityStatus.Untested;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompatibilityRuleKind
{
    Launch,
    ProcessDetection,
    Window,
    Input,
    Artwork,
    Performance,
}
