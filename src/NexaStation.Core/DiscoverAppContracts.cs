using NexaStation.Domain;

namespace NexaStation.Core;

public interface IDiscoverAppCatalogue
{
    Task<IReadOnlyList<DiscoverAppDefinition>> GetAsync(CancellationToken cancellationToken=default);
}

public interface IDiscoverAppStateService
{
    Task<DiscoverAppState> ResolveAsync(DiscoverAppDefinition definition,CancellationToken cancellationToken=default);
}

public interface IStoreNavigationService
{
    Task<StoreNavigationResult> OpenProductAsync(string productId,CancellationToken cancellationToken=default);
    Task<StoreNavigationResult> SearchAsync(string query,CancellationToken cancellationToken=default);
}

public interface IExternalUriNavigationService
{
    Task<ExternalUriNavigationResult> OpenAsync(Uri uri,CancellationToken cancellationToken=default);
}
