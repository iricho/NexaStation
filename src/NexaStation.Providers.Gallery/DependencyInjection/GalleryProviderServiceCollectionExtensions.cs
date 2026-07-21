using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.Providers.Gallery.Metadata;
using NexaStation.Providers.Gallery.Scanning;
using NexaStation.Providers.Gallery.Sources;
using NexaStation.Providers.Gallery.Thumbnails;
using NexaStation.Providers.Gallery.Watching;

namespace NexaStation.Providers.Gallery.DependencyInjection;

public static class GalleryProviderServiceCollectionExtensions
{
    public static IServiceCollection AddGalleryProvider(this IServiceCollection services)=>services
        .AddSingleton<IGallerySourceScanner,GallerySourceScanner>()
        .AddSingleton<IGalleryMediaInspector,WindowsGalleryMediaInspector>()
        .AddSingleton<IGalleryThumbnailService,GalleryThumbnailService>()
        .AddSingleton<IGalleryLibraryService,GalleryLibraryService>()
        .AddSingleton<IGallerySourceWatcherCoordinator,GallerySourceWatcherCoordinator>();
}
