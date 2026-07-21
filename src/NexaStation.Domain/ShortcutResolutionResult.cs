namespace NexaStation.Domain;

public sealed record ShortcutResolutionResult(
    bool Resolved,
    bool ValidationUncertain,
    string? TargetPath,
    string? Arguments,
    string? WorkingDirectory,
    string? ErrorMessage);
