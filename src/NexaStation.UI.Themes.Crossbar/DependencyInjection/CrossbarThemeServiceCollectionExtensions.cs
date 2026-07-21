using Microsoft.Extensions.DependencyInjection;using NexaStation.UI.Themes;
namespace NexaStation.UI.Themes.Crossbar.DependencyInjection;
public static class CrossbarThemeServiceCollectionExtensions{public static IServiceCollection AddNexaCrossbarTheme(this IServiceCollection services){services.AddSingleton<INexaTheme,CrossbarTheme>();return services;}}
