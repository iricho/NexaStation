using Microsoft.Extensions.DependencyInjection;
using NexaStation.Interaction.ActionTargets;

namespace NexaStation.Interaction.DependencyInjection;

public static class InteractionServiceCollectionExtensions
{
    public static IServiceCollection AddNexaInteraction(this IServiceCollection services)
    {
        services.AddSingleton<IActionTargetService, ActionTargetService>();
        services.AddSingleton<PrimaryActionRouter>();
        services.AddSingleton<IPrimaryActionRouter>(provider => provider.GetRequiredService<PrimaryActionRouter>());
        services.AddSingleton<IPrimaryActionRegistration>(provider => provider.GetRequiredService<PrimaryActionRouter>());
        return services;
    }
}
