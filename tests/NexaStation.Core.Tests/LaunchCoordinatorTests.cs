using NexaStation.Core;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.Core.Tests;

public sealed class LaunchCoordinatorTests
{
    [Fact]
    public async Task ValidLaunchFollowsExpectedStateOrder()
    {
        TestContext context = new();
        List<LaunchState> states = [];
        context.Coordinator.StateChanged += (_, args) => states.Add(args.Session.State);

        LaunchResult result = await context.Coordinator.LaunchAsync(CreateItem());

        Assert.True(result.Succeeded);
        Assert.Equal(
            [LaunchState.Validating, LaunchState.Starting, LaunchState.Running, LaunchState.Restoring, LaunchState.Completed, LaunchState.Idle],
            states);
    }

    [Fact]
    public async Task FrontendHidesOnlyAfterSuccessfulLaunch()
    {
        TestContext context = new();
        context.Window.OnHide = () => Assert.True(context.Launcher.HasStarted);

        await context.Coordinator.LaunchAsync(CreateItem());

        Assert.Equal(1, context.Window.HideCount);
    }

    [Fact]
    public async Task FrontendRestoresAfterProcessExit()
    {
        TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestContext context = new(exit.Task);
        Task<LaunchResult> launch = context.Coordinator.LaunchAsync(CreateItem());

        await context.Window.Hidden.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, context.Window.FullscreenRestoreCount);
        exit.SetResult(0);
        await launch;

        Assert.Equal(1, context.Window.ForegroundCount);
        Assert.Equal(1, context.Window.FullscreenRestoreCount);
    }

    [Fact]
    public async Task FrontendRestoresAfterLaunchFailure()
    {
        TestContext context = new();
        context.Launcher.Exception = new InvalidOperationException("No process");

        LaunchResult result = await context.Coordinator.LaunchAsync(CreateItem());

        Assert.False(result.Succeeded);
        Assert.Equal(1, context.Window.ForegroundCount);
        Assert.Equal(1, context.Window.FullscreenRestoreCount);
    }

    [Fact]
    public async Task SecondLaunchIsRejectedWhileFirstIsRunning()
    {
        TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestContext context = new(exit.Task);
        Task<LaunchResult> first = context.Coordinator.LaunchAsync(CreateItem());
        await context.Window.Hidden.Task.WaitAsync(TimeSpan.FromSeconds(2));

        LaunchResult second = await context.Coordinator.LaunchAsync(CreateItem("second"));

        Assert.True(second.Rejected);
        exit.SetResult(0);
        Assert.True((await first).Succeeded);
    }

    [Fact]
    public async Task SelectedItemRemainsUnchangedAfterRestoration()
    {
        TestContext context = new();
        LibraryItem item = CreateItem("selected-item");

        await context.Coordinator.LaunchAsync(item);

        Assert.Equal(item.Id, context.Selection.SelectedItemId);
    }

    [Fact]
    public async Task CancellationBeforeLaunchDoesNotHideFrontend()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        TestContext context = new();

        LaunchResult result = await context.Coordinator.LaunchAsync(CreateItem(), cancellation.Token);

        Assert.True(result.Canceled);
        Assert.Equal(0, context.Window.HideCount);
        Assert.False(context.Launcher.HasStarted);
    }

    [Fact]
    public async Task MissingExecutableReturnsReadableFailure()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"nexastation-{Guid.NewGuid():N}.exe");
        TestContext context = new();
        LibraryItem item = CreateItem("missing", new LaunchTarget(LaunchType.Executable, missing));

        LaunchResult result = await context.Coordinator.LaunchAsync(item);

        Assert.False(result.Succeeded);
        Assert.Contains("could not be located", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(context.Launcher.HasStarted);
    }

    [Fact]
    public async Task ProcessExitWaitingDoesNotBlockCaller()
    {
        TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestContext context = new(exit.Task);

        Task<LaunchResult> launch = context.Coordinator.LaunchAsync(CreateItem());
        await context.Window.Hidden.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(launch.IsCompleted);
        exit.SetResult(0);
        Assert.True((await launch).Succeeded);
    }

    [Fact]
    public async Task LaunchStateReturnsToIdleAfterCompletion()
    {
        TestContext context = new();

        await context.Coordinator.LaunchAsync(CreateItem());

        Assert.Equal(LaunchState.Idle, context.Coordinator.CurrentSession?.State);
        Assert.False(context.Coordinator.IsLaunchInProgress);
    }

    [Fact]
    public async Task EmulatorTargetIsResolvedBeforeProcessStart()
    {
        FakeEmulatorResolver resolver=new();TestContext context=new(resolver:resolver);LaunchTarget target=new(LaunchType.Emulator){LibraryItemId="emulated",ProviderId="emulation",EmulatedGameSourceId="game-source"};
        LaunchResult result=await context.Coordinator.LaunchAsync(CreateItem("emulated",target));
        Assert.True(result.Succeeded);Assert.Equal("game-source",resolver.Request?.EmulatedGameSourceId);Assert.Equal(LaunchType.Executable,context.Launcher.StartedTarget?.LaunchType);Assert.Equal(["--game","C:\\Games\\Homebrew.iso"],context.Launcher.StartedTarget?.ArgumentList);
    }

    [Fact]
    public async Task ManualReturnProfileDoesNotWaitForOrHideAStandaloneTarget()
    {
        TaskCompletionSource<int> exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TestContext context = new(exit.Task);
        LaunchTarget target = new(LaunchType.Executable, "notepad.exe") { TrackingMode = ApplicationTrackingMode.ManualReturn, HideFrontendWhileRunning = false };

        LaunchResult result = await context.Coordinator.LaunchAsync(CreateItem("manual", target));

        Assert.True(result.Succeeded);
        Assert.Equal(0, context.Window.HideCount);
        Assert.False(exit.Task.IsCompleted);
        exit.SetResult(0);
    }

    [Fact]
    public async Task WindowPolicyCanRestoreWithoutForcingForeground()
    {
        TestContext context = new();
        LaunchTarget target = new(LaunchType.Executable, "notepad.exe") { DoNotForceFrontendForeground = true };
        LaunchResult result = await context.Coordinator.LaunchAsync(CreateItem("window-policy", target));
        Assert.True(result.Succeeded);
        Assert.Equal(0, context.Window.ForegroundCount);
        Assert.Equal(1, context.Window.FullscreenRestoreCount);
    }

    [Theory]
    [InlineData(ContentLifecycleStatus.Unassigned,ContentLifecycleStatus.Playing)]
    [InlineData(ContentLifecycleStatus.Backlog,ContentLifecycleStatus.Playing)]
    [InlineData(ContentLifecycleStatus.Completed,ContentLifecycleStatus.Completed)]
    [InlineData(ContentLifecycleStatus.Dropped,ContentLifecycleStatus.Dropped)]
    public async Task SuccessfulGameLaunchUsesSafeAutomaticLifecycleTransition(ContentLifecycleStatus initial,ContentLifecycleStatus expected)
    {
        MemoryLifecycle lifecycle=new(initial);TestContext context=new(lifecycle:lifecycle);LaunchResult result=await context.Coordinator.LaunchAsync(CreateItem("game",contentType:ContentType.Game));Assert.True(result.Succeeded);Assert.Equal(expected,lifecycle.Status);
    }

    [Fact]
    public async Task FailedGameLaunchDoesNotChangeLifecycleStatus()
    {
        MemoryLifecycle lifecycle=new(ContentLifecycleStatus.Backlog);TestContext context=new(lifecycle:lifecycle);context.Launcher.Exception=new InvalidOperationException("failed");Assert.False((await context.Coordinator.LaunchAsync(CreateItem("game",contentType:ContentType.Game))).Succeeded);Assert.Equal(ContentLifecycleStatus.Backlog,lifecycle.Status);
    }

    private static LibraryItem CreateItem(string id = "notepad", LaunchTarget? target = null,ContentType contentType=ContentType.Application)
    {
        LaunchTarget launchTarget = (target ?? new LaunchTarget(LaunchType.Executable, "notepad.exe")) with { LibraryItemId = id };
        return new LibraryItem
        {
            Id = id, Title = "Notepad", SortTitle = "Notepad", Subtitle = "Test", Description = "Test item",
            ContentType = contentType, AddedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            DefaultLaunchTargetId = launchTarget.Id, LaunchTargets = [launchTarget],
        };
    }

    private sealed class TestContext
    {
        public TestContext(Task<int>? exitTask = null,IEmulatorLaunchResolver? resolver=null,IContentLifecycleService? lifecycle=null)
        {
            Launcher = new FakeProcessLauncher(exitTask ?? Task.FromResult(0));
            Window = new FakeWindowService();
            Selection = new SelectionStateService();
            Coordinator = new LaunchCoordinator(Launcher, Window, Selection, new FakeLogger(),emulatorResolver:resolver,contentLifecycle:lifecycle);
        }

        public FakeProcessLauncher Launcher { get; }

        public FakeWindowService Window { get; }

        public SelectionStateService Selection { get; }

        public LaunchCoordinator Coordinator { get; }
    }

    private sealed class MemoryLifecycle(ContentLifecycleStatus initial):IContentLifecycleService
    {
        public ContentLifecycleStatus Status{get;private set;}=initial;public Task<ContentLifecycleStatus> GetStatusAsync(ContentLifecycleTarget target,CancellationToken cancellationToken=default)=>Task.FromResult(Status);public Task<IReadOnlyList<ContentLifecycleEntry>> QueryAsync(ContentLifecycleQuery query,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<ContentLifecycleEntry>>([]);public Task SetStatusAsync(ContentLifecycleTarget target,ContentLifecycleStatus status,CancellationToken cancellationToken=default){Status=status;return Task.CompletedTask;}public Task ClearStatusAsync(ContentLifecycleTarget target,CancellationToken cancellationToken=default){Status=ContentLifecycleStatus.Unassigned;return Task.CompletedTask;}public Task<int> ClearStaleTargetsAsync(ContentLifecycleTargetType targetType,IReadOnlyCollection<string> validTargetIds,CancellationToken cancellationToken=default)=>Task.FromResult(0);
    }

    private sealed class FakeProcessLauncher(Task<int> exitTask) : IProcessLauncher
    {
        public bool HasStarted { get; private set; }

        public Exception? Exception { get; set; }
        public LaunchTarget? StartedTarget { get; private set; }

        public Task<RunningProcess> StartAsync(LaunchTarget target, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasStarted = true;
            StartedTarget=target;
            return Exception is not null
                ? Task.FromException<RunningProcess>(Exception)
                : Task.FromResult(new RunningProcess(4242, exitTask));
        }
    }

    private sealed class FakeEmulatorResolver:IEmulatorLaunchResolver
    {
        public EmulatedGameLaunchRequest? Request{get;private set;}
        public Task<ResolvedLaunchTarget> ResolveAsync(EmulatedGameLaunchRequest request,CancellationToken cancellationToken=default){Request=request;return Task.FromResult(new ResolvedLaunchTarget(new LaunchTarget(LaunchType.Executable,"fake-emulator.exe",ExpectedProcessName:"fake-emulator"){ProviderId="emulation",ArgumentList=["--game","C:\\Games\\Homebrew.iso"]}));}
    }

    private sealed class FakeWindowService : IFrontendWindowService
    {
        public TaskCompletionSource Hidden { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int HideCount { get; private set; }

        public int ForegroundCount { get; private set; }

        public int FullscreenRestoreCount { get; private set; }

        public Action? OnHide { get; set; }

        public Task HideAsync()
        {
            OnHide?.Invoke();
            HideCount++;
            Hidden.TrySetResult();
            return Task.CompletedTask;
        }

        public Task RestoreFullscreenAsync()
        {
            FullscreenRestoreCount++;
            return Task.CompletedTask;
        }

        public Task BringToForegroundAsync()
        {
            ForegroundCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : IAppLogger
    {
        public void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void LogError(string eventName, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
