using Microsoft.Extensions.DependencyInjection;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.Input.Glyphs;
using NexaStation.Input.Mapping;

namespace NexaStation.Input.DependencyInjection;

public static class InputServiceCollectionExtensions
{
    public static IServiceCollection AddAdaptiveInput(this IServiceCollection services)
    {
        services.AddSingleton<ActiveInputModeService>();
        services.AddSingleton<IActiveInputModeService>(provider => provider.GetRequiredService<ActiveInputModeService>());
        services.AddSingleton<IInputDeviceLifecycleService>(provider => provider.GetRequiredService<ActiveInputModeService>());
        services.AddSingleton<IControllerFamilyDetector, ControllerFamilyDetector>();
        services.AddSingleton<IControllerDeviceResolver, ControllerDeviceResolver>();
        services.AddSingleton<InputBindingService>();
        services.AddSingleton<IInputBindingResolver>(provider => provider.GetRequiredService<InputBindingService>());
        services.AddSingleton<IInputContextService, InputContextService>();
        services.AddSingleton<IInputProfileStore, InMemoryInputProfileStore>();
        services.AddSingleton<InputMappingValidator>();
        services.AddSingleton<InputMappingService>();
        services.AddSingleton<InputGlyphService>();
        services.AddSingleton<IInputGlyphService>(provider => provider.GetRequiredService<InputGlyphService>());
        services.AddSingleton<IInputPromptPreferenceService>(provider => provider.GetRequiredService<InputGlyphService>());
        return services;
    }
}
