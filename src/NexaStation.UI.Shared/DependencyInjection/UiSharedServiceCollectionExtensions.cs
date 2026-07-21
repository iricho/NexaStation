using Microsoft.Extensions.DependencyInjection;
using NexaStation.UI.Navigation;
using NexaStation.UI.Settings;
using NexaStation.UI.Themes;

namespace NexaStation.UI.DependencyInjection;

public static class UiSharedServiceCollectionExtensions
{
    public static IServiceCollection AddNexaUiShared(this IServiceCollection services)
    {
        services.AddSingleton<IThemeNavigationStateService, DefaultThemeNavigationStateService>();
        services.AddSingleton<IThemeRuntimeContext, DefaultThemeRuntimeContext>();
        services.AddSingleton<IThemePageRegistry, ThemePageRegistry>();
        services.AddSingleton<IThemeManager, ThemeManager>();
        services.AddSingleton<ISettingsCatalog>(_ => BuiltInSettingsCatalog.Create());
        services.AddSingleton<ISettingsSearchService, SettingsSearchService>();
        return services;
    }
}
