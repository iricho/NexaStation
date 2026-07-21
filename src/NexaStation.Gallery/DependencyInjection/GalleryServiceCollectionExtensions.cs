using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.Gallery.Albums;
using NexaStation.Gallery.Backgrounds;
using NexaStation.Gallery.Slideshows;
using NexaStation.Gallery.Viewing;

namespace NexaStation.Gallery.DependencyInjection;

public static class GalleryServiceCollectionExtensions
{
    public static IServiceCollection AddGallery(this IServiceCollection services)=>services
        .AddSingleton<GalleryViewerSession>()
        .AddSingleton<GalleryAlbumService>()
        .AddSingleton<ISlideshowService,SlideshowService>()
        .AddSingleton<IGalleryMediaLauncher,GalleryMediaLauncher>()
        .AddSingleton<IGalleryBackgroundService,GalleryBackgroundService>();
}
