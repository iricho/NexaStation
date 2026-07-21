using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Sessions;
using NexaStation.SystemIntegration.Time;
using NexaStation.SystemIntegration.Windows;
using Xunit;

namespace NexaStation.SystemIntegration.Tests;

public sealed class SystemIntegrationTests
{
    [Theory][InlineData(0)][InlineData(.25)][InlineData(.5)][InlineData(1)]public void VolumeStateRepresentsNormalizedEndpoint(double volume){SystemVolumeState state=new(volume,false,"endpoint","Speakers",true);Assert.InRange(state.Volume,0,1);Assert.True(state.Available);}
    [Fact]public void UnavailableVolumeCarriesReadableReason(){SystemVolumeState state=new(0,false,null,null,false,"Endpoint unavailable");Assert.False(state.Available);Assert.NotEmpty(state.FailureReason!);}
    [Fact]public void MuteStateIsIndependentOfLevel(){SystemVolumeState state=new(.8,true,"id","TV",true);Assert.True(state.Muted);Assert.Equal(.8,state.Volume);}
    [Theory][InlineData(ControllerBatteryState.Unknown)][InlineData(ControllerBatteryState.Wired)][InlineData(ControllerBatteryState.Charging)][InlineData(ControllerBatteryState.Low)][InlineData(ControllerBatteryState.Medium)][InlineData(ControllerBatteryState.Full)]public void EveryControllerBatteryStateIsExplicit(ControllerBatteryState state){ControllerStatus status=new("session","Controller","Gamepad",true,null,state,null,null,true,null);Assert.Equal(state,status.BatteryState);}
    [Theory][InlineData(false,"None")][InlineData(true,"LocalAccess")][InlineData(true,"InternetAccess")]public void NetworkSnapshotSupportsConnectionLevels(bool available,string access){NetworkStatusSnapshot snapshot=new(available,access,null,null,null,null,DateTime.UtcNow);Assert.Equal(available,snapshot.NetworkAvailable);Assert.Equal(access,snapshot.InternetAccessLevel);}
    [Fact]public void NetworkSnapshotDoesNotRequireAnIpAddress(){NetworkStatusSnapshot snapshot=new(true,"InternetAccess","Home","Wi-Fi",false,false,DateTime.UtcNow);Assert.Equal("Home",snapshot.ConnectionProfileName);}
    [Fact]public void LocalTimeUsesCurrentRegionalFormatting(){using LocalTimeService service=new();service.StartUpdates();Assert.NotEmpty(service.Current.DisplayTime);Assert.NotEmpty(service.Current.DisplayDate);service.StopUpdates();}
    [Fact]public void FocusedItemBecomesCurrentActivity(){CurrentActivityService service=new();service.SetFocusedItem("id","Movie","Media");Assert.Equal("Movie",service.Current.Title);Assert.Null(service.Current.Duration);}
    [Fact]public void TrackedActivityIncludesDurationAndConfidence(){CurrentActivityService service=new();service.SetTrackedActivity("id","Game","Steam",DateTime.UtcNow.AddSeconds(-2),"ProcessTracked");Assert.NotNull(service.Current.Duration);Assert.Equal("ProcessTracked",service.Current.TrackingConfidence);}
    [Fact]public void ClearingActivityReturnsHome(){CurrentActivityService service=new();service.SetTrackedActivity("id","Game",null,DateTime.UtcNow,"Tracked");service.ClearTrackedActivity();Assert.Equal("NexaStation Home",service.Current.Title);}
    [Theory][InlineData("minimize")][InlineData("desktop")][InlineData("restore")][InlineData("restart")]public async Task WindowActionsUseOnlyInjectedCallbacks(string action){List<string> calls=[];NexaStationWindowActions service=new(()=>Call("minimize"),()=>Call("desktop"),()=>Call("restore"),()=>Call("restart"));await(action switch{"minimize"=>service.MinimizeAsync(),"desktop"=>service.ReturnToDesktopAsync(),"restore"=>service.RestoreAsync(),_=>service.RestartApplicationAsync()});Assert.Equal([action],calls);Task Call(string value){calls.Add(value);return Task.CompletedTask;}}
    [Theory][InlineData(SystemPowerAction.Sleep)][InlineData(SystemPowerAction.SignOut)][InlineData(SystemPowerAction.Restart)][InlineData(SystemPowerAction.ShutDown)]public void PowerActionsAreExplicitAndNeverShellStrings(SystemPowerAction action)=>Assert.True(Enum.IsDefined(action));
    [Fact]public void AudioSwitchFallbackReportsSettingsBehavior(){AudioOutputChangeResult result=new(false,true,"Sound Settings opened");Assert.True(result.OpenedSettings);Assert.False(result.Succeeded);}
}
