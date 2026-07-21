using NexaStation.Domain;

namespace NexaStation.Core;

public interface ILaunchCoordinator
{
    bool IsLaunchInProgress { get; }

    Task<LaunchResult> LaunchAsync(
        LibraryItem item,
        CancellationToken cancellationToken = default);
}
