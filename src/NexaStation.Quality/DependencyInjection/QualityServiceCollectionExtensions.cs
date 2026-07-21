using Microsoft.Extensions.DependencyInjection;
using NexaStation.Quality.Accessibility;
using NexaStation.Quality.CrashFingerprinting;
using NexaStation.Quality.Navigation;

namespace NexaStation.Quality.DependencyInjection;

public static class QualityServiceCollectionExtensions
{
    public static IServiceCollection AddQualityServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ICrashFingerprintService, CrashFingerprintService>();
        services.AddSingleton<ICrashFingerprintTracker, CrashFingerprintTracker>();
        services.AddSingleton<INavigationAuditService, NavigationAuditService>();
        services.AddSingleton<IAccessibilityAuditService, AccessibilityAuditService>();
        return services;
    }
}
