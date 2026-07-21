namespace NexaStation.MediaPlayer.Playback;

public sealed record MediaEngineOpenResult(TimeSpan Duration, bool CanSeek, IReadOnlyList<MediaTrackDescriptor> AudioTracks, IReadOnlyList<MediaTrackDescriptor> SubtitleTracks);
public sealed record MediaEngineState(MediaPlaybackState State, TimeSpan Position, TimeSpan Duration, double BufferedProgress, string? ErrorCode = null, string? ErrorMessage = null);
public sealed class MediaEngineStateChangedEventArgs(MediaEngineState state) : EventArgs { public MediaEngineState State { get; } = state; }

public interface IMediaPlaybackEngine : IDisposable
{
    event EventHandler<MediaEngineStateChangedEventArgs>? StateChanged;
    Task<MediaEngineOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken);
    Task PlayAsync(CancellationToken cancellationToken);
    Task PauseAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken);
    Task SetVolumeAsync(double volume, CancellationToken cancellationToken);
    Task SetMutedAsync(bool muted, CancellationToken cancellationToken);
    Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken);
    Task SelectAudioTrackAsync(string? trackId, CancellationToken cancellationToken);
    Task SelectSubtitleTrackAsync(string? trackId, CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}

public interface IWindowsMediaPlayerAccessor { Windows.Media.Playback.MediaPlayer MediaPlayer { get; } }
