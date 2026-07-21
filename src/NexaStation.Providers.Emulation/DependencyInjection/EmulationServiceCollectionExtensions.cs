using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.Providers.Emulation.Adapters;
using NexaStation.Providers.Emulation.Definitions;
using NexaStation.Providers.Emulation.Discovery;
using NexaStation.Providers.Emulation.Identification;
using NexaStation.Providers.Emulation.Launching;
using NexaStation.Providers.Emulation.Scanning;

namespace NexaStation.Providers.Emulation.DependencyInjection;

public static class EmulationServiceCollectionExtensions
{
    public static IServiceCollection AddEmulationProvider(this IServiceCollection services)
    {
        services.AddSingleton<IEmulatorCapabilityProbe, EmulatorCapabilityProbe>();
        services.AddSingleton<IGameIdentifierReader, FilenameGameIdentifier>();
        services.AddSingleton<IGameSourceScanner, GameSourceScanner>();
        services.AddSingleton<IRetroArchCoreDiscovery, RetroArchCoreDiscovery>();
        services.AddSingleton<IEmulatorAdapter, DolphinAdapter>();services.AddSingleton<IEmulatorAdapter, Pcsx2Adapter>();services.AddSingleton<IEmulatorAdapter, PpssppAdapter>();services.AddSingleton<IEmulatorAdapter, RetroArchAdapter>();services.AddSingleton<IEmulatorAdapter, Rpcs3Adapter>();services.AddSingleton<IEmulatorAdapter, CustomEmulatorAdapter>();
        services.AddSingleton<IEmulatorAdapterRegistry, EmulatorAdapterRegistry>();
        services.AddSingleton<IEmulationLibraryService, EmulationLibraryService>();
        services.AddSingleton<IEmulatorLaunchResolver, EmulatorLaunchResolver>();
        services.AddSingleton<ILibraryProvider, EmulationLibraryProvider>();
        return services;
    }
}
