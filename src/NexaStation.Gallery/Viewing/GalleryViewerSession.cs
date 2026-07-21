using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Gallery.Navigation;

namespace NexaStation.Gallery.Viewing;

public sealed class GalleryViewerSession(IGalleryPersistence persistence)
{
    private IReadOnlyList<GalleryMediaItem> _items=[];
    public GalleryMediaItem? Current { get; private set; }
    public string? OriginatingItemId { get; private set; }
    public GalleryViewport? Viewport { get; private set; }
    public async Task OpenAsync(IReadOnlyList<GalleryMediaItem> items,string itemId,double viewportWidth,double viewportHeight,CancellationToken cancellationToken=default)
    { _items=items;OriginatingItemId=itemId;Current=items.FirstOrDefault(value=>value.Id==itemId&&value.Available)??GalleryNavigator.Move(items,null,1);if(Current is null)return;await persistence.MarkOpenedAsync(Current.Id,DateTime.UtcNow,cancellationToken).ConfigureAwait(false);if(Current.Width>0&&Current.Height>0)Viewport=GalleryViewportController.Create(viewportWidth,viewportHeight,Current.Width.Value,Current.Height.Value); }
    public async Task<GalleryMediaItem?> MoveAsync(int offset,double viewportWidth,double viewportHeight,CancellationToken cancellationToken=default)
    { GalleryMediaItem? next=GalleryNavigator.Move(_items,Current?.Id,offset);if(next is null)return Current;Current=next;Viewport=next.Width>0&&next.Height>0?GalleryViewportController.Create(viewportWidth,viewportHeight,next.Width.Value,next.Height.Value):null;await persistence.MarkOpenedAsync(next.Id,DateTime.UtcNow,cancellationToken).ConfigureAwait(false);return next; }
    public async Task ToggleFavoriteAsync(CancellationToken cancellationToken=default){if(Current is null)return;await persistence.SetFavoriteAsync(Current.Id,!Current.Favorite,cancellationToken).ConfigureAwait(false);Current=Current with{Favorite=!Current.Favorite};}
    public void Zoom(double factor)=>Viewport=Viewport is null?null:GalleryViewportController.Zoom(Viewport,factor);
    public void Pan(double x,double y)=>Viewport=Viewport is null?null:GalleryViewportController.Pan(Viewport,x,y);
    public void ResetZoom(GalleryZoomMode mode=GalleryZoomMode.Fit)=>Viewport=Viewport is null?null:GalleryViewportController.Reset(Viewport,mode);
}
