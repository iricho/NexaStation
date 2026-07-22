using NexaStation.Domain;

namespace NexaStation.Core;

public interface IContentLifecycleService
{
    Task<ContentLifecycleStatus> GetStatusAsync(ContentLifecycleTarget target, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentLifecycleEntry>> QueryAsync(ContentLifecycleQuery query, CancellationToken cancellationToken = default);
    Task SetStatusAsync(ContentLifecycleTarget target, ContentLifecycleStatus status, CancellationToken cancellationToken = default);
    Task ClearStatusAsync(ContentLifecycleTarget target, CancellationToken cancellationToken = default);
    Task<int> ClearStaleTargetsAsync(ContentLifecycleTargetType targetType, IReadOnlyCollection<string> validTargetIds, CancellationToken cancellationToken = default);
}
