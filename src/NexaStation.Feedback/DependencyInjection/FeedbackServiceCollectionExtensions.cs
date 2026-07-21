using Microsoft.Extensions.DependencyInjection;
using NexaStation.Feedback.IssuePreparation;
using NexaStation.Feedback.ReproductionBundles;

namespace NexaStation.Feedback.DependencyInjection;

public static class FeedbackServiceCollectionExtensions
{
    public static IServiceCollection AddFeedbackServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IReproductionBundleService, ReproductionBundleService>();
        services.AddSingleton<IGitHubIssuePreparationService, GitHubIssuePreparationService>();
        return services;
    }
}
