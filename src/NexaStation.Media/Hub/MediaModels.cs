using NexaStation.Domain;

namespace NexaStation.Media.Hub;

public enum MediaAssociationScope{GlobalMediaType,FileExtension,GallerySource,MediaItem}
public enum MediaLaunchMethod{WindowsDefault,PreferredPlayer,LibraryApplication}
public enum MediaTrackingConfidence{Unknown,Requested,ProcessTracked}
public sealed record MediaAssociation(string Id,MediaAssociationScope ScopeType,string? ScopeValue,GalleryMediaType? MediaType,string? FileExtension,string? PlayerLibraryItemId,string? LaunchTargetId,string? ArgumentsTemplate,ApplicationTrackingMode TrackingMode,bool HideFrontend,bool RestoreFrontend,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);
public sealed record RecentMediaEntry(string Id,string? MediaItemId,GalleryMediaType MediaType,DateTime OpenedAtUtc,MediaLaunchMethod LaunchMethod,string? PlayerLibraryItemId,MediaTrackingConfidence TrackingConfidence);
public sealed record MediaHubRow(string Title,IReadOnlyList<LibraryItem> LibraryItems,IReadOnlyList<GalleryMediaItem> GalleryItems);
public sealed record MediaLaunchPlan(bool UseWindowsDefault,LibraryItem? Player,LaunchTarget? LaunchTarget,IReadOnlyList<string> Arguments,string Preview,string? FailureReason=null);

public interface IMediaPersistence
{
    Task SaveAssociationAsync(MediaAssociation value,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<MediaAssociation>> GetAssociationsAsync(CancellationToken cancellationToken=default);
    Task AddRecentAsync(RecentMediaEntry value,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<RecentMediaEntry>> GetRecentAsync(int maximumResults,CancellationToken cancellationToken=default);
}
public interface IRecentMediaService{Task RecordOpenedAsync(RecentMediaEntry entry,CancellationToken cancellationToken=default);Task<IReadOnlyList<RecentMediaEntry>> GetRecentAsync(int maximumResults,CancellationToken cancellationToken=default);}
public interface IMediaAssociationService{Task<MediaAssociation?> ResolveAsync(GalleryMediaItem item,CancellationToken cancellationToken=default);Task<MediaLaunchPlan> CreateLaunchPlanAsync(GalleryMediaItem item,CancellationToken cancellationToken=default);Task SaveAsync(MediaAssociation association,CancellationToken cancellationToken=default);}
public interface IMediaHubService{Task<IReadOnlyList<MediaHubRow>> GetRowsAsync(CancellationToken cancellationToken=default);}
