using Microsoft.Extensions.DependencyInjection;
using NexaStation.Media.Associations;
using NexaStation.Media.Hub;
using NexaStation.Media.Recent;

namespace NexaStation.Media.DependencyInjection;
public static class MediaServiceCollectionExtensions{public static IServiceCollection AddMedia(this IServiceCollection services)=>services.AddSingleton<IRecentMediaService,RecentMediaService>().AddSingleton<IMediaAssociationService,MediaAssociationService>().AddSingleton<IMediaHubService,MediaHubService>();}
