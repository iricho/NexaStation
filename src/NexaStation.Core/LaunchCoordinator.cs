using System.Diagnostics;
using NexaStation.Compatibility.Matching;
using NexaStation.Domain;

namespace NexaStation.Core;

public sealed class LaunchCoordinator : ILaunchCoordinator
{
    private static readonly string[] DefaultIgnoredLaunchProcesses = ["steam", "steamwebhelper", "gameoverlayui", "crashhandler"];
    private readonly IProcessLauncher _processLauncher;
    private readonly IFrontendWindowService _windowService;
    private readonly ISelectionStateService _selectionState;
    private readonly IAppLogger _logger;
    private readonly IPlaySessionRepository? _playSessions;
    private readonly ILaunchedApplicationDetector? _applicationDetector;
    private readonly IEmulatorLaunchResolver? _emulatorResolver;
    private readonly IWindowsApplicationActivationService? _windowsActivation;
    private readonly IForegroundApplicationTracker? _foregroundTracker;
    private readonly IDesktopControlSession? _desktopControl;
    private readonly ICompatibilityProfileResolver? _compatibilityProfiles;
    private readonly IActivityTimelineService? _activityTimeline;
    private readonly IActivityPrivacyService? _activityPrivacy;
    private readonly IPresenceCoordinator? _presence;
    private int _launchInProgress;

    public LaunchCoordinator(
        IProcessLauncher processLauncher,
        IFrontendWindowService windowService,
        ISelectionStateService selectionState,
        IAppLogger logger,
        IPlaySessionRepository? playSessions = null,
        ILaunchedApplicationDetector? applicationDetector = null,
        IEmulatorLaunchResolver? emulatorResolver = null,
        IWindowsApplicationActivationService? windowsActivation = null,
        IForegroundApplicationTracker? foregroundTracker = null,
        IDesktopControlSession? desktopControl = null,
        ICompatibilityProfileResolver? compatibilityProfiles = null,
        IActivityTimelineService? activityTimeline = null,
        IActivityPrivacyService? activityPrivacy = null,
        IPresenceCoordinator? presence = null)
    {
        _processLauncher = processLauncher;
        _windowService = windowService;
        _selectionState = selectionState;
        _logger = logger;
        _playSessions = playSessions;
        _applicationDetector = applicationDetector;
        _emulatorResolver = emulatorResolver;
        _windowsActivation = windowsActivation;
        _foregroundTracker = foregroundTracker;
        _desktopControl = desktopControl;
        _compatibilityProfiles = compatibilityProfiles;
        _activityTimeline = activityTimeline;
        _activityPrivacy = activityPrivacy;
        _presence = presence;
    }

    public event EventHandler<LaunchStateChangedEventArgs>? StateChanged;

    public bool IsLaunchInProgress => Volatile.Read(ref _launchInProgress) == 1;

    public LaunchSession? CurrentSession { get; private set; }

    public async Task<LaunchResult> LaunchAsync(LibraryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Interlocked.CompareExchange(ref _launchInProgress, 1, 0) != 0)
        {
            _logger.Information("LaunchRejected", "A duplicate launch request was rejected.", Properties(item));
            return LaunchResult.Duplicate();
        }

        RunningProcess? runningProcess = null;
        IReadOnlyList<RunningProcess> trackedProcesses = [];
        PlaySession? playSession = null;
        Stopwatch? elapsed = null;
        LaunchTarget? launchTarget = null;
        CompatibilityResolutionResult? compatibility = null;
        LaunchResult result;

        try
        {
            _selectionState.SelectedItemId = item.Id;
            CurrentSession = LaunchSession.Start(item.Id);
            _logger.Information("LaunchRequested", "A library item launch was requested.", Properties(item));

            TransitionTo(LaunchState.Validating);
            cancellationToken.ThrowIfCancellationRequested();

            launchTarget = item.LaunchTarget;
            if (launchTarget?.LaunchType is LaunchType.Emulator)
            {
                if (_emulatorResolver is null || string.IsNullOrWhiteSpace(launchTarget.EmulatedGameSourceId))
                    return await FailAndRestoreAsync("This emulated game does not have a resolvable emulator target.", null).ConfigureAwait(false);
                ResolvedLaunchTarget resolved = await _emulatorResolver.ResolveAsync(
                    new(launchTarget.EmulatedGameSourceId, launchTarget.EmulatorInstallationId, launchTarget.LaunchProfileId),
                    cancellationToken).ConfigureAwait(false);
                launchTarget = resolved.LaunchTarget;
            }

            if (launchTarget is not null && _compatibilityProfiles is not null)
            {
                compatibility = await _compatibilityProfiles.ResolveAsync(CompatibilityLaunchPolicy.CreateTarget(item, launchTarget), cancellationToken).ConfigureAwait(false);
                launchTarget = CompatibilityLaunchPolicy.Apply(launchTarget, compatibility.Rules);
                if (compatibility.AppliedProfiles.Count > 0)
                {
                    _logger.Information("CompatibilityProfilesApplied", "Validated declarative compatibility profiles were applied to the launch.", new Dictionary<string, object?>
                    {
                        ["LibraryItemId"] = item.Id,
                        ["ProfileIds"] = string.Join(',', compatibility.AppliedProfiles.Select(profile => profile.Id)),
                        ["ConflictCount"] = compatibility.Conflicts.Count,
                    });
                }
            }

            string? validationError = LaunchTargetValidator.Validate(launchTarget);
            if (validationError is not null)
            {
                result = await FailAndRestoreAsync(validationError, null).ConfigureAwait(false);
                return result;
            }

            TransitionTo(LaunchState.Starting);
            DateTime launchStartedAtUtc = DateTime.UtcNow;
            bool windowsApplication = IsWindowsApplicationTarget(launchTarget!);
            if (windowsApplication)
            {
                if (_windowsActivation is null)
                    return await FailAndRestoreAsync("Windows application activation is not available.", null).ConfigureAwait(false);
                WindowsActivationResult activation = await _windowsActivation.ActivateAsync(ToWindowsRequest(launchTarget!), cancellationToken).ConfigureAwait(false);
                if (!activation.Succeeded)
                    return await FailAndRestoreAsync(activation.Message, null).ConfigureAwait(false);

                if (launchTarget!.TrackingMode is ApplicationTrackingMode.None or ApplicationTrackingMode.ManualReturn)
                {
                    DateTime now = DateTime.UtcNow;
                    playSession = new PlaySession(Guid.NewGuid().ToString("N"), item.Id, now, ProcessId: activation.ProcessIdHint, ProviderId: launchTarget.ProviderId, SessionKind: ActivitySessionKind.Application, TrackingMode: launchTarget.TrackingMode);
                    if (_playSessions is not null)
                    {
                        await _playSessions.StartSessionAsync(playSession, cancellationToken).ConfigureAwait(false);
                        await _playSessions.CompleteSessionAsync(playSession.Id, now, 0, true, cancellationToken).ConfigureAwait(false);
                    }
                    await RecordActivityStartedAsync(item,playSession,cancellationToken).ConfigureAwait(false);
                    await RecordActivityEndedAsync(item,playSession,now,0,ActivityResult.Succeeded,cancellationToken).ConfigureAwait(false);
                    TransitionTo(LaunchState.Running);
                    TransitionTo(LaunchState.Completed, ended: true);
                    return LaunchResult.Success(CurrentSession!);
                }

                int? processId = activation.ProcessIdHint;
                ForegroundApplicationDetectionResult? foreground = null;
                if (processId is null && _foregroundTracker is not null)
                {
                    foreground = await _foregroundTracker.DetectAsync(new(
                        launchStartedAtUtc,
                        null,
                        launchTarget.ExpectedExecutablePath,
                        launchTarget.AppUserModelId,
                        launchTarget.ExpectedWindowClass,
                        TimeSpan.FromSeconds(Math.Clamp(launchTarget.DetectionTimeoutSeconds, 1, 120)),
                        TimeSpan.FromMilliseconds(500)), cancellationToken).ConfigureAwait(false);
                    processId = foreground.Detected ? foreground.ProcessId : null;
                }
                if (processId is null)
                    return await FailAndRestoreAsync("Windows accepted the launch, but NexaStation could not safely identify a process to track. Usage time was not recorded.", null).ConfigureAwait(false);
                runningProcess = AttachToProcess(processId.Value);
            }
            else
            {
                runningProcess = await _processLauncher
                    .StartAsync(launchTarget!, cancellationToken)
                    .ConfigureAwait(false);
            }

            // A validated profile may deliberately opt out of process tracking for any
            // target type, including Steam and emulators. The launch is still recorded,
            // but NexaStation does not guess at process ownership or hide the frontend.
            if (launchTarget!.TrackingMode is ApplicationTrackingMode.None or ApplicationTrackingMode.ManualReturn)
            {
                DateTime now = DateTime.UtcNow;
                playSession = new PlaySession(Guid.NewGuid().ToString("N"), item.Id, now, ProcessId: runningProcess?.ProcessId, ProviderId: launchTarget.ProviderId, ProviderItemId: SteamItemId(launchTarget), SessionKind: item.ContentType == ContentType.Application ? ActivitySessionKind.Application : ActivitySessionKind.Game, TrackingMode: launchTarget.TrackingMode);
                if (_playSessions is not null)
                {
                    await _playSessions.StartSessionAsync(playSession, cancellationToken).ConfigureAwait(false);
                    await _playSessions.CompleteSessionAsync(playSession.Id, now, 0, true, cancellationToken).ConfigureAwait(false);
                }
                await RecordActivityStartedAsync(item,playSession,cancellationToken).ConfigureAwait(false);
                await RecordActivityEndedAsync(item,playSession,now,0,ActivityResult.Succeeded,cancellationToken).ConfigureAwait(false);
                TransitionTo(LaunchState.Running);
                TransitionTo(LaunchState.Completed, ended: true);
                return LaunchResult.Success(CurrentSession!);
            }

            LaunchedApplicationDetectionResult? detection = null;
            if (_applicationDetector?.CanDetect(launchTarget!) == true)
            {
                IReadOnlyCollection<string> configuredIgnoredProcesses = compatibility?.Rules.Tracking.IgnoredProcessNames ?? [];
                detection = await _applicationDetector.DetectAsync(
                    new LaunchedApplicationDetectionRequest(
                        launchTarget!,
                        DateTime.UtcNow,
                        launchTarget!.WorkingDirectory,
                        string.IsNullOrWhiteSpace(launchTarget.ExpectedProcessName) ? [] : [launchTarget.ExpectedProcessName],
                        DefaultIgnoredLaunchProcesses.Concat(configuredIgnoredProcesses).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                        TimeSpan.FromSeconds(Math.Clamp(compatibility?.Rules.Tracking.DetectionTimeoutSeconds ?? launchTarget.DetectionTimeoutSeconds, 1, 600)),
                        TimeSpan.FromMilliseconds(750),
                        TimeSpan.FromSeconds(5)),
                    cancellationToken).ConfigureAwait(false);
                if (!detection.Detected || detection.Processes.Count == 0)
                {
                    return await FailAndRestoreAsync(
                        "The launch request was sent, but no game process was detected. Keep waiting, choose a running process, or edit Steam process settings.",
                        null).ConfigureAwait(false);
                }

                trackedProcesses = detection.Processes;
            }
            else
            {
                trackedProcesses = [runningProcess];
            }

            int primaryProcessId = trackedProcesses[0].ProcessId;
            CurrentSession = CurrentSession! with { ProcessId = primaryProcessId };
            DateTime playStartedAtUtc = DateTime.UtcNow;
            playSession = new PlaySession(
                Guid.NewGuid().ToString("N"),
                item.Id,
                playStartedAtUtc,
                ProcessId: primaryProcessId,
                ProviderId: launchTarget!.ProviderId,
                ProviderItemId: SteamItemId(launchTarget),
                ProcessNames: detection is null ? launchTarget.ExpectedProcessName : string.Join(';', detection.ProcessNames),
                DetectionConfidence: detection?.Confidence,
                SessionKind: item.ContentType == ContentType.Application ? ActivitySessionKind.Application : ActivitySessionKind.Game,
                TrackingMode: launchTarget.TrackingMode,
                ForegroundProcessId: windowsApplication ? primaryProcessId : null);
            if (_playSessions is not null)
            {
                await _playSessions.StartSessionAsync(playSession, cancellationToken).ConfigureAwait(false);
            }
            await RecordActivityStartedAsync(item,playSession,cancellationToken).ConfigureAwait(false);
            elapsed = Stopwatch.StartNew();
            TransitionTo(LaunchState.Running);

            if (item.DesktopControlEnabled && _desktopControl is not null)
            {
                await _desktopControl.StartAsync(new(item.Id, primaryProcessId, item.ContentType == ContentType.Game, string.Equals(launchTarget.ProviderId, "emulation", StringComparison.OrdinalIgnoreCase)), cancellationToken).ConfigureAwait(false);
            }

            if (launchTarget!.HideFrontendWhileRunning)
            {
                await _windowService.HideAsync().ConfigureAwait(false);
            }

            int[] exitCodes = await Task.WhenAll(trackedProcesses.Select(process => process.WaitForExitAsync())).ConfigureAwait(false);
            elapsed.Stop();
            if (_playSessions is not null && playSession is not null)
            {
                await _playSessions.CompleteSessionAsync(playSession.Id, DateTime.UtcNow, Math.Max(0, (long)elapsed.Elapsed.TotalSeconds), true, cancellationToken).ConfigureAwait(false);
            }
            if(playSession is not null)await RecordActivityEndedAsync(item,playSession,DateTime.UtcNow,Math.Max(0,(long)elapsed.Elapsed.TotalSeconds),ActivityResult.Succeeded,cancellationToken).ConfigureAwait(false);
            _logger.Information(
                "ProcessExited",
                "The launched process exited.",
                new Dictionary<string, object?> { ["ProcessId"] = primaryProcessId, ["ExitCodes"] = string.Join(',', exitCodes) });

            TransitionTo(LaunchState.Restoring);
            if (launchTarget.RestoreFrontendAfterExit && !launchTarget.RestoreFrontendManually)
            {
                await RestoreFrontendAsync(launchTarget).ConfigureAwait(false);
            }

            TransitionTo(LaunchState.Completed, ended: true);
            result = LaunchResult.Success(CurrentSession!);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("LaunchCanceled", "The launch was canceled before a process started.", Properties(item));
            result = LaunchResult.Cancelled(CurrentSession);
            return result;
        }
        catch (Exception exception)
        {
            if (_playSessions is not null && playSession is not null)
            {
                await _playSessions.FailSessionAsync(playSession.Id, exception.Message, CancellationToken.None).ConfigureAwait(false);
            }
            if(playSession is not null)await RecordActivityEndedAsync(item,playSession,DateTime.UtcNow,elapsed is null?0:Math.Max(0,(long)elapsed.Elapsed.TotalSeconds),ActivityResult.Failed,CancellationToken.None).ConfigureAwait(false);
            result = await FailAndRestoreAsync("NexaStation could not start this item. Check its launch target and try again.", exception)
                .ConfigureAwait(false);
            return result;
        }
        finally
        {
            if (_desktopControl?.IsActive == true)
            {
                await _desktopControl.StopAsync(DesktopControlStopReason.ApplicationExited, CancellationToken.None).ConfigureAwait(false);
            }
            if (runningProcess is not null)
            {
                await runningProcess.DisposeAsync().ConfigureAwait(false);
            }
            foreach (RunningProcess process in trackedProcesses)
            {
                if (!ReferenceEquals(process, runningProcess))
                {
                    await process.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (CurrentSession is not null)
            {
                TransitionTo(LaunchState.Idle);
            }

            Interlocked.Exchange(ref _launchInProgress, 0);
        }
    }

    private async Task<LaunchResult> FailAndRestoreAsync(string message, Exception? exception)
    {
        TransitionTo(LaunchState.Failed, failureMessage: message, ended: true);
            _logger.LogError("LaunchFailed", message, exception, SessionProperties());

        try
        {
            await RestoreFrontendAsync().ConfigureAwait(false);
        }
        catch (Exception restoreException)
        {
            _logger.LogError("RestoreFailed", "Frontend restoration failed after a launch error.", restoreException, SessionProperties());
        }

        return LaunchResult.Failure(message, CurrentSession);
    }

    private async Task RestoreFrontendAsync(LaunchTarget? target = null)
    {
        _logger.Information("RestoreAttempt", "Restoring the NexaStation frontend.", SessionProperties());
        if (target?.FrontendRestoreDelayMilliseconds > 0)
            await Task.Delay(Math.Clamp(target.FrontendRestoreDelayMilliseconds, 0, 30_000)).ConfigureAwait(false);
        if (target?.DoNotForceFrontendForeground != true)
            await _windowService.BringToForegroundAsync().ConfigureAwait(false);
        await _windowService.RestoreFullscreenAsync().ConfigureAwait(false);
    }

    private async Task RecordActivityStartedAsync(LibraryItem item,PlaySession session,CancellationToken token)
    {
        ActivityPrivacyLevel privacy=_activityPrivacy is null?(item.Hidden?ActivityPrivacyLevel.Private:ActivityPrivacyLevel.Normal):await _activityPrivacy.EvaluateAsync(item,token).ConfigureAwait(false);ActivityEventType type=item.ContentType==ContentType.Application?ActivityEventType.ApplicationOpened:ActivityEventType.GameStarted;if(_activityTimeline is not null)await _activityTimeline.RecordAsync(new(Guid.NewGuid().ToString("N"),type,ActivityTargetType.LibraryItem,item.Id,session.Id,item.Title,item.Subtitle,session.ProviderId,session.ProviderItemId,session.StartedAtUtc,null,null,ActivityResult.Succeeded,privacy,CreatedAtUtc:DateTime.UtcNow),token).ConfigureAwait(false);if(_presence is not null)await _presence.SetForLaunchAsync(item,privacy,session.ProviderId,session.StartedAtUtc,token).ConfigureAwait(false);
    }

    private async Task RecordActivityEndedAsync(LibraryItem item,PlaySession session,DateTime ended,long duration,ActivityResult result,CancellationToken token)
    {
        ActivityPrivacyLevel privacy=_activityPrivacy is null?(item.Hidden?ActivityPrivacyLevel.Private:ActivityPrivacyLevel.Normal):await _activityPrivacy.EvaluateAsync(item,token).ConfigureAwait(false);ActivityEventType type=item.ContentType==ContentType.Application?ActivityEventType.ApplicationEnded:ActivityEventType.GameEnded;if(_activityTimeline is not null)await _activityTimeline.RecordAsync(new(Guid.NewGuid().ToString("N"),type,ActivityTargetType.LibraryItem,item.Id,session.Id,item.Title,item.Subtitle,session.ProviderId,session.ProviderItemId,ended,ended,duration,result,privacy,CreatedAtUtc:DateTime.UtcNow),token).ConfigureAwait(false);if(_presence is not null)await _presence.ClearAsync(token).ConfigureAwait(false);
    }

    private void TransitionTo(LaunchState state, string? failureMessage = null, bool ended = false)
    {
        CurrentSession = CurrentSession! with
        {
            State = state,
            FailureMessage = failureMessage ?? CurrentSession.FailureMessage,
            EndedAt = ended ? DateTimeOffset.UtcNow : CurrentSession.EndedAt,
        };

        _logger.Information(
            "LaunchStateChanged",
            $"Launch state changed to {state}.",
            SessionProperties());
        StateChanged?.Invoke(this, new LaunchStateChangedEventArgs(CurrentSession));
    }

    private Dictionary<string, object?> SessionProperties() =>
        new Dictionary<string, object?>
        {
            ["SessionId"] = CurrentSession?.Id,
            ["LibraryItemId"] = CurrentSession?.LibraryItemId,
            ["State"] = CurrentSession?.State.ToString(),
            ["ProcessId"] = CurrentSession?.ProcessId,
        };

    private static Dictionary<string, object?> Properties(LibraryItem item) =>
        new Dictionary<string, object?> { ["LibraryItemId"] = item.Id, ["ContentType"] = item.ContentType.ToString() };

    private static string? SteamItemId(LaunchTarget target)
    {
        if (!string.Equals(target.ProviderId, "steam", StringComparison.OrdinalIgnoreCase) || target.Uri is null)
        {
            return null;
        }

        return target.Uri.AbsolutePath.Trim('/');
    }

    private static bool IsWindowsApplicationTarget(LaunchTarget target) =>
        target.WindowsLaunchMethod is not null || target.LaunchType is LaunchType.PackagedApplication or LaunchType.ShellItem or LaunchType.BrowserApplication;

    private static WindowsApplicationLaunchRequest ToWindowsRequest(LaunchTarget target) => new(
        target.WindowsLaunchMethod ?? target.LaunchType switch
        {
            LaunchType.PackagedApplication => WindowsLaunchMethod.AppUserModelId,
            LaunchType.ShellItem => WindowsLaunchMethod.ShellItem,
            LaunchType.BrowserApplication => WindowsLaunchMethod.BrowserApplication,
            LaunchType.WindowsShortcut => WindowsLaunchMethod.ShellLink,
            LaunchType.Uri => WindowsLaunchMethod.Uri,
            _ => WindowsLaunchMethod.Executable,
        },
        target.ShortcutPath,
        target.ExecutablePath,
        target.ArgumentList,
        target.WorkingDirectory,
        target.AppUserModelId,
        target.Uri,
        target.ShellParsingPath);

    private static RunningProcess AttachToProcess(int processId)
    {
        Process process = Process.GetProcessById(processId);
        process.EnableRaisingEvents = true;
        TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnExited(object? sender, EventArgs args)
        {
            try { exited.TrySetResult(process.ExitCode); }
            catch (InvalidOperationException exception) { exited.TrySetException(exception); }
        }
        process.Exited += OnExited;
        if (process.HasExited) exited.TrySetResult(process.ExitCode);
        return new(process.Id, exited.Task, () =>
        {
            process.Exited -= OnExited;
            process.Dispose();
            return ValueTask.CompletedTask;
        });
    }
}
