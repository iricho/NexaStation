using NexaStation.Domain;

namespace NexaStation.Core;

public sealed record LaunchResult(
    bool Succeeded,
    bool Rejected,
    bool Canceled,
    string? ErrorMessage,
    LaunchSession? Session)
{
    public static LaunchResult Success(LaunchSession session) => new(true, false, false, null, session);

    public static LaunchResult Failure(string error, LaunchSession? session) => new(false, false, false, error, session);

    public static LaunchResult Duplicate() => new(false, true, false, "Another application is already running.", null);

    public static LaunchResult Cancelled(LaunchSession? session) => new(false, false, true, "Launch was canceled.", session);
}
