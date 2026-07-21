namespace NexaStation.MediaPlayer.Playback;

public enum MediaPlaybackState { Closed, Opening, Ready, Playing, Paused, Buffering, Seeking, Ended, Failed }
public enum MediaSourceType { GalleryVideo, LocalVideo, Recording, UserSelectedFile, RecentMedia, UnknownLocalMedia }
public enum MediaPrivacyLevel { Normal, Private, GuestTemporary }
public enum MediaResumePolicy { Ask, Resume, StartFromBeginning, IgnoreSavedProgress }
public enum MediaCloseReason { UserBack, Navigation, Ended, Error, Shutdown }
public enum MediaAspectRatioMode { Fit, Fill, Original, Stretch }

public sealed record MediaQueueItem(string MediaItemId, string LocalPath, string DisplayTitle, MediaPrivacyLevel PrivacyLevel = MediaPrivacyLevel.Normal);
public sealed record MediaFocusOrigin(string PageId, string ScopeId, string ItemId, string ItemType);
public sealed record MediaTrackDescriptor(string Id, string Label, string? Language = null, string? Channels = null, bool IsExternal = false);

public sealed record MediaOpenRequest
{
    public required string MediaItemId { get; init; }
    public required string LocalPath { get; init; }
    public required string DisplayTitle { get; init; }
    public MediaSourceType SourceType { get; init; }
    public MediaPrivacyLevel PrivacyLevel { get; init; }
    public TimeSpan? StartingPosition { get; init; }
    public MediaResumePolicy ResumePolicy { get; init; } = MediaResumePolicy.Ask;
    public IReadOnlyList<MediaQueueItem> Queue { get; init; } = [];
    public string? PreferredAudioTrack { get; init; }
    public string? PreferredSubtitleTrack { get; init; }
    public string? ExternalSubtitlePath { get; init; }
    public MediaFocusOrigin? ReturnFocusOrigin { get; init; }
    public string? ActivityTargetId { get; init; }
}

public sealed record MediaPlaybackSnapshot
{
    public MediaPlaybackState State { get; init; } = MediaPlaybackState.Closed;
    public string? MediaItemId { get; init; }
    public string? Title { get; init; }
    public TimeSpan Duration { get; init; }
    public TimeSpan Position { get; init; }
    public double BufferedProgress { get; init; }
    public double Volume { get; init; } = 1;
    public bool Muted { get; init; }
    public double PlaybackRate { get; init; } = 1;
    public bool Fullscreen { get; init; }
    public bool ControlsVisible { get; init; } = true;
    public MediaAspectRatioMode AspectRatio { get; init; } = MediaAspectRatioMode.Fit;
    public MediaTrackDescriptor? SelectedAudioTrack { get; init; }
    public MediaTrackDescriptor? SelectedSubtitleTrack { get; init; }
    public IReadOnlyList<MediaTrackDescriptor> AudioTracks { get; init; } = [];
    public IReadOnlyList<MediaTrackDescriptor> SubtitleTracks { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool CanSeek { get; init; }
    public bool CanSelectAudioTracks => AudioTracks.Count > 1;
    public bool CanSelectSubtitles => SubtitleTracks.Count > 0;
    public MediaPrivacyLevel PrivacyLevel { get; init; }
}

public sealed record MediaOpenResult(bool Succeeded, MediaPlaybackSnapshot Snapshot, string? ErrorCode = null, string? ErrorMessage = null, TimeSpan? ResumePosition = null, bool UnsupportedMedia = false);
public sealed class MediaPlaybackChangedEventArgs(MediaPlaybackSnapshot snapshot) : EventArgs { public MediaPlaybackSnapshot Snapshot { get; } = snapshot; }

public interface INexaMediaPlayerService
{
    MediaPlaybackSnapshot Current { get; }
    event EventHandler<MediaPlaybackChangedEventArgs>? PlaybackChanged;
    Task<MediaOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken = default);
    Task PlayAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
    Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default);
    Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default);
    Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken = default);
    Task SetAspectRatioAsync(MediaAspectRatioMode mode, CancellationToken cancellationToken = default);
    Task SetFullscreenAsync(bool fullscreen, CancellationToken cancellationToken = default);
    Task SetControlsVisibleAsync(bool visible, CancellationToken cancellationToken = default);
    Task SelectAudioTrackAsync(string? trackId, CancellationToken cancellationToken = default);
    Task SelectSubtitleTrackAsync(string? trackId, CancellationToken cancellationToken = default);
    Task ForgetProgressAsync(string mediaItemId, CancellationToken cancellationToken = default);
    Task CloseAsync(MediaCloseReason reason, CancellationToken cancellationToken = default);
}

public sealed record MediaPlaybackProgress(string MediaItemId, double PositionSeconds, double DurationSeconds, bool Completed, DateTimeOffset LastPlayedAtUtc, DateTimeOffset UpdatedAtUtc, bool IsPrivate = false);
public sealed record MediaPlayerPreferences(double Volume = 1, bool Muted = false, double PlaybackRate = 1, bool RememberPlaybackRate = false, MediaAspectRatioMode AspectRatio = MediaAspectRatioMode.Fit, TimeSpan? ControlsInactivityDelay = null);

public interface IMediaPlaybackProgressStore
{
    Task<MediaPlaybackProgress?> GetAsync(string mediaItemId, CancellationToken cancellationToken = default);
    Task SaveAsync(MediaPlaybackProgress progress, CancellationToken cancellationToken = default);
    Task DeleteAsync(string mediaItemId, CancellationToken cancellationToken = default);
}

public interface IMediaPlayerPreferencesStore
{
    Task<MediaPlayerPreferences> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(MediaPlayerPreferences preferences, CancellationToken cancellationToken = default);
}
