using NexaStation.QuickMenu.Models;
using NexaStation.QuickMenu.Notifications;
using NexaStation.QuickMenu.Safety;
using NexaStation.QuickMenu.Services;
using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;
using Xunit;

namespace NexaStation.QuickMenu.Tests;

public sealed class QuickMenuTests
{
    [Fact]public async Task OpenFromDashboardItem(){using Fixture f=new();await f.Menu.OpenAsync(new("Games","item","card"));Assert.True(f.Menu.IsOpen);}
    [Fact]public async Task PreserveOriginState(){using Fixture f=new();await f.Menu.OpenAsync(new("Media","m","control"));Assert.Equal(("Media","m","control"),(f.Menu.State.OriginPageId,f.Menu.State.OriginItemId,f.Menu.State.OriginControlKey));}
    [Fact]public async Task CloseRestoresExactFocus(){using Fixture f=new();int restored=0;await f.Menu.OpenAsync(new("Home",null,null,()=>{restored++;return Task.CompletedTask;}));await f.Menu.CloseAsync();Assert.Equal(1,restored);}
    [Fact]public async Task DuplicateOpenIsIgnored(){using Fixture f=new();await f.Menu.OpenAsync(new("Home",null,null));await f.Menu.OpenAsync(new("Games",null,null));Assert.Equal("Home",f.Menu.State.OriginPageId);}
    [Fact]public async Task DisabledActionIsRejected(){using Fixture f=new();f.Menu.RegisterAction(new("x","X","","",QuickMenuActionType.Normal,Enabled:false,DisabledReason:"disabled"),_=>Task.CompletedTask);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Menu.ExecuteActionAsync("x"));}
    [Fact]public async Task ConcurrentSystemActionIsRejected(){using Fixture f=new();TaskCompletionSource entered=new();TaskCompletionSource release=new();f.Menu.RegisterAction(new("x","X","","",QuickMenuActionType.System),async _=>{entered.SetResult();await release.Task;});Task first=f.Menu.ExecuteActionAsync("x");await entered.Task;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Menu.ExecuteActionAsync("x"));release.SetResult();await first;}
    [Fact]public async Task ClosingCancelsRefreshAndHold(){using Fixture f=new();await f.Menu.OpenAsync(new("Home",null,null));Task<bool> hold=f.Holds.HoldAsync(10000);await f.Menu.CloseAsync();Assert.False(await hold);}
    [Fact]public async Task ConfirmationRequiresSecondExecution(){using Fixture f=new();int calls=0;f.Menu.RegisterAction(new("x","X","","",QuickMenuActionType.Destructive,true),_=>{calls++;return Task.CompletedTask;});await f.Menu.ExecuteActionAsync("x");Assert.Equal(0,calls);await f.Menu.ExecuteActionAsync("x");Assert.Equal(1,calls);}
    [Theory][InlineData("release")][InlineData("focus")][InlineData("controller")][InlineData("foreground")]
    public async Task HoldsCancelForEverySafetyTrigger(string trigger){using HoldSafetyCoordinator hold=new();Task<bool> pending=hold.HoldAsync(10000);switch(trigger){case"release":hold.OnButtonReleased();break;case"focus":hold.OnFocusChanged();break;case"controller":hold.OnControllerDisconnected();break;default:hold.OnLostForeground();break;}Assert.False(await pending);}
    [Theory][InlineData(-1,0)][InlineData(0.5,0.5)][InlineData(2,1)]public void VolumeValuesClamp(double input,double expected)=>Assert.Equal(expected,Math.Clamp(input,0,1));
    [Theory][InlineData(NotificationType.Information)][InlineData(NotificationType.Success)][InlineData(NotificationType.Warning)][InlineData(NotificationType.Error)][InlineData(NotificationType.ActionRequired)]public void LocalNotificationsRetainEverySupportedType(NotificationType type){LocalNotificationService service=new();service.Publish(new("x",type,"Title","Message",DateTime.UtcNow));Assert.Equal(type,service.Items[0].Type);}
    [Fact]public void NotificationsHaveBoundedHistory(){LocalNotificationService service=new();for(int i=0;i<75;i++)service.Publish(new(i.ToString(System.Globalization.CultureInfo.InvariantCulture),NotificationType.Information,"T","M",DateTime.UtcNow));Assert.Equal(50,service.Items.Count);}
    [Fact]public void MarkReadUpdatesLocalNotification(){LocalNotificationService service=new();service.Publish(new("x",NotificationType.Information,"T","M",DateTime.UtcNow));service.MarkRead("x");Assert.True(service.Items[0].Read);}

    private sealed class Fixture:IDisposable
    {
        public Fixture(){Holds=new();Menu=new(new Volume(),new Controllers(),new Network(),new Activity(),Holds);}
        public HoldSafetyCoordinator Holds{get;}public QuickMenuService Menu{get;}public void Dispose(){Menu.Dispose();Holds.Dispose();}
    }
    private sealed class Volume:ISystemVolumeService{public event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;public Task<SystemVolumeState> GetStateAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new SystemVolumeState(.5,false,"id","device",true));public Task SetVolumeAsync(double normalizedVolume,CancellationToken cancellationToken=default)=>Task.CompletedTask;public Task SetMutedAsync(bool muted,CancellationToken cancellationToken=default)=>Task.CompletedTask;}
    private sealed class Controllers:IControllerStatusService{public event EventHandler<ControllerStatusChangedEventArgs>? StatusChanged;public Task<IReadOnlyList<ControllerStatus>> GetControllersAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<ControllerStatus>>([]);}
    private sealed class Network:INetworkStatusService{public event EventHandler<NetworkStatusChangedEventArgs>? StatusChanged;public Task<NetworkStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new NetworkStatusSnapshot(true,"InternetAccess",null,"Ethernet",false,false,DateTime.UtcNow));}
    private sealed class Activity:ICurrentActivityService{public CurrentActivitySnapshot Current{get;}=new("Home",null,null,"Direct");public void SetFocusedItem(string? itemId,string? title,string? provider){}public void SetTrackedActivity(string itemId,string title,string? provider,DateTime startedAtUtc,string confidence){}public void ClearTrackedActivity(){}}
}
