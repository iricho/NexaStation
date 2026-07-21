using Microsoft.Extensions.DependencyInjection;
using NexaStation.TextInput.Keyboard;
using NexaStation.TextInput.Sessions;
using NexaStation.TextInput.Suggestions;
using NexaStation.TextInput.Validation;

namespace NexaStation.TextInput.DependencyInjection;

public static class TextInputServiceCollectionExtensions
{
    public static IServiceCollection AddNexaTextInput(this IServiceCollection services) => services
        .AddSingleton<IKeyboardLayoutCatalog, BuiltInKeyboardLayoutCatalog>()
        .AddSingleton<TextInputValidator>()
        .AddSingleton<ILocalSuggestionSource, EmptyLocalSuggestionSource>()
        .AddSingleton<ILocalSuggestionService, LocalSuggestionService>()
        .AddSingleton<IOnScreenKeyboardService, OnScreenKeyboardService>();
}
