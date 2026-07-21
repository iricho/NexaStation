using Microsoft.Extensions.DependencyInjection;
using NexaStation.QuickMenu.Notifications;
using NexaStation.QuickMenu.Safety;
using NexaStation.QuickMenu.Services;
namespace NexaStation.QuickMenu.DependencyInjection;
public static class QuickMenuServiceCollectionExtensions{public static IServiceCollection AddQuickMenu(this IServiceCollection services)=>services.AddSingleton<HoldSafetyCoordinator>().AddSingleton<QuickMenuService>().AddSingleton<IQuickMenuService>(p=>p.GetRequiredService<QuickMenuService>()).AddSingleton<ILocalNotificationService,LocalNotificationService>();}
