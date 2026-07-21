using NexaStation.QuickMenu.Models;
using NexaStation.QuickMenu.Services;

namespace NexaStation.QuickMenu.Notifications;
public sealed class LocalNotificationService:ILocalNotificationService
{
    private const int MaximumItems=50;private readonly List<NotificationItem> _items=[];public event EventHandler? Changed;public IReadOnlyList<NotificationItem> Items=>_items;
    public void Publish(NotificationItem item){_items.RemoveAll(value=>value.ExpiresAtUtc<=DateTime.UtcNow);_items.Insert(0,item);if(_items.Count>MaximumItems)_items.RemoveRange(MaximumItems,_items.Count-MaximumItems);Changed?.Invoke(this,EventArgs.Empty);}
    public void MarkRead(string id){int index=_items.FindIndex(x=>x.Id==id);if(index>=0){_items[index]=_items[index] with{Read=true};Changed?.Invoke(this,EventArgs.Empty);}}
}
