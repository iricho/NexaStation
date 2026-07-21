using NexaStation.Domain;

namespace NexaStation.Core;

public interface IArtworkProvider
{
    string Id { get; }
    string DisplayName { get; }
    ArtworkProviderCapabilities Capabilities { get; }
    Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default);
    Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default);
}

public interface IGameMetadataProvider
{
    string Id { get; }
    Task<IReadOnlyList<GameMetadataCandidate>> SearchAsync(GameMetadataSearchRequest request, CancellationToken cancellationToken = default);
    Task<GameMetadataResult> GetMetadataAsync(string providerGameId, CancellationToken cancellationToken = default);
}

public interface IGameIdentityResolver
{
    Task<GameIdentity> ResolveAsync(string libraryItemId, CancellationToken cancellationToken = default);
}

public interface IGameMatchScorer
{
    ArtworkMatchCandidate Score(GameIdentity identity, ArtworkMatchCandidate candidate);
}

public interface IProviderCredentialStore
{
    Task SaveAsync(ProviderCredential credential, CancellationToken cancellationToken = default);
    Task<ProviderCredential?> GetAsync(string providerId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string providerId, CancellationToken cancellationToken = default);
}

public interface IProviderRateLimitCoordinator
{
    Task<RateLimitLease> AcquireAsync(string providerId, CancellationToken cancellationToken = default);
    Task ReportResponseAsync(string providerId, ProviderResponseMetadata metadata, CancellationToken cancellationToken = default);
}

public interface IArtworkAssignmentStore
{
    Task<ArtworkAssignment?> GetCurrentAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtworkAssignment>> GetHistoryAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default);
    Task ApplyAsync(ArtworkAssignment assignment, CancellationToken cancellationToken = default);
    Task<ArtworkAssignment?> UndoAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default);
    Task SetLockedAsync(string libraryItemId, ArtworkKind assetType, bool locked, CancellationToken cancellationToken = default);
}

public interface IArtworkPresenceService
{
    Task<bool> IsPresentAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default);
}

public interface IArtworkJobService
{
    Task<string> CreateAsync(ArtworkJobRequest request, CancellationToken cancellationToken = default);
    Task StartAsync(string jobId, CancellationToken cancellationToken = default);
    Task PauseAsync(string jobId, CancellationToken cancellationToken = default);
    Task ResumeAsync(string jobId, CancellationToken cancellationToken = default);
    Task CancelAsync(string jobId, CancellationToken cancellationToken = default);
    Task<ArtworkJobSnapshot> GetAsync(string jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtworkJobSnapshot>> ListAsync(CancellationToken cancellationToken = default);
    Task<ArtworkJobRequest> GetRequestAsync(string jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtworkJobItemSnapshot>> ListItemsAsync(string jobId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ArtworkJobSnapshot> WatchAsync(string jobId, CancellationToken cancellationToken = default);
}

public interface IArtworkJobPreflightService
{
    Task<ArtworkJobPreflightResult> CheckAsync(ArtworkJobRequest request, CancellationToken cancellationToken = default);
}

public interface IArtworkProviderConnectionTester
{
    Task<ArtworkProviderConnectionTestResult> TestAsync(string providerId, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string providerId, bool enabled, CancellationToken cancellationToken = default);
}

public interface IArtworkProviderConnectionProbe
{
    Task<ArtworkProviderStatus> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public interface IArtworkJobProcessor
{
    Task RunAsync(string jobId, CancellationToken cancellationToken = default);
    Task ResumeInterruptedAsync(CancellationToken cancellationToken = default);
}

public interface IArtworkReviewService
{
    Task<IReadOnlyList<ArtworkReviewItem>> ListAsync(CancellationToken cancellationToken = default);
    Task AcceptAsync(string jobItemId, string providerId, string providerGameId, bool remember, CancellationToken cancellationToken = default);
    Task SkipAsync(string jobItemId, CancellationToken cancellationToken = default);
}

public interface IRemoteArtworkDownloader
{
    Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default);
    Task<ArtworkDownloadResult> ImportLocalAsync(string sourcePath, ArtworkKind assetType, ArtworkDownloadRequest request, CancellationToken cancellationToken = default);
}

public interface IMetadataProvenanceStore
{
    Task SaveExternalIdentityAsync(ExternalGameIdentity identity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalGameIdentity>> GetExternalIdentitiesAsync(string libraryItemId, CancellationToken cancellationToken = default);
    Task RecordFieldOriginAsync(MetadataFieldOrigin origin, CancellationToken cancellationToken = default);
    Task MarkUserModifiedAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default);
    Task<bool> CanAutomaticallyOverwriteAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default);
}
