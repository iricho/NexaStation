using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace NexaStation.MediaPlayer.Playback;

public sealed class WindowsMediaPlaybackEngine : IMediaPlaybackEngine, IWindowsMediaPlayerAccessor
{
    private readonly Windows.Media.Playback.MediaPlayer _player = new() { AutoPlay = false };
    private MediaPlaybackItem? _item;
    private TaskCompletionSource<MediaEngineOpenResult>? _openCompletion;
    private bool _disposed;

    public WindowsMediaPlaybackEngine()
    {
        _player.MediaOpened += OnMediaOpened;
        _player.MediaFailed += OnMediaFailed;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPositionChanged;
        _player.PlaybackSession.NaturalDurationChanged += OnNaturalDurationChanged;
        _player.PlaybackSession.BufferingProgressChanged += OnBufferingProgressChanged;
    }

    public Windows.Media.Playback.MediaPlayer MediaPlayer => _player;
    public event EventHandler<MediaEngineStateChangedEventArgs>? StateChanged;

    public async Task<MediaEngineOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StorageFile file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(request.LocalPath)).AsTask(cancellationToken).ConfigureAwait(false);
        MediaSource source = MediaSource.CreateFromStorageFile(file);
        if (!string.IsNullOrWhiteSpace(request.ExternalSubtitlePath)) await AddSubtitleAsync(source, request.ExternalSubtitlePath, cancellationToken).ConfigureAwait(false);
        _item = new MediaPlaybackItem(source);
        _openCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => _openCompletion.TrySetCanceled(cancellationToken));
        _player.Source = _item;
        return await _openCompletion.Task.ConfigureAwait(false);
    }

    public Task PlayAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.Play(); return Task.CompletedTask; }
    public Task PauseAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.Pause(); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.Pause(); _player.PlaybackSession.Position = TimeSpan.Zero; return Task.CompletedTask; }
    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.PlaybackSession.Position = position; return Task.CompletedTask; }
    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.Volume = Math.Clamp(volume, 0, 1); return Task.CompletedTask; }
    public Task SetMutedAsync(bool muted, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.IsMuted = muted; return Task.CompletedTask; }
    public Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); _player.PlaybackSession.PlaybackRate = rate; return Task.CompletedTask; }

    public Task SelectAudioTrackAsync(string? trackId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_item is null || trackId is null) return Task.CompletedTask;
        for (int index = 0; index < _item.AudioTracks.Count; index++)
            if (string.Equals($"audio-{index}", trackId, StringComparison.Ordinal)) { _item.AudioTracks.SelectedIndex = index; break; }
        return Task.CompletedTask;
    }

    public Task SelectSubtitleTrackAsync(string? trackId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_item is null) return Task.CompletedTask;
        for (uint index = 0; index < _item.TimedMetadataTracks.Count; index++)
        {
            bool selected = string.Equals($"subtitle-{index}", trackId, StringComparison.Ordinal);
            _item.TimedMetadataTracks.SetPresentationMode(index, selected ? TimedMetadataTrackPresentationMode.PlatformPresented : TimedMetadataTrackPresentationMode.Disabled);
        }
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); _player.Pause(); _player.Source = null; _item = null; _openCompletion = null; return Task.CompletedTask;
    }

    private void OnMediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
    {
        MediaPlaybackSession session = sender.PlaybackSession;
        IReadOnlyList<MediaTrackDescriptor> audio = _item is null ? [] : _item.AudioTracks.Select((track, index) => new MediaTrackDescriptor($"audio-{index}", string.IsNullOrWhiteSpace(track.Label) ? $"Audio {index + 1}" : track.Label, track.Language)).ToArray();
        IReadOnlyList<MediaTrackDescriptor> subtitles = _item is null ? [] : _item.TimedMetadataTracks.Select((track, index) => new MediaTrackDescriptor($"subtitle-{index}", string.IsNullOrWhiteSpace(track.Label) ? $"Subtitle {index + 1}" : track.Label, track.Language, IsExternal: track.Id?.Contains("external", StringComparison.OrdinalIgnoreCase) == true)).ToArray();
        _openCompletion?.TrySetResult(new(session.NaturalDuration, session.CanSeek, audio, subtitles));
        Raise(MediaPlaybackState.Ready);
    }

    private void OnMediaFailed(Windows.Media.Playback.MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        string message = "This video could not be played in NexaStation.";
        _openCompletion?.TrySetException(new InvalidDataException(message));
        Raise(MediaPlaybackState.Failed, args.Error.ToString(), message);
    }
    private void OnMediaEnded(Windows.Media.Playback.MediaPlayer sender, object args) => Raise(MediaPlaybackState.Ended);
    private void OnPositionChanged(MediaPlaybackSession sender, object args) => Raise(Map(sender.PlaybackState));
    private void OnNaturalDurationChanged(MediaPlaybackSession sender, object args) => Raise(Map(sender.PlaybackState));
    private void OnBufferingProgressChanged(MediaPlaybackSession sender, object args) => Raise(Map(sender.PlaybackState));
    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args) => Raise(Map(sender.PlaybackState));
    private void Raise(MediaPlaybackState state, string? code = null, string? message = null)
    {
        MediaPlaybackSession session = _player.PlaybackSession;
        StateChanged?.Invoke(this, new(new(state, session.Position, session.NaturalDuration, session.BufferingProgress, code, message)));
    }
    private static MediaPlaybackState Map(Windows.Media.Playback.MediaPlaybackState state) => state switch { Windows.Media.Playback.MediaPlaybackState.Playing => MediaPlaybackState.Playing, Windows.Media.Playback.MediaPlaybackState.Paused => MediaPlaybackState.Paused, Windows.Media.Playback.MediaPlaybackState.Buffering => MediaPlaybackState.Buffering, Windows.Media.Playback.MediaPlaybackState.Opening => MediaPlaybackState.Opening, _ => MediaPlaybackState.Ready };

    private static async Task AddSubtitleAsync(MediaSource source, string path, CancellationToken token)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".srt", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Only SRT and VTT external subtitles are accepted.");
        StorageFile subtitle = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(token).ConfigureAwait(false);
        TimedTextSource text = TimedTextSource.CreateFromStream(await subtitle.OpenReadAsync().AsTask(token).ConfigureAwait(false), extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase) ? "text/vtt" : "application/x-subrip");
        source.ExternalTimedTextSources.Add(text);
    }

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _player.MediaOpened -= OnMediaOpened; _player.MediaFailed -= OnMediaFailed; _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged; _player.PlaybackSession.PositionChanged -= OnPositionChanged; _player.PlaybackSession.NaturalDurationChanged -= OnNaturalDurationChanged; _player.PlaybackSession.BufferingProgressChanged -= OnBufferingProgressChanged;
        _player.Dispose();
    }
}
