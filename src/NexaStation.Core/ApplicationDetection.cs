using NexaStation.Domain;

namespace NexaStation.Core;

public sealed record LaunchedApplicationDetectionRequest(
    LaunchTarget LaunchTarget,
    DateTime LaunchStartedAtUtc,
    string? InstallationPath,
    IReadOnlyCollection<string> ExpectedProcessNames,
    IReadOnlyCollection<string> IgnoredProcessNames,
    TimeSpan Timeout,
    TimeSpan PollInterval,
    TimeSpan RelatedProcessGracePeriod);

public sealed record LaunchedApplicationDetectionResult(
    bool Detected,
    IReadOnlyList<RunningProcess> Processes,
    IReadOnlyList<string> ProcessNames,
    double Confidence,
    string Message,
    string? LearnedProcessRule = null);

public interface ILaunchedApplicationDetector
{
    bool CanDetect(LaunchTarget target);
    Task<LaunchedApplicationDetectionResult> DetectAsync(
        LaunchedApplicationDetectionRequest request,
        CancellationToken cancellationToken = default);
}
