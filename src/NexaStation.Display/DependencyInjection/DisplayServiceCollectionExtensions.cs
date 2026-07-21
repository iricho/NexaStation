using Microsoft.Extensions.DependencyInjection;
using NexaStation.Display.Calibration;
using NexaStation.Display.Handheld;
using NexaStation.Display.Monitors;
using NexaStation.Display.Scaling;

namespace NexaStation.Display.DependencyInjection;

public static class DisplayServiceCollectionExtensions
{
    public static IServiceCollection AddNexaDisplay(this IServiceCollection services) => services
        .AddSingleton<IDisplayPlatformAdapter, EmptyDisplayPlatformAdapter>()
        .AddSingleton<IDisplayPreferenceStore, InMemoryDisplayPreferenceStore>()
        .AddSingleton<IDisplaySelectionService, DisplaySelectionService>()
        .AddSingleton<IDisplayCalibrationStore, InMemoryDisplayCalibrationStore>()
        .AddSingleton<SafeAreaCalibrationService>()
        .AddSingleton<InterfaceScalePolicy>()
        .AddSingleton<BatteryVisualPolicy>();
}
