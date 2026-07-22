using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.MediaPlayer.Playback;

public sealed class NexaMediaPlayerService : INexaMediaPlayerService, IDisposable
{
    private static readonly double[] AllowedRates = [0.5, 0.75, 1, 1.25, 1.5, 2];
    private readonly IMediaPlaybackEngine _engine;
    private readonly IMediaPlaybackProgressStore _progress;
    private readonly IMediaPlayerPreferencesStore _preferences;
    private readonly IContentLifecycleService? _contentLifecycle;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MediaPlaybackSnapshot _current = new();
    private MediaOpenRequest? _request;
    private DateTimeOffset _lastProgressWrite = DateTimeOffset.MinValue;
    private bool _disposed;

    public NexaMediaPlayerService(IMediaPlaybackEngine engine, IMediaPlaybackProgressStore progress, IMediaPlayerPreferencesStore preferences, IContentLifecycleService? contentLifecycle = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _contentLifecycle = contentLifecycle;
        _engine.StateChanged += OnEngineStateChanged;
    }

    public MediaPlaybackSnapshot Current => _current;
    public event EventHandler<MediaPlaybackChangedEventArgs>? PlaybackChanged;

    public async Task<MediaOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateLocalPath(request.LocalPath);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseCoreAsync(saveProgress: true, cancellationToken).ConfigureAwait(false);
            _request = request;
            MediaPlayerPreferences preferences = await _preferences.GetAsync(cancellationToken).ConfigureAwait(false);
            Update(new() { State = MediaPlaybackState.Opening, MediaItemId = request.MediaItemId, Title = SafeTitle(request), PrivacyLevel = request.PrivacyLevel, Volume = Math.Clamp(preferences.Volume, 0, 1), Muted = preferences.Muted, PlaybackRate = preferences.RememberPlaybackRate ? NormalizeRate(preferences.PlaybackRate) : 1, AspectRatio = preferences.AspectRatio, ControlsVisible = true });
            await _engine.SetVolumeAsync(_current.Volume, cancellationToken).ConfigureAwait(false);
            await _engine.SetMutedAsync(_current.Muted, cancellationToken).ConfigureAwait(false);
            await _engine.SetPlaybackRateAsync(_current.PlaybackRate, cancellationToken).ConfigureAwait(false);
            MediaPlaybackProgress? saved = request.ResumePolicy is MediaResumePolicy.IgnoreSavedProgress or MediaResumePolicy.StartFromBeginning ? null : await _progress.GetAsync(request.MediaItemId, cancellationToken).ConfigureAwait(false);
            TimeSpan? resume = ResumePolicy.Offer(saved);
            MediaEngineOpenResult opened = await _engine.OpenAsync(request, cancellationToken).ConfigureAwait(false);
            TimeSpan start = request.StartingPosition ?? (request.ResumePolicy == MediaResumePolicy.Resume ? resume ?? TimeSpan.Zero : TimeSpan.Zero);
            if (request.ResumePolicy == MediaResumePolicy.StartFromBeginning) start = TimeSpan.Zero;
            if (start > TimeSpan.Zero && opened.CanSeek) await _engine.SeekAsync(Clamp(start, opened.Duration), cancellationToken).ConfigureAwait(false);
            Update(_current with { State = MediaPlaybackState.Ready, Duration = opened.Duration, Position = Clamp(start, opened.Duration), CanSeek = opened.CanSeek, AudioTracks = opened.AudioTracks, SubtitleTracks = opened.SubtitleTracks, SelectedAudioTrack = opened.AudioTracks.Count > 0 ? opened.AudioTracks[0] : null, SelectedSubtitleTrack = opened.SubtitleTracks.FirstOrDefault(track => track.Id == request.PreferredSubtitleTrack) });
            return new(true, _current, ResumePosition: resume);
        }
        catch (Exception exception) when (exception is FileNotFoundException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            string code = exception is FileNotFoundException ? "FileMissing" : exception is UnauthorizedAccessException ? "AccessDenied" : "UnsupportedOrInvalidMedia";
            Update(_current with { State = MediaPlaybackState.Failed, ErrorCode = code, ErrorMessage = "This video could not be played in NexaStation." });
            return new(false, _current, code, _current.ErrorMessage, UnsupportedMedia: code == "UnsupportedOrInvalidMedia");
        }
        finally { _gate.Release(); }
    }

    public Task PlayAsync(CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.PlayAsync(cancellationToken).ConfigureAwait(false); Update(_current with { State = MediaPlaybackState.Playing, ControlsVisible = true }); await MarkWatchingAsync(cancellationToken).ConfigureAwait(false); });
    public Task PauseAsync(CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.PauseAsync(cancellationToken).ConfigureAwait(false); Update(_current with { State = MediaPlaybackState.Paused, ControlsVisible = true }); await SaveProgressAsync(force: true, cancellationToken).ConfigureAwait(false); });
    public Task StopAsync(CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.StopAsync(cancellationToken).ConfigureAwait(false); Update(_current with { State = MediaPlaybackState.Ready, Position = TimeSpan.Zero, ControlsVisible = true }); await SaveProgressAsync(force: true, cancellationToken).ConfigureAwait(false); });
    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { if (!_current.CanSeek) return; TimeSpan clamped = Clamp(position, _current.Duration); Update(_current with { State = MediaPlaybackState.Seeking, Position = clamped }); await _engine.SeekAsync(clamped, cancellationToken).ConfigureAwait(false); });
    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { double value = Math.Clamp(volume, 0, 1); await _engine.SetVolumeAsync(value, cancellationToken).ConfigureAwait(false); Update(_current with { Volume = value }); await SavePreferencesAsync(cancellationToken).ConfigureAwait(false); });
    public Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.SetMutedAsync(muted, cancellationToken).ConfigureAwait(false); Update(_current with { Muted = muted }); await SavePreferencesAsync(cancellationToken).ConfigureAwait(false); });
    public Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { double value = NormalizeRate(rate); await _engine.SetPlaybackRateAsync(value, cancellationToken).ConfigureAwait(false); Update(_current with { PlaybackRate = value }); MediaPlayerPreferences preferences = await _preferences.GetAsync(cancellationToken).ConfigureAwait(false); if (preferences.RememberPlaybackRate) await SavePreferencesAsync(cancellationToken).ConfigureAwait(false); });
    public Task SetAspectRatioAsync(MediaAspectRatioMode mode, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { Update(_current with { AspectRatio = mode }); await SavePreferencesAsync(cancellationToken).ConfigureAwait(false); });
    public Task SetFullscreenAsync(bool fullscreen, CancellationToken cancellationToken = default) => ExecuteAsync(() => { Update(_current with { Fullscreen = fullscreen }); return Task.CompletedTask; });
    public Task SetControlsVisibleAsync(bool visible, CancellationToken cancellationToken = default) => ExecuteAsync(() => { Update(_current with { ControlsVisible = visible }); return Task.CompletedTask; });
    public Task SelectAudioTrackAsync(string? trackId, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.SelectAudioTrackAsync(trackId, cancellationToken).ConfigureAwait(false); Update(_current with { SelectedAudioTrack = _current.AudioTracks.FirstOrDefault(track => track.Id == trackId) }); });
    public Task SelectSubtitleTrackAsync(string? trackId, CancellationToken cancellationToken = default) => ExecuteAsync(async () => { await _engine.SelectSubtitleTrackAsync(trackId, cancellationToken).ConfigureAwait(false); Update(_current with { SelectedSubtitleTrack = _current.SubtitleTracks.FirstOrDefault(track => track.Id == trackId) }); });
    public Task ForgetProgressAsync(string mediaItemId, CancellationToken cancellationToken = default) => _progress.DeleteAsync(mediaItemId, cancellationToken);

    public async Task CloseAsync(MediaCloseReason reason, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await CloseCoreAsync(saveProgress: reason != MediaCloseReason.Error, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await action().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task CloseCoreAsync(bool saveProgress, CancellationToken token)
    {
        if (_current.State == MediaPlaybackState.Closed) return;
        if (saveProgress) await SaveProgressAsync(force: true, token).ConfigureAwait(false);
        await _engine.CloseAsync(token).ConfigureAwait(false);
        _request = null; _lastProgressWrite = DateTimeOffset.MinValue;
        Update(new());
    }

    private async Task SaveProgressAsync(bool force, CancellationToken token)
    {
        if (_request is null || _current.Duration <= TimeSpan.Zero || _current.State == MediaPlaybackState.Failed) return;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!force && now - _lastProgressWrite < TimeSpan.FromSeconds(15)) return;
        bool completed = ResumePolicy.IsCompleted(_current.Position, _current.Duration);
        await _progress.SaveAsync(new(_request.MediaItemId, _current.Position.TotalSeconds, _current.Duration.TotalSeconds, completed, now, now, _request.PrivacyLevel == MediaPrivacyLevel.Private), token).ConfigureAwait(false);
        _lastProgressWrite = now;
    }

    private async Task SavePreferencesAsync(CancellationToken token)
    {
        MediaPlayerPreferences existing = await _preferences.GetAsync(token).ConfigureAwait(false);
        await _preferences.SaveAsync(existing with { Volume = _current.Volume, Muted = _current.Muted, PlaybackRate = _current.PlaybackRate, AspectRatio = _current.AspectRatio }, token).ConfigureAwait(false);
    }

    private void OnEngineStateChanged(object? sender, MediaEngineStateChangedEventArgs e)
    {
        Update(_current with { State = e.State.State, Position = e.State.Position, Duration = e.State.Duration, BufferedProgress = e.State.BufferedProgress, ErrorCode = e.State.ErrorCode, ErrorMessage = e.State.ErrorMessage });
        if (e.State.State is MediaPlaybackState.Playing or MediaPlaybackState.Paused) _ = SaveProgressAsync(force: false, CancellationToken.None);
        if (e.State.State == MediaPlaybackState.Ended)
        {
            _ = SaveProgressAsync(force: true, CancellationToken.None);
            _ = MarkWatchedAsync();
        }
    }

    private async Task MarkWatchingAsync(CancellationToken cancellationToken)
    {
        if (_contentLifecycle is null || _request?.SourceType != MediaSourceType.GalleryVideo) return;
        try
        {
            ContentLifecycleTarget target = ContentLifecycleTarget.GalleryMedia(_request.MediaItemId);
            ContentLifecycleStatus status = await _contentLifecycle.GetStatusAsync(target, cancellationToken).ConfigureAwait(false);
            if (status is ContentLifecycleStatus.Unassigned or ContentLifecycleStatus.Backlog)
                await _contentLifecycle.SetStatusAsync(target, ContentLifecycleStatus.Playing, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            // Playback is authoritative; optional organization metadata must not interrupt it.
        }
    }

    private async Task MarkWatchedAsync()
    {
        try
        {
            if (_contentLifecycle is null || _request?.SourceType != MediaSourceType.GalleryVideo) return;
            ContentLifecycleTarget target = ContentLifecycleTarget.GalleryMedia(_request.MediaItemId);
            if (await _contentLifecycle.GetStatusAsync(target, CancellationToken.None).ConfigureAwait(false) == ContentLifecycleStatus.Playing)
                await _contentLifecycle.SetStatusAsync(target, ContentLifecycleStatus.Completed, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Lifecycle metadata must never interrupt playback completion.
        }
    }

    private void Update(MediaPlaybackSnapshot snapshot) { _current = snapshot; PlaybackChanged?.Invoke(this, new(snapshot)); }
    private static TimeSpan Clamp(TimeSpan value, TimeSpan duration) => value < TimeSpan.Zero ? TimeSpan.Zero : duration > TimeSpan.Zero && value > duration ? duration : value;
    private static double NormalizeRate(double rate) => AllowedRates.Contains(rate) ? rate : throw new ArgumentOutOfRangeException(nameof(rate), "Playback rate must be one of the supported values.");
    private static void ValidateLocalPath(string path) { ArgumentException.ThrowIfNullOrWhiteSpace(path); if (Uri.TryCreate(path, UriKind.Absolute, out Uri? uri) && !uri.IsFile) throw new ArgumentException("Network media URLs are not supported.", nameof(path)); if (!File.Exists(path)) throw new FileNotFoundException("The media file could not be located.", path); }
    private static string SafeTitle(MediaOpenRequest request) => request.PrivacyLevel is MediaPrivacyLevel.Private or MediaPrivacyLevel.GuestTemporary ? "Private video" : request.DisplayTitle;

    public void Dispose() { if (_disposed) return; _disposed = true; _engine.StateChanged -= OnEngineStateChanged; _engine.Dispose(); _gate.Dispose(); }
}

public static class ResumePolicy
{
    public static TimeSpan? Offer(MediaPlaybackProgress? progress)
    {
        if (progress is null || progress.Completed || progress.PositionSeconds < 30 || progress.DurationSeconds <= 0) return null;
        TimeSpan position = TimeSpan.FromSeconds(progress.PositionSeconds), duration = TimeSpan.FromSeconds(progress.DurationSeconds);
        return IsCompleted(position, duration) ? null : position;
    }

    public static bool IsCompleted(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return false;
        TimeSpan remaining = duration - position;
        TimeSpan threshold = TimeSpan.FromTicks(Math.Min((long)(duration.Ticks * 0.05), TimeSpan.FromMinutes(2).Ticks));
        return remaining <= threshold;
    }
}
