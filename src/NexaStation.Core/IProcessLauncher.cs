using NexaStation.Domain;

namespace NexaStation.Core;

public interface IProcessLauncher
{
    Task<RunningProcess> StartAsync(
        LaunchTarget target,
        CancellationToken cancellationToken = default);
}
