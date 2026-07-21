using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Gallery.Slideshows;

public sealed class SlideshowService : ISlideshowService,IAsyncDisposable,IDisposable
{
    private CancellationTokenSource? _run;private Task? _loop;private bool _foreground=true;private GallerySlideshowState _state=new(false,null,-1,false);
    public event EventHandler<GalleryMediaItem>? ItemChanged;
    public GallerySlideshowState State=>_state;
    public async Task StartAsync(GallerySlideshowRequest request,CancellationToken cancellationToken=default)
    {
        await StopAsync(GallerySlideshowStopReason.Cancelled,cancellationToken).ConfigureAwait(false);GalleryMediaItem[] items=request.Items.Where(value=>value.Available&&!value.Hidden&&value.MediaType is GalleryMediaType.Image or GalleryMediaType.AnimatedImage).ToArray();if(items.Length==0)return;if(request.Shuffle)Shuffle(items);int start=Math.Max(0,Array.FindIndex(items,value=>value.Id==request.OriginatingItemId));_run=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);_state=new(true,items[start].Id,start,false);ItemChanged?.Invoke(this,items[start]);_loop=RunAsync(items,start,request,_run.Token);
    }
    public async Task StopAsync(GallerySlideshowStopReason reason,CancellationToken cancellationToken=default){CancellationTokenSource? run=_run;Task? loop=_loop;_run=null;_loop=null;if(run is not null){run.Cancel();if(loop is not null)try{await loop.ConfigureAwait(false);}catch(OperationCanceledException){}run.Dispose();}_state=new(false,_state.CurrentItemId,_state.CurrentIndex,false);}
    public void SetForeground(bool foreground){_foreground=foreground;_state=_state with{Paused=!foreground&&_state.Active};}
    private async Task RunAsync(GalleryMediaItem[] items,int index,GallerySlideshowRequest request,CancellationToken token){while(!token.IsCancellationRequested){while(!_foreground)await Task.Delay(100,token).ConfigureAwait(false);await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(request.IntervalSeconds,1,3600)),token).ConfigureAwait(false);if(!_foreground)continue;index++;if(index>=items.Length){if(!request.Loop){_state=new(false,items[^1].Id,items.Length-1,false);return;}index=0;}_state=new(true,items[index].Id,index,false);ItemChanged?.Invoke(this,items[index]);}}
    private static void Shuffle<T>(T[] items){Random.Shared.Shuffle(items);}
    public async ValueTask DisposeAsync()=>await StopAsync(GallerySlideshowStopReason.Shutdown).ConfigureAwait(false);
    public void Dispose()=>StopAsync(GallerySlideshowStopReason.Shutdown).GetAwaiter().GetResult();
}
