using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.DesktopControl.Input;
using NexaStation.DesktopControl.Profiles;
using NexaStation.DesktopControl.Safety;
using NexaStation.DesktopControl.Windows;

namespace NexaStation.DesktopControl.DependencyInjection;

public static class DesktopControlServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopControl(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopControllerStateSource, WindowsGamepadDesktopStateSource>();
        services.AddSingleton<IDesktopInputInjector, DesktopInputInjector>();
        services.AddSingleton<IDesktopControlSafetyGuard, WindowsIntegrityGuard>();
        services.AddSingleton<IPhysicalMouseActivityMonitor, PhysicalMouseActivityMonitor>();
        services.AddSingleton<IForegroundApplicationTracker, ForegroundApplicationTracker>();
        services.AddSingleton<DesktopControlSession>();
        services.AddSingleton<IDesktopControlSession>(provider => provider.GetRequiredService<DesktopControlSession>());
        return services;
    }
}
