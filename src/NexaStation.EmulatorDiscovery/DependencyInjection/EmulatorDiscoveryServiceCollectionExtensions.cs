using Microsoft.Extensions.DependencyInjection;
using NexaStation.EmulatorDiscovery.Discovery;
using NexaStation.EmulatorDiscovery.Sources;

namespace NexaStation.EmulatorDiscovery.DependencyInjection;

public static class EmulatorDiscoveryServiceCollectionExtensions
{
    public static IServiceCollection AddEmulatorDiscovery(this IServiceCollection services)
    {
        services.AddSingleton<IEmulatorDiscoverySource, BoundedFolderEmulatorSource>();
        services.AddSingleton<IEmulatorDiscoverySource, RegistryEmulatorSource>();
        services.AddSingleton<IEmulatorDiscoveryService, EmulatorDiscoveryService>();
        return services;
    }
}
