using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.Providers.Steam.Artwork;
using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Providers.Steam.Launching;
using NexaStation.Providers.Steam.Manifests;
using NexaStation.Recovery;

namespace NexaStation.Providers.Steam.DependencyInjection;

public static class SteamServiceCollectionExtensions
{
    public static IServiceCollection AddSteamProvider(this IServiceCollection services)=>services
        .AddSingleton<IKeyValuesParser,KeyValuesParser>()
        .AddSingleton<ISteamAppManifestParser,SteamAppManifestParser>()
        .AddSingleton<ISteamInstallationLocator,SteamInstallationLocator>()
        .AddSingleton<ISteamLibraryLocator,SteamLibraryLocator>()
        .AddSingleton<ISteamApplicationScanner,SteamApplicationScanner>()
        .AddSingleton<ISteamArtworkLocator,SteamArtworkLocator>()
        .AddSingleton<IProviderArtworkCache>(provider=>new ProviderArtworkCache(provider.GetRequiredService<INexaStationPaths>()))
        .AddSingleton<ISteamLaunchTargetFactory,SteamLaunchTargetFactory>()
        .AddSingleton<IProcessSnapshotProvider,WindowsProcessSnapshotProvider>()
        .AddSingleton<ILaunchedApplicationDetector,SteamLaunchedApplicationDetector>()
        .AddSingleton<SteamLibraryProvider>()
        .AddSingleton<ILibraryProvider>(provider=>provider.GetRequiredService<SteamLibraryProvider>());
}
