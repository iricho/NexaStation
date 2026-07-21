using NexaStation.QuickMenu.Models;
namespace NexaStation.QuickMenu.Services;
public interface IQuickMenuService{bool IsOpen{get;}event EventHandler<QuickMenuStateChangedEventArgs>? StateChanged;Task OpenAsync(QuickMenuOpenRequest request,CancellationToken cancellationToken=default);Task CloseAsync(CancellationToken cancellationToken=default);Task ExecuteActionAsync(string actionId,CancellationToken cancellationToken=default);}
public interface ILocalNotificationService{event EventHandler? Changed;IReadOnlyList<NotificationItem> Items{get;}void Publish(NotificationItem item);void MarkRead(string id);}
public interface ISystemActionHistory{Task RecordAsync(SystemActionHistoryEntry entry,CancellationToken cancellationToken=default);}
