using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.Core.Tests;

public sealed class WindowsApplicationLaunchTests
{
    [Fact]
    public async Task UntrackedPackagedLaunchRecordsZeroDurationApplicationEvent()
    {
        FakeActivation activation=new(new(true,null,"accepted"));FakeSessions sessions=new();LaunchCoordinator coordinator=Coordinator(activation,sessions:sessions);LaunchResult result=await coordinator.LaunchAsync(Item(ApplicationTrackingMode.None));Assert.True(result.Succeeded);Assert.Equal(WindowsLaunchMethod.AppUserModelId,activation.Request?.LaunchMethod);Assert.Equal(ActivitySessionKind.Application,sessions.Started?.SessionKind);Assert.Equal(ApplicationTrackingMode.None,sessions.Started?.TrackingMode);Assert.Equal(0,sessions.CompletedDuration);
    }

    [Fact]
    public async Task RejectedPackagedActivationReturnsReadableFailure()
    {
        LaunchResult result=await Coordinator(new FakeActivation(new(false,null,"activation rejected"))).LaunchAsync(Item(ApplicationTrackingMode.None));Assert.False(result.Succeeded);Assert.Contains("activation rejected",result.ErrorMessage,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrackedWindowsApplicationStartsAndStopsDesktopControl()
    {
        using Process process=Process.Start(new ProcessStartInfo("cmd.exe","/d /c ping 127.0.0.1 -n 2 >nul"){UseShellExecute=false,CreateNoWindow=true})!;FakeDesktopControl desktop=new();LaunchCoordinator coordinator=Coordinator(new FakeActivation(new(true,process.Id,"started")),desktop:desktop);LaunchResult result=await coordinator.LaunchAsync(Item(ApplicationTrackingMode.ActivatedProcessHint,desktop:true));Assert.True(result.Succeeded);Assert.Equal(1,desktop.StartCount);Assert.Equal(1,desktop.StopCount);Assert.Equal(process.Id,desktop.LastRequest?.TargetProcessId);
    }

    private static LaunchCoordinator Coordinator(IWindowsApplicationActivationService activation,FakeSessions? sessions=null,FakeDesktopControl? desktop=null)=>new(new NeverProcessLauncher(),new Window(),new SelectionStateService(),new Logger(),sessions,windowsActivation:activation,desktopControl:desktop);
    private static LibraryItem Item(ApplicationTrackingMode tracking,bool desktop=false){LaunchTarget target=new(LaunchType.PackagedApplication){LibraryItemId="calculator",ProviderId="windows-apps-folder",WindowsLaunchMethod=WindowsLaunchMethod.AppUserModelId,AppUserModelId="Microsoft.WindowsCalculator_abc!App",TrackingMode=tracking,HideFrontendWhileRunning=false,RestoreFrontendAfterExit=false};return new(){Id="calculator",Title="Calculator",SortTitle="Calculator",ContentType=ContentType.Application,AddedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow,DefaultLaunchTargetId=target.Id,DesktopControlEnabled=desktop,LaunchTargets=[target]};}
    private sealed class FakeActivation(WindowsActivationResult result):IWindowsApplicationActivationService{public WindowsApplicationLaunchRequest?Request{get;private set;}public Task<WindowsActivationResult>ActivateAsync(WindowsApplicationLaunchRequest request,CancellationToken cancellationToken=default){Request=request;return Task.FromResult(result);}}
    private sealed class NeverProcessLauncher:IProcessLauncher{public Task<RunningProcess>StartAsync(LaunchTarget target,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Classic launcher must not be used.");}
    private sealed class Window:IFrontendWindowService{public Task HideAsync()=>Task.CompletedTask;public Task RestoreFullscreenAsync()=>Task.CompletedTask;public Task BringToForegroundAsync()=>Task.CompletedTask;}
    private sealed class Logger:IAppLogger{public void Information(string eventName,string message,IReadOnlyDictionary<string,object?>?properties=null){}public void LogError(string eventName,string message,Exception?exception=null,IReadOnlyDictionary<string,object?>?properties=null){}}
    private sealed class FakeSessions:IPlaySessionRepository{public PlaySession?Started{get;private set;}public long?CompletedDuration{get;private set;}public Task StartSessionAsync(PlaySession session,CancellationToken cancellationToken=default){Started=session;return Task.CompletedTask;}public Task CompleteSessionAsync(string sessionId,DateTime endedAtUtc,long durationSeconds,bool exitDetectedNormally,CancellationToken cancellationToken=default){CompletedDuration=durationSeconds;return Task.CompletedTask;}public Task FailSessionAsync(string sessionId,string failureMessage,CancellationToken cancellationToken=default)=>Task.CompletedTask;public Task<IReadOnlyList<PlaySession>>GetRecentAsync(int maximumResults,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<PlaySession>>([]);public Task RepairIncompleteSessionsAsync(DateTime applicationStartedAtUtc,CancellationToken cancellationToken=default)=>Task.CompletedTask;}
    private sealed class FakeDesktopControl:IDesktopControlSession{public bool IsActive{get;private set;}public int StartCount{get;private set;}public int StopCount{get;private set;}public DesktopControlSessionRequest?LastRequest{get;private set;}public event EventHandler<DesktopControlStopReason>?Stopped;public Task StartAsync(DesktopControlSessionRequest request,CancellationToken cancellationToken=default){StartCount++;LastRequest=request;IsActive=true;return Task.CompletedTask;}public Task StopAsync(DesktopControlStopReason reason,CancellationToken cancellationToken=default){StopCount++;IsActive=false;Stopped?.Invoke(this,reason);return Task.CompletedTask;}}
}
