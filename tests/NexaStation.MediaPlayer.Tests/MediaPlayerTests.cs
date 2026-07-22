using NexaStation.MediaPlayer.ExternalPlayers;
using NexaStation.MediaPlayer.Playback;
using NexaStation.Core;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.MediaPlayer.Tests;

public sealed class MediaPlayerTests : IDisposable
{
    private readonly string _mediaPath = Path.Combine(Path.GetTempPath(), $"nexa-media-{Guid.NewGuid():N}.mp4");
    public MediaPlayerTests() => File.WriteAllBytes(_mediaPath, [0]);
    public void Dispose() => File.Delete(_mediaPath);

    [Fact] public async Task Valid_local_video_opens_and_defaults_to_fit()
    {
        FakeEngine engine = new(); MemoryProgress progress = new(); NexaMediaPlayerService service = new(engine, progress, new MemoryPreferences());
        MediaOpenResult result = await service.OpenAsync(Request());
        Assert.True(result.Succeeded); Assert.Equal(MediaAspectRatioMode.Fit, result.Snapshot.AspectRatio); Assert.Equal(MediaPlaybackState.Ready, result.Snapshot.State);
    }

    [Fact] public async Task Play_pause_stop_and_seek_are_clamped()
    {
        FakeEngine engine = new(); NexaMediaPlayerService service = new(engine, new MemoryProgress(), new MemoryPreferences()); await service.OpenAsync(Request());
        await service.PlayAsync(); Assert.Equal(MediaPlaybackState.Playing, service.Current.State);
        await service.SeekAsync(TimeSpan.FromMinutes(20)); Assert.Equal(TimeSpan.FromMinutes(10), engine.Position);
        await service.PauseAsync(); Assert.Equal(MediaPlaybackState.Paused, service.Current.State);
        await service.StopAsync(); Assert.Equal(TimeSpan.Zero, service.Current.Position);
    }

    [Fact] public async Task Volume_mute_and_allowed_rate_persist()
    {
        MemoryPreferences preferences = new(); NexaMediaPlayerService service = new(new FakeEngine(), new MemoryProgress(), preferences); await service.OpenAsync(Request()); await service.SetVolumeAsync(.4); await service.SetMutedAsync(true); await service.SetPlaybackRateAsync(1.5);
        Assert.Equal(.4, preferences.Value.Volume); Assert.True(preferences.Value.Muted); await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SetPlaybackRateAsync(1.1));
    }

    [Fact] public void Resume_thresholds_and_completion_are_bounded()
    {
        Assert.Null(ResumePolicy.Offer(new("x", 20, 600, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        Assert.Equal(TimeSpan.FromMinutes(2), ResumePolicy.Offer(new("x", 120, 600, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        Assert.True(ResumePolicy.IsCompleted(TimeSpan.FromSeconds(575), TimeSpan.FromMinutes(10)));
        Assert.False(ResumePolicy.IsCompleted(TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(10)));
    }

    [Fact] public async Task Missing_file_fails_safely()
    {
        NexaMediaPlayerService service = new(new FakeEngine(), new MemoryProgress(), new MemoryPreferences()); MediaOpenRequest request = Request() with { LocalPath = _mediaPath + ".missing" };
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.OpenAsync(request));
    }

    [Fact] public void Unsafe_external_player_template_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => ExternalMediaPlayerService.RenderArguments("{MEDIA_PATH} & whoami", _mediaPath));
        Assert.Throws<InvalidOperationException>(() => ExternalMediaPlayerService.RenderArguments("--play", _mediaPath));
    }

    [Theory]
    [InlineData(ContentLifecycleStatus.Unassigned,ContentLifecycleStatus.Playing)]
    [InlineData(ContentLifecycleStatus.Backlog,ContentLifecycleStatus.Playing)]
    [InlineData(ContentLifecycleStatus.Completed,ContentLifecycleStatus.Completed)]
    [InlineData(ContentLifecycleStatus.Dropped,ContentLifecycleStatus.Dropped)]
    public async Task NativeGalleryPlaybackStartsWatchingWithoutOverwritingTerminalStates(ContentLifecycleStatus initial,ContentLifecycleStatus expected)
    {
        FakeEngine engine=new();MemoryLifecycle lifecycle=new(initial);NexaMediaPlayerService service=new(engine,new MemoryProgress(),new MemoryPreferences(),lifecycle);await service.OpenAsync(Request() with{SourceType=MediaSourceType.GalleryVideo});await service.PlayAsync();Assert.Equal(expected,lifecycle.Status);
    }

    [Fact]
    public async Task NaturalGalleryVideoEndMarksWatchingAsWatched()
    {
        FakeEngine engine=new();MemoryLifecycle lifecycle=new(ContentLifecycleStatus.Backlog);NexaMediaPlayerService service=new(engine,new MemoryProgress(),new MemoryPreferences(),lifecycle);await service.OpenAsync(Request() with{SourceType=MediaSourceType.GalleryVideo});await service.PlayAsync();engine.Raise(MediaPlaybackState.Ended);for(int i=0;i<20&&lifecycle.Status!=ContentLifecycleStatus.Completed;i++)await Task.Delay(10);Assert.Equal(ContentLifecycleStatus.Completed,lifecycle.Status);
    }

    [Fact]
    public async Task NonGalleryAndCancelledPlaybackDoNotInferLifecycleCompletion()
    {
        FakeEngine engine=new();MemoryLifecycle lifecycle=new(ContentLifecycleStatus.Backlog);NexaMediaPlayerService service=new(engine,new MemoryProgress(),new MemoryPreferences(),lifecycle);await service.OpenAsync(Request());await service.PlayAsync();engine.Raise(MediaPlaybackState.Ended);await Task.Delay(20);Assert.Equal(ContentLifecycleStatus.Backlog,lifecycle.Status);
    }

    private MediaOpenRequest Request() => new() { MediaItemId = "video", LocalPath = _mediaPath, DisplayTitle = "Video", SourceType = MediaSourceType.LocalVideo };

    private sealed class FakeEngine : IMediaPlaybackEngine
    {
        public event EventHandler<MediaEngineStateChangedEventArgs>? StateChanged; public TimeSpan Position { get; private set; }
        public Task<MediaEngineOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken) => Task.FromResult(new MediaEngineOpenResult(TimeSpan.FromMinutes(10), true, [], []));
        public Task PlayAsync(CancellationToken cancellationToken) => Task.CompletedTask; public Task PauseAsync(CancellationToken cancellationToken) => Task.CompletedTask; public Task StopAsync(CancellationToken cancellationToken) { Position = TimeSpan.Zero; return Task.CompletedTask; }
        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken) { Position = position; return Task.CompletedTask; } public Task SetVolumeAsync(double volume, CancellationToken cancellationToken) => Task.CompletedTask; public Task SetMutedAsync(bool muted, CancellationToken cancellationToken) => Task.CompletedTask; public Task SetPlaybackRateAsync(double rate, CancellationToken cancellationToken) => Task.CompletedTask; public Task SelectAudioTrackAsync(string? trackId, CancellationToken cancellationToken) => Task.CompletedTask; public Task SelectSubtitleTrackAsync(string? trackId, CancellationToken cancellationToken) => Task.CompletedTask; public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask; public void Dispose() { }
        public void Raise(MediaPlaybackState state)=>StateChanged?.Invoke(this,new(new(state,Position,TimeSpan.FromMinutes(10),0)));
    }
    private sealed class MemoryProgress : IMediaPlaybackProgressStore { public Dictionary<string, MediaPlaybackProgress> Values { get; } = []; public Task<MediaPlaybackProgress?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(Values.GetValueOrDefault(id)); public Task SaveAsync(MediaPlaybackProgress value, CancellationToken cancellationToken = default) { Values[value.MediaItemId] = value; return Task.CompletedTask; } public Task DeleteAsync(string id, CancellationToken cancellationToken = default) { Values.Remove(id); return Task.CompletedTask; } }
    private sealed class MemoryPreferences : IMediaPlayerPreferencesStore { public MediaPlayerPreferences Value { get; private set; } = new(ControlsInactivityDelay: TimeSpan.FromSeconds(4)); public Task<MediaPlayerPreferences> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value); public Task SaveAsync(MediaPlayerPreferences value, CancellationToken cancellationToken = default) { Value = value; return Task.CompletedTask; } }
    private sealed class MemoryLifecycle(ContentLifecycleStatus initial):IContentLifecycleService { public ContentLifecycleStatus Status{get;private set;}=initial;public Task<ContentLifecycleStatus> GetStatusAsync(ContentLifecycleTarget target,CancellationToken cancellationToken=default)=>Task.FromResult(Status);public Task<IReadOnlyList<ContentLifecycleEntry>> QueryAsync(ContentLifecycleQuery query,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<ContentLifecycleEntry>>([]);public Task SetStatusAsync(ContentLifecycleTarget target,ContentLifecycleStatus status,CancellationToken cancellationToken=default){Status=status;return Task.CompletedTask;}public Task ClearStatusAsync(ContentLifecycleTarget target,CancellationToken cancellationToken=default){Status=ContentLifecycleStatus.Unassigned;return Task.CompletedTask;}public Task<int> ClearStaleTargetsAsync(ContentLifecycleTargetType targetType,IReadOnlyCollection<string> validTargetIds,CancellationToken cancellationToken=default)=>Task.FromResult(0); }
}
