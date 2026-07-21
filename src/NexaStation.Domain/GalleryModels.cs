namespace NexaStation.Domain;

public enum GalleryMediaType { Image, AnimatedImage, Video, Unknown }
public enum GallerySourceKind { Photos, Screenshots, Recordings, Videos, Wallpapers, Custom }
public enum GalleryPrivacyLevel { Normal, Private }
public enum GalleryThumbnailState { Missing, Pending, Ready, Failed, UnsupportedCodec, Stale }
public enum GalleryThumbnailType { GridSmall, GridMedium, DetailsLarge, VideoPreview, AlbumCover }
public enum GalleryAlbumType { Manual, Folder, Automatic }
public enum GallerySourceOverlapKind { Exact, ExistingParent, ExistingChild }
public enum GalleryViewKind { Home, Timeline, Folders, Grid, Albums }
public enum GalleryGridSize { Small, Medium, Large }
public enum GallerySortMode { NewestCaptured, OldestCaptured, RecentlyAdded, RecentlyOpened, Filename, FileSize }
public enum GalleryZoomMode { Fit, Fill, ActualSize, Custom }
public enum GallerySlideshowTransition { None, Fade }
public enum GallerySlideshowStopReason { UserReturned, LostForeground, ContentLaunch, Exhausted, Shutdown, Cancelled }
public enum GalleryLaunchPreferenceScope { GlobalVideo, Source, MediaItem }

public sealed record GallerySource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string FolderPath { get; init; }
    public required string NormalizedFolderPath { get; init; }
    public GallerySourceKind SourceKind { get; init; }
    public bool IncludeSubfolders { get; init; } = true;
    public bool Enabled { get; init; } = true;
    public bool WatchForChanges { get; init; }
    public bool IncludeHiddenFiles { get; init; }
    public bool IncludeImages { get; init; } = true;
    public bool IncludeVideos { get; init; } = true;
    public GalleryPrivacyLevel PrivacyLevel { get; init; }
    public bool ShowOnHome { get; init; } = true;
    public bool ShowFilenames { get; init; } = true;
    public bool AllowSlideshow { get; init; } = true;
    public bool AllowBackgroundUse { get; init; } = true;
    public bool FolderAlbumsEnabled { get; init; }
    public bool Available { get; init; } = true;
    public string? PreferredApplicationId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastSuccessfulScanAtUtc { get; init; }
}

public sealed record GalleryMediaItem
{
    public required string Id { get; init; }
    public required string GallerySourceId { get; init; }
    public GalleryMediaType MediaType { get; init; }
    public required string FilePath { get; init; }
    public required string NormalizedFilePath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required string ParentFolder { get; init; }
    public long FileSize { get; init; }
    public DateTime? CreatedAtUtc { get; init; }
    public DateTime ModifiedAtUtc { get; init; }
    public DateTime? CapturedAtUtc { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long? DurationMilliseconds { get; init; }
    public int? Orientation { get; init; }
    public bool Favorite { get; init; }
    public bool Hidden { get; init; }
    public bool Available { get; init; } = true;
    public GalleryThumbnailState ThumbnailState { get; init; }
    public DateTime AddedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastOpenedAtUtc { get; init; }
    public string? ThumbnailPath { get; init; }
    public string? SourceName { get; init; }
    public GallerySourceKind SourceKind { get; init; }
    public GalleryPrivacyLevel SourcePrivacyLevel { get; init; }
    public string? PreferredApplicationId { get; init; }
}

public sealed record GalleryThumbnail(string Id,string GalleryMediaItemId,GalleryThumbnailType ThumbnailType,string LocalCachePath,int Width,int Height,long FileSize,DateTime SourceModifiedAtUtc,DateTime CreatedAtUtc,DateTime LastAccessedAtUtc,int GenerationVersion);
public sealed record GalleryAlbum(string Id,string Name,string? Description,GalleryAlbumType AlbumType,string? CoverMediaItemId,string? FolderPath,int SortOrder,DateTime CreatedAtUtc,DateTime UpdatedAtUtc);
public sealed record GalleryAlbumItem(string GalleryAlbumId,string GalleryMediaItemId,int SortOrder,DateTime AddedAtUtc);
public sealed record GallerySourceOverlap(GallerySource Existing,GallerySourceOverlapKind Kind);

public sealed record GalleryMediaCandidate(string FilePath,string NormalizedFilePath,string FileName,string Extension,string ParentFolder,long FileSize,DateTime? CreatedAtUtc,DateTime ModifiedAtUtc,GalleryMediaType MediaType);
public sealed record GallerySourceScanResult(GallerySource Source,IReadOnlyList<GalleryMediaCandidate> Candidates,IReadOnlyList<string> Warnings,IReadOnlyList<string> Errors,bool IsComplete,bool WasCancelled,bool RootAvailable,DateTime StartedAtUtc,DateTime CompletedAtUtc)
{
    public TimeSpan Duration => CompletedAtUtc-StartedAtUtc;
}
public sealed record GalleryMediaInspectionRequest(GalleryMediaCandidate Candidate,bool ReadExtendedMetadata=false);
public sealed record GalleryMediaInspectionResult(GalleryMediaCandidate Candidate,int? Width=null,int? Height=null,long? DurationMilliseconds=null,int? Orientation=null,DateTime? CapturedAtUtc=null,string? CameraModel=null,bool UnsupportedCodec=false,string? Warning=null);
public sealed record GalleryRefreshResult(string SourceId,int Added,int Updated,int Unchanged,int Missing,int Failed,bool Applied,IReadOnlyList<string> Warnings,TimeSpan Duration);

public sealed record GalleryQuery(string? SearchText=null,string? SourceId=null,string? AlbumId=null,GalleryMediaType? MediaType=null,GallerySourceKind? SourceKind=null,bool FavoritesOnly=false,bool IncludeHidden=false,bool IncludeUnavailable=false,bool IncludePrivate=true,bool HomePreviewOnly=false,int? Year=null,GallerySortMode SortMode=GallerySortMode.NewestCaptured,int MaximumResults=500);
public sealed record GalleryThumbnailRequest(GalleryMediaItem MediaItem,GalleryThumbnailType ThumbnailType,int MaximumDimension=512,int GenerationVersion=1);
public sealed record GalleryThumbnailResult(bool Succeeded,string? LocalCachePath,int Width,int Height,bool CacheHit,bool Placeholder,string? Error=null);
public sealed record GalleryThumbnailCacheStats(long DiskBytes,long MemoryBytes,int DiskEntries,int MemoryEntries);

public sealed record GalleryLaunchPreference(string Id,GalleryLaunchPreferenceScope Scope,string? ScopeId,string ApplicationLibraryItemId,string ArgumentsTemplate,bool HideFrontendWhileRunning=false,bool RestoreFrontendAfterExit=false,ApplicationTrackingMode TrackingMode=ApplicationTrackingMode.None);
public sealed record GalleryMediaLaunchRequest(GalleryMediaItem MediaItem,bool DirectUserAction,string? PreferredApplicationId=null,string? ArgumentsTemplate=null,bool AllowDefaultFallback=true);
public sealed record GalleryMediaLaunchResult(bool Succeeded,bool UsedPreferredApplication,string? Error=null,int? ProcessId=null);

public sealed record GalleryViewport(double ViewportWidth,double ViewportHeight,double ImageWidth,double ImageHeight,double Scale,double PanX,double PanY,GalleryZoomMode Mode);
public sealed record GallerySlideshowRequest(IReadOnlyList<GalleryMediaItem> Items,string? OriginatingItemId,int IntervalSeconds=5,bool Shuffle=false,bool Loop=true,GallerySlideshowTransition Transition=GallerySlideshowTransition.Fade,bool ShowFilename=false,bool PauseVideos=true);
public sealed record GallerySlideshowState(bool Active,string? CurrentItemId,int CurrentIndex,bool Paused);
public sealed record GalleryApplicationState(GalleryViewKind CurrentView,string? SelectedGalleryItemId,string? SelectedGallerySourceId,string? SelectedAlbumId,GalleryGridSize GridSize,GallerySortMode SortMode,string? Filter,GalleryZoomMode ViewerZoomMode,int SlideshowInterval,double ScrollPosition=0);
public sealed record GalleryBackgroundSelection(string MediaItemId,string ManagedPath,GalleryZoomMode DisplayMode,DateTime UpdatedAtUtc);
