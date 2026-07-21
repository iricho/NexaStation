using NexaStation.Domain;

namespace NexaStation.Core;

public enum ArtworkSourceType { UserOverride, ManagedCache, ProviderLocal, Sidecar, Catalogue, Online, GeneratedFallback }
public enum ArtworkFallbackType { Game, Application, Gallery, Collection, StreamingService, Generic }

public sealed record ArtworkRequest(
    string TargetId,
    ContentType ContentType,
    string? ProviderId,
    AssetType PreferredAssetType,
    int TargetWidth,
    int TargetHeight,
    bool AllowOnlineLookup=false,
    bool IsPrivateContent=false);

public sealed record ArtworkResolutionResult(
    ArtworkSourceType SourceType,
    string? AssetPath,
    string? PreviewPath,
    int? Width,
    int? Height,
    string CacheKey,
    bool IsFallback,
    string? FailureReason,
    ArtworkFallbackType FallbackType);

public interface IArtworkResolver
{
    Task<ArtworkResolutionResult> ResolveAsync(ArtworkRequest request,CancellationToken cancellationToken=default);
}
