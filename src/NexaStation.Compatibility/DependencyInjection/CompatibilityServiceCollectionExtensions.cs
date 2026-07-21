using Microsoft.Extensions.DependencyInjection;
using NexaStation.Compatibility.Feeds;
using NexaStation.Compatibility.Matching;
using NexaStation.Compatibility.Validation;

namespace NexaStation.Compatibility.DependencyInjection;

public static class CompatibilityServiceCollectionExtensions
{
    public static IServiceCollection AddCompatibility(this IServiceCollection services, CompatibilityFeedSettings? feedSettings = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICompatibilityProfileValidator, CompatibilityProfileValidator>();
        services.AddSingleton<ICompatibilityProfileResolver, CompatibilityProfileResolver>();
        services.AddSingleton(feedSettings ?? new CompatibilityFeedSettings());
        services.AddSingleton<ICompatibilityFeedSignatureVerifier, RejectAllCompatibilityFeedSignatureVerifier>();
        services.AddSingleton<ICompatibilityFeedService, CompatibilityFeedService>();
        return services;
    }
}
