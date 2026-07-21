namespace NexaStation.Domain;

public sealed record LaunchSession(
    Guid Id,
    string LibraryItemId,
    LaunchState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt = null,
    int? ProcessId = null,
    string? FailureMessage = null)
{
    public static LaunchSession Start(string libraryItemId) =>
        new(Guid.NewGuid(), libraryItemId, LaunchState.Idle, DateTimeOffset.UtcNow);
}
