using NexaStation.Core;
using NexaStation.DesktopControl.Mouse;
using NexaStation.DesktopControl.Profiles;
using NexaStation.DesktopControl.Safety;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.DesktopControl.Tests;

public sealed class CursorAndSessionTests
{
    [Theory]
    [InlineData(0,0)]
    [InlineData(0.05,0.05)]
    [InlineData(-0.1,0)]
    public void DeadzoneProducesNoMotion(double x,double y){DesktopCursorMotion result=CursorMotionCalculator.Calculate(x,y,0.016,Profile(),false,false);Assert.Equal(0,result.DeltaX);Assert.Equal(0,result.DeltaY);}
    [Fact] public void HorizontalStickProducesHorizontalMotion(){DesktopCursorMotion result=CursorMotionCalculator.Calculate(1,0,0.016,Profile(),false,false);Assert.True(result.DeltaX>0);Assert.Equal(0,result.DeltaY);}
    [Fact] public void VerticalAxisIsInvertedForScreenCoordinates(){DesktopCursorMotion result=CursorMotionCalculator.Calculate(0,1,0.016,Profile(),false,false);Assert.True(result.DeltaY<0);}
    [Fact] public void SlowModeReducesMotion(){double normal=CursorMotionCalculator.Calculate(1,0,0.016,Profile(),false,false).DeltaX;double slow=CursorMotionCalculator.Calculate(1,0,0.016,Profile(),true,false).DeltaX;Assert.True(slow<normal);}
    [Fact] public void FastModeIncreasesMotion(){double normal=CursorMotionCalculator.Calculate(1,0,0.016,Profile(),false,false).DeltaX;double fast=CursorMotionCalculator.Calculate(1,0,0.016,Profile(),false,true).DeltaX;Assert.True(fast>normal);}
    [Fact] public void MotionIsClampedPerTick(){DesktopCursorMotion result=CursorMotionCalculator.Calculate(1,0,0.25,Profile() with{CursorSensitivity=100000},false,true);Assert.InRange(result.DeltaX,0,120);}
    [Fact] public void InvalidElapsedTimeCannotExplodeMotion(){DesktopCursorMotion result=CursorMotionCalculator.Calculate(1,0,10,Profile(),false,false);Assert.InRange(Math.Abs(result.DeltaX),0,120);}

    [Fact] public async Task GamesAreAlwaysRejected(){await Assert.ThrowsAsync<InvalidOperationException>(()=>Context().Session.StartAsync(new("game",1,true,false)));}
    [Fact] public async Task EmulatorsAreAlwaysRejected(){await Assert.ThrowsAsync<InvalidOperationException>(()=>Context().Session.StartAsync(new("game",1,false,true)));}
    [Fact] public async Task KnownElevatedTargetIsRejected(){await Assert.ThrowsAsync<InvalidOperationException>(()=>Context().Session.StartAsync(new("app",1,false,false,true)));}
    [Fact] public async Task DisabledAssignmentDoesNotStart(){TestContext context=Context(enabled:false);await context.Session.StartAsync(new("app",null,false,false));Assert.False(context.Session.IsActive);}
    [Fact] public async Task DisabledProfileDoesNotStart(){TestContext context=Context(profile:Profile()with{Enabled=false});await context.Session.StartAsync(new("app",null,false,false));Assert.False(context.Session.IsActive);}
    [Fact] public async Task SafetyRefusalPreventsSession(){TestContext context=Context(allowed:false);await Assert.ThrowsAsync<InvalidOperationException>(()=>context.Session.StartAsync(new("app",42,false,false)));Assert.False(context.Session.IsActive);}

    [Fact]
    public async Task StopReleasesInjectedButtons()
    {
        TestContext context=Context();await context.Session.StartAsync(new("app",null,false,false));Assert.True(context.Session.IsActive);await context.Session.StopAsync(DesktopControlStopReason.UserDisabled);Assert.False(context.Session.IsActive);Assert.True(context.Injector.ReleaseCount>0);
    }

    [Fact]
    public async Task EmergencyBothStickChordStopsImmediately()
    {
        TestContext context=Context(reading:new(true,0,0,0,0,0,0,DesktopControllerButtons.LeftThumb|DesktopControllerButtons.RightThumb,DateTime.UtcNow));TaskCompletionSource<DesktopControlStopReason> stopped=new(TaskCreationOptions.RunContinuationsAsynchronously);context.Session.Stopped+=(_,reason)=>stopped.TrySetResult(reason);await context.Session.StartAsync(new("app",null,false,false));Assert.Equal(DesktopControlStopReason.EmergencyDisabled,await stopped.Task.WaitAsync(TimeSpan.FromSeconds(2)));Assert.True(context.Injector.ReleaseCount>0);
    }

    [Fact]
    public async Task AButtonInjectsSingleLeftClickOnEdge()
    {
        TestContext context=Context(reading:new(true,0,0,0,0,0,0,DesktopControllerButtons.A,DateTime.UtcNow));await context.Session.StartAsync(new("app",null,false,false));await WaitUntilAsync(()=>context.Injector.LeftClicks>0);await Task.Delay(40);await context.Session.StopAsync(DesktopControlStopReason.UserDisabled);Assert.Equal(1,context.Injector.LeftClicks);
    }

    [Fact]
    public async Task StickMotionUsesInjectorAndRecordsControllerActivity()
    {
        TestContext context=Context(reading:new(true,1,0,0,0,0,0,DesktopControllerButtons.None,DateTime.UtcNow));await context.Session.StartAsync(new("app",null,false,false));await WaitUntilAsync(()=>context.Injector.MoveCount>0);await context.Session.StopAsync(DesktopControlStopReason.UserDisabled);Assert.True(context.Mouse.RecordCount>0);
    }

    [Fact]
    public async Task PhysicalMousePauseSuppressesCursorMotion()
    {
        TestContext context=Context(reading:new(true,1,0,0,0,0,0,DesktopControllerButtons.None,DateTime.UtcNow),pause:true);await context.Session.StartAsync(new("app",null,false,false));await Task.Delay(80);await context.Session.StopAsync(DesktopControlStopReason.UserDisabled);Assert.Equal(0,context.Injector.MoveCount);
    }

    [Fact] public void NullProcessIntegrityCheckIsAllowed()=>Assert.True(new WindowsIntegrityGuard().CanControl(null).Allowed);

    private static async Task WaitUntilAsync(Func<bool> predicate){DateTime deadline=DateTime.UtcNow.AddSeconds(2);while(!predicate()&&DateTime.UtcNow<deadline)await Task.Delay(10);Assert.True(predicate());}
    private static DesktopControlProfile Profile()=>new(){Id="basic",Name="Basic",Enabled=true,CursorSensitivity=900,CursorAcceleration=1.7,CursorDeadzone=0.18,ScrollSensitivity=480,CreatedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow};
    private static TestContext Context(bool enabled=true,DesktopControlProfile? profile=null,bool allowed=true,DesktopControllerReading? reading=null,bool pause=false)=>new(enabled,profile??Profile(),allowed,reading??new(true,0,0,0,0,0,0,DesktopControllerButtons.None,DateTime.UtcNow),pause);

    private sealed class TestContext
    {
        public TestContext(bool enabled,DesktopControlProfile profile,bool allowed,DesktopControllerReading reading,bool pause){Injector=new();Mouse=new(pause);Session=new DesktopControlSession(new FakeProfiles(enabled,profile),new FixedController(reading),Injector,new FakeSafety(allowed),Mouse);}
        public DesktopControlSession Session{get;}public FakeInjector Injector{get;}public FakeMouse Mouse{get;}
    }
    private sealed class FakeProfiles(bool enabled,DesktopControlProfile profile):IDesktopControlProfileProvider{public Task<DesktopControlProfileResolution>ResolveForItemAsync(string libraryItemId,CancellationToken cancellationToken=default)=>Task.FromResult(new DesktopControlProfileResolution(profile,enabled));}
    private sealed class FixedController(DesktopControllerReading reading):IDesktopControllerStateSource{public DesktopControllerReading Read()=>reading with{TimestampUtc=DateTime.UtcNow};}
    private sealed class FakeSafety(bool allowed):IDesktopControlSafetyGuard{public DesktopControlSafetyResult CanControl(int? processId)=>new(allowed,allowed?"ok":"no");}
    private sealed class FakeMouse(bool pause):IPhysicalMouseActivityMonitor{public int RecordCount{get;private set;}public bool ShouldPause(DateTime nowUtc)=>pause;public void RecordControllerMovement(DateTime atUtc)=>RecordCount++;}
    private sealed class FakeInjector:IDesktopInputInjector
    {
        public int MoveCount{get;private set;}public int LeftClicks{get;private set;}public int ReleaseCount{get;private set;}
        public DesktopInputInjectionResult MoveCursor(double deltaX,double deltaY){MoveCount++;return new(true,"ok");}public DesktopInputInjectionResult Scroll(int verticalDelta,int horizontalDelta)=>new(true,"ok");public DesktopInputInjectionResult Click(DesktopMouseButton button){if(button==DesktopMouseButton.Left)LeftClicks++;return new(true,"ok");}public DesktopInputInjectionResult SendApprovedKey(DesktopApprovedKey key)=>new(true,"ok");public void ReleaseAll()=>ReleaseCount++;
    }
}
