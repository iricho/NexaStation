using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Gallery.Validation;

namespace NexaStation.Providers.Gallery.Watching;

public sealed class DebouncedGalleryChangeBuffer(TimeSpan delay) : IAsyncDisposable
{
    private readonly Dictionary<string,CancellationTokenSource> _pending=new(StringComparer.Ordinal);private readonly object _gate=new();private Func<string,CancellationToken,Task>? _callback;private CancellationToken _shutdown;
    public void Start(Func<string,CancellationToken,Task> callback,CancellationToken shutdown){_callback=callback;_shutdown=shutdown;}
    public void Signal(string sourceId){CancellationTokenSource current;lock(_gate){if(_pending.Remove(sourceId,out CancellationTokenSource? old)){old.Cancel();old.Dispose();}current=CancellationTokenSource.CreateLinkedTokenSource(_shutdown);_pending[sourceId]=current;}_=DispatchAsync(sourceId,current);}
    private async Task DispatchAsync(string sourceId,CancellationTokenSource source){try{await Task.Delay(delay,source.Token).ConfigureAwait(false);if(_callback is not null)await _callback(sourceId,source.Token).ConfigureAwait(false);}catch(OperationCanceledException){}finally{lock(_gate){if(_pending.TryGetValue(sourceId,out CancellationTokenSource? value)&&ReferenceEquals(value,source)){_pending.Remove(sourceId);source.Dispose();}}}}
    public ValueTask DisposeAsync(){lock(_gate){foreach(CancellationTokenSource source in _pending.Values){source.Cancel();source.Dispose();}_pending.Clear();}return ValueTask.CompletedTask;}
}

public sealed class GallerySourceWatcherCoordinator(IGalleryPersistence persistence,IAppLogger logger) : IGallerySourceWatcherCoordinator,IDisposable
{
    private readonly List<FileSystemWatcher> _watchers=[];private readonly DebouncedGalleryChangeBuffer _buffer=new(TimeSpan.FromMilliseconds(750));private CancellationTokenSource? _shutdown;private Func<string,CancellationToken,Task>? _refresh;
    public async Task StartAsync(Func<string,CancellationToken,Task> refreshSourceAsync,CancellationToken cancellationToken=default){_refresh=refreshSourceAsync;_shutdown=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);_buffer.Start(refreshSourceAsync,_shutdown.Token);await ReloadAsync(cancellationToken).ConfigureAwait(false);}
    public async Task ReloadAsync(CancellationToken cancellationToken=default)
    {
        DisposeWatchers();IReadOnlyList<GallerySource> sources=await persistence.GetSourcesAsync(cancellationToken).ConfigureAwait(false);List<GallerySource> watched=[];
        foreach(GallerySource source in sources.Where(value=>value.Enabled&&value.WatchForChanges&&Directory.Exists(value.FolderPath)).OrderBy(value=>value.NormalizedFolderPath.Length))
        {
            if(watched.Any(parent=>parent.IncludeSubfolders&&GalleryPathPolicy.IsInside(source.FolderPath,parent.FolderPath)))continue;
            try{FileSystemWatcher watcher=new(source.FolderPath){IncludeSubdirectories=source.IncludeSubfolders,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite|NotifyFilters.CreationTime,Filter="*",EnableRaisingEvents=true};watcher.Created+=(_,_)=>_buffer.Signal(source.Id);watcher.Changed+=(_,_)=>_buffer.Signal(source.Id);watcher.Deleted+=(_,_)=>_buffer.Signal(source.Id);watcher.Renamed+=(_,_)=>_buffer.Signal(source.Id);watcher.Error+=(_,args)=>{logger.LogError("GalleryWatcherOverflow","A Gallery watcher failed or overflowed; a full source refresh was scheduled.",args.GetException());_buffer.Signal(source.Id);};_watchers.Add(watcher);watched.Add(source);}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){logger.LogError("GalleryWatcherStartFailed","An optional Gallery watcher could not start.",exception);}
        }
    }
    public Task StopAsync(CancellationToken cancellationToken=default){_shutdown?.Cancel();DisposeWatchers();return Task.CompletedTask;}
    private void DisposeWatchers(){foreach(FileSystemWatcher watcher in _watchers){watcher.EnableRaisingEvents=false;watcher.Dispose();}_watchers.Clear();}
    public async ValueTask DisposeAsync(){await StopAsync().ConfigureAwait(false);await _buffer.DisposeAsync().ConfigureAwait(false);_shutdown?.Dispose();}
    public void Dispose(){StopAsync().GetAwaiter().GetResult();_buffer.DisposeAsync().AsTask().GetAwaiter().GetResult();_shutdown?.Dispose();}
}
