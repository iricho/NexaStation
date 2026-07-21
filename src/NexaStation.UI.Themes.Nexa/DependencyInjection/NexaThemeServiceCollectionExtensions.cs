using Microsoft.Extensions.DependencyInjection;
using NexaStation.UI.Themes;
namespace NexaStation.UI.Themes.Nexa.DependencyInjection;
public static class NexaThemeServiceCollectionExtensions { public static IServiceCollection AddNexaDefaultTheme(this IServiceCollection services){services.AddSingleton<INexaTheme,NexaTheme>();return services;} }
