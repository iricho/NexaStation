using NexaStation.Domain;

namespace NexaStation.Core;

public interface IGalleryPersistence
{
    Task<IReadOnlyList<GallerySource>> GetSourcesAsync(CancellationToken cancellationToken=default);
    Task<GallerySource?> GetSourceAsync(string id,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<GallerySourceOverlap>> FindSourceOverlapsAsync(string normalizedFolderPath,CancellationToken cancellationToken=default);
    Task AddSourceAsync(GallerySource source,CancellationToken cancellationToken=default);
    Task UpdateSourceAsync(GallerySource source,CancellationToken cancellationToken=default);
    Task RemoveSourceAsync(string sourceId,CancellationToken cancellationToken=default);
    Task<GalleryRefreshResult> ApplyScanAsync(GallerySourceScanResult scan,IReadOnlyList<GalleryMediaInspectionResult> inspections,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<GalleryMediaItem>> QueryAsync(GalleryQuery query,CancellationToken cancellationToken=default);
    Task<GalleryMediaItem?> GetMediaItemAsync(string id,CancellationToken cancellationToken=default);
    Task SetFavoriteAsync(string id,bool favorite,CancellationToken cancellationToken=default);
    Task SetHiddenAsync(string id,bool hidden,CancellationToken cancellationToken=default);
    Task MarkOpenedAsync(string id,DateTime openedAtUtc,CancellationToken cancellationToken=default);
    Task RelinkMediaAsync(string id,string filePath,string normalizedFilePath,long fileSize,DateTime modifiedAtUtc,CancellationToken cancellationToken=default);
    Task RemoveMediaIndexAsync(string id,CancellationToken cancellationToken=default);
    Task<GalleryThumbnail?> GetThumbnailAsync(string mediaItemId,GalleryThumbnailType type,CancellationToken cancellationToken=default);
    Task UpsertThumbnailAsync(GalleryThumbnail thumbnail,CancellationToken cancellationToken=default);
    Task RemoveThumbnailsAsync(string mediaItemId,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<GalleryThumbnail>> GetThumbnailLruAsync(CancellationToken cancellationToken=default);
    Task<IReadOnlyList<GalleryAlbum>> GetAlbumsAsync(CancellationToken cancellationToken=default);
    Task<GalleryAlbum?> GetAlbumAsync(string id,CancellationToken cancellationToken=default);
    Task SaveAlbumAsync(GalleryAlbum album,CancellationToken cancellationToken=default);
    Task DeleteAlbumAsync(string id,CancellationToken cancellationToken=default);
    Task AddAlbumItemAsync(GalleryAlbumItem item,CancellationToken cancellationToken=default);
    Task RemoveAlbumItemAsync(string albumId,string mediaItemId,CancellationToken cancellationToken=default);
    Task SetAlbumCoverAsync(string albumId,string? mediaItemId,CancellationToken cancellationToken=default);
    Task<GalleryApplicationState?> GetApplicationStateAsync(CancellationToken cancellationToken=default);
    Task SaveApplicationStateAsync(GalleryApplicationState state,CancellationToken cancellationToken=default);
    Task<GalleryLaunchPreference?> GetLaunchPreferenceAsync(GalleryLaunchPreferenceScope scope,string? scopeId,CancellationToken cancellationToken=default);
    Task SaveLaunchPreferenceAsync(GalleryLaunchPreference preference,CancellationToken cancellationToken=default);
    Task<GalleryBackgroundSelection?> GetBackgroundAsync(CancellationToken cancellationToken=default);
    Task SaveBackgroundAsync(GalleryBackgroundSelection? selection,CancellationToken cancellationToken=default);
    Task<GalleryMediaItem> UpsertMediaAsync(GallerySource source,GalleryMediaInspectionResult inspection,CancellationToken cancellationToken=default)=>throw new NotSupportedException("Targeted Gallery indexing is not implemented by this persistence provider.");
}

public interface IGallerySourceScanner { Task<GallerySourceScanResult> ScanAsync(GallerySource source,CancellationToken cancellationToken=default); }
public interface IGalleryMediaInspector { Task<GalleryMediaInspectionResult> InspectAsync(GalleryMediaInspectionRequest request,CancellationToken cancellationToken=default); }
public interface IGalleryThumbnailService
{
    Task<GalleryThumbnailResult> GetOrCreateAsync(GalleryThumbnailRequest request,CancellationToken cancellationToken=default);
    Task InvalidateAsync(string galleryMediaItemId,CancellationToken cancellationToken=default);
    Task<GalleryThumbnailCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken=default);
    Task ClearCacheAsync(CancellationToken cancellationToken=default);
}
public interface IGalleryLibraryService
{
    Task<IReadOnlyList<GallerySourceOverlap>> PreviewSourceAsync(string folderPath,CancellationToken cancellationToken=default);
    Task<GallerySource> AddSourceAsync(GallerySource source,bool userApproved,bool scanImmediately,CancellationToken cancellationToken=default);
    Task<GalleryRefreshResult> RefreshSourceAsync(string sourceId,CancellationToken cancellationToken=default);
    Task<GalleryMediaItem?> IndexApprovedFileAsync(string filePath,CancellationToken cancellationToken=default);
    Task RemoveSourceAsync(string sourceId,bool keepCachedThumbnails,CancellationToken cancellationToken=default);
}
public interface IGallerySourceWatcherCoordinator : IAsyncDisposable
{
    Task StartAsync(Func<string,CancellationToken,Task> refreshSourceAsync,CancellationToken cancellationToken=default);
    Task ReloadAsync(CancellationToken cancellationToken=default);
    Task StopAsync(CancellationToken cancellationToken=default);
}
public interface IGalleryMediaLauncher { Task<GalleryMediaLaunchResult> OpenAsync(GalleryMediaLaunchRequest request,CancellationToken cancellationToken=default); }
public interface IGalleryBackgroundService
{
    Task<GalleryBackgroundSelection> SetAsync(GalleryMediaItem item,GalleryZoomMode displayMode,CancellationToken cancellationToken=default);
    Task ResetAsync(CancellationToken cancellationToken=default);
}
public interface ISlideshowService
{
    event EventHandler<GalleryMediaItem>? ItemChanged;
    GallerySlideshowState State { get; }
    Task StartAsync(GallerySlideshowRequest request,CancellationToken cancellationToken=default);
    Task StopAsync(GallerySlideshowStopReason reason,CancellationToken cancellationToken=default);
    void SetForeground(bool foreground);
}
