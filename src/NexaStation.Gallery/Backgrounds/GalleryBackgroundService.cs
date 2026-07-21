using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Recovery;

namespace NexaStation.Gallery.Backgrounds;

public sealed class GalleryBackgroundService(IGalleryThumbnailService thumbnails,IGalleryPersistence persistence,INexaStationPaths? paths=null) : IGalleryBackgroundService
{
    private readonly string _root=(paths??new NexaStationPaths()).Backgrounds;
    public async Task<GalleryBackgroundSelection> SetAsync(GalleryMediaItem item,GalleryZoomMode displayMode,CancellationToken cancellationToken=default)
    {
        if(item.MediaType is not (GalleryMediaType.Image or GalleryMediaType.AnimatedImage))throw new InvalidOperationException("Only images can be used as a NexaStation background.");GalleryThumbnailResult thumbnail=await thumbnails.GetOrCreateAsync(new(item,GalleryThumbnailType.DetailsLarge,1920),cancellationToken).ConfigureAwait(false);if(!thumbnail.Succeeded||thumbnail.LocalCachePath is null)throw new InvalidOperationException(thumbnail.Error??"The background image could not be prepared.");Directory.CreateDirectory(_root);string managed=Path.Combine(_root,"gallery-background.png");string temporary=managed+".tmp";File.Copy(thumbnail.LocalCachePath,temporary,true);File.Move(temporary,managed,true);GalleryBackgroundSelection selection=new(item.Id,managed,displayMode,DateTime.UtcNow);await persistence.SaveBackgroundAsync(selection,cancellationToken).ConfigureAwait(false);return selection;
    }
    public async Task ResetAsync(CancellationToken cancellationToken=default){GalleryBackgroundSelection? current=await persistence.GetBackgroundAsync(cancellationToken).ConfigureAwait(false);await persistence.SaveBackgroundAsync(null,cancellationToken).ConfigureAwait(false);if(current is not null&&File.Exists(current.ManagedPath))File.Delete(current.ManagedPath);}
}
