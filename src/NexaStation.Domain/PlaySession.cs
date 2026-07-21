namespace NexaStation.Domain;

public sealed record PlaySession(
    string Id,
    string LibraryItemId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc = null,
    long? DurationSeconds = null,
    int? ProcessId = null,
    bool ExitDetectedNormally = false,
    string? FailureMessage = null,
    string? ProviderId = null,
    string? ProviderItemId = null,
    string? ProcessNames = null,
    double? DetectionConfidence = null,
    ActivitySessionKind SessionKind = ActivitySessionKind.Game,
    ApplicationTrackingMode TrackingMode = ApplicationTrackingMode.LaunchedProcess,
    int? ForegroundProcessId = null,
    string? ForegroundWindowIdentity = null);
