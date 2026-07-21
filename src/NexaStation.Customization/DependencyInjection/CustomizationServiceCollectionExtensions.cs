using Microsoft.Extensions.DependencyInjection;
using NexaStation.Customization.Collections;
using NexaStation.Customization.Dashboard;
using NexaStation.Customization.ImportExport;
using NexaStation.Customization.Layouts;
using NexaStation.Customization.Performance;
using NexaStation.Customization.Pinning;
using NexaStation.Customization.Services;
using NexaStation.Customization.Sounds;
using NexaStation.Customization.Themes;

namespace NexaStation.Customization.DependencyInjection;

public static class CustomizationServiceCollectionExtensions
{
    public static IServiceCollection AddCustomization(this IServiceCollection services)=>services
        .AddSingleton<IInterfaceLayoutService,InterfaceLayoutService>()
        .AddSingleton<IDashboardConfigurationService,DashboardConfigurationService>()
        .AddSingleton<IPinService,PinService>()
        .AddSingleton<ICollectionService,CollectionService>()
        .AddSingleton<ISmartCollectionQueryService,SmartCollectionQueryService>()
        .AddSingleton<IThemeService,ThemeService>()
        .AddSingleton<IThemeAssetService,ThemeAssetService>()
        .AddSingleton<IUiSoundPlayer,NullUiSoundPlayer>()
        .AddSingleton<IUiSoundService,UiSoundService>()
        .AddSingleton<IPerformancePresetService,PerformancePresetService>()
        .AddSingleton<IInterfaceConfigurationService,InterfaceConfigurationService>();
}
