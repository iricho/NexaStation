using NexaStation.Core;
using NexaStation.DesktopControl.Mouse;
using NexaStation.Domain;

namespace NexaStation.DesktopControl.Profiles;

public sealed class DesktopControlSession(
    IDesktopControlProfileProvider profiles,
    IDesktopControllerStateSource controller,
    IDesktopInputInjector injector,
    IDesktopControlSafetyGuard safety,
    IPhysicalMouseActivityMonitor physicalMouse) : IDesktopControlSession, IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _sessionCancellation;
    private Task? _loop;
    private bool _disposed;
    private DesktopControlStopReason? _requestedStopReason;

    public bool IsActive { get; private set; }
    public event EventHandler<DesktopControlStopReason>? Stopped;

    public async Task StartAsync(DesktopControlSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.IsGame || request.IsEmulator)
            throw new InvalidOperationException("Desktop control is opt-in for Windows applications and is never enabled for games or emulators.");
        if (request.IsElevatedTargetKnown)
            throw new InvalidOperationException("Desktop control is disabled for elevated target applications.");

        DesktopControlProfileResolution resolution = await profiles.ResolveForItemAsync(request.LibraryItemId, cancellationToken).ConfigureAwait(false);
        if (!resolution.EnableOnLaunch || resolution.Profile is not { Enabled: true } profile)
            return;
        DesktopControlSafetyResult allowed = safety.CanControl(request.TargetProcessId);
        if (!allowed.Allowed) throw new InvalidOperationException(allowed.Message);

        await StopAsync(DesktopControlStopReason.UserDisabled, cancellationToken).ConfigureAwait(false);
        CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _sessionCancellation = linked;
            _requestedStopReason = null;
            IsActive = true;
            _loop = RunAsync(profile, linked.Token);
        }
    }

    public async Task StopAsync(DesktopControlStopReason reason, CancellationToken cancellationToken = default)
    {
        Task? loop;
        lock (_gate)
        {
            if (!IsActive && _loop is null) return;
            _requestedStopReason = reason;
            _sessionCancellation?.Cancel();
            loop = _loop;
        }
        if (loop is not null)
        {
            try { await loop.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }
        Complete(reason);
    }

    private async Task RunAsync(DesktopControlProfile profile, CancellationToken cancellationToken)
    {
        DesktopControllerButtons previous = DesktopControllerButtons.None;
        DateTime previousAt = DateTime.UtcNow;
        DateTime? disconnectedAt = null;
        int failures = 0;
        DesktopControlStopReason reason = DesktopControlStopReason.Cancelled;
        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(16));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                DesktopControllerReading reading = controller.Read();
                if (!reading.Connected)
                {
                    disconnectedAt ??= DateTime.UtcNow;
                    if (DateTime.UtcNow - disconnectedAt >= TimeSpan.FromSeconds(1)) { reason = DesktopControlStopReason.ControllerDisconnected; break; }
                    continue;
                }
                disconnectedAt = null;
                if (reading.Buttons.HasFlag(DesktopControllerButtons.LeftThumb) && reading.Buttons.HasFlag(DesktopControllerButtons.RightThumb))
                {
                    reason = DesktopControlStopReason.EmergencyDisabled;
                    break;
                }

                DateTime now = DateTime.UtcNow;
                double elapsed = Math.Clamp((now - previousAt).TotalSeconds, 0, 0.1);
                previousAt = now;
                if (!profile.PauseOnPhysicalMouseInput || !physicalMouse.ShouldPause(now))
                {
                    DesktopCursorMotion motion = CursorMotionCalculator.Calculate(reading.LeftX, reading.LeftY, elapsed, profile, reading.Buttons.HasFlag(DesktopControllerButtons.LeftShoulder), reading.Buttons.HasFlag(DesktopControllerButtons.RightShoulder));
                    if (motion.DeltaX != 0 || motion.DeltaY != 0)
                    {
                        DesktopInputInjectionResult moved = injector.MoveCursor(motion.DeltaX, motion.DeltaY);
                        physicalMouse.RecordControllerMovement(now);
                        failures = moved.Succeeded ? 0 : failures + 1;
                    }
                    int verticalScroll = Math.Abs(reading.RightY) <= profile.CursorDeadzone ? 0 : (int)Math.Round(reading.RightY * profile.ScrollSensitivity * elapsed);
                    int horizontalScroll = Math.Abs(reading.RightX) <= profile.CursorDeadzone ? 0 : (int)Math.Round(reading.RightX * profile.ScrollSensitivity * elapsed);
                    if (verticalScroll != 0 || horizontalScroll != 0)
                        failures = injector.Scroll(verticalScroll, horizontalScroll).Succeeded ? 0 : failures + 1;
                }

                failures += HandleEdge(reading.Buttons, previous, DesktopControllerButtons.A, profile.LeftClickAction) ? 0 : 1;
                failures += HandleEdge(reading.Buttons, previous, DesktopControllerButtons.B, profile.RightClickAction) ? 0 : 1;
                failures += HandleEdge(reading.Buttons, previous, DesktopControllerButtons.X, profile.BackAction) ? 0 : 1;
                failures += HandleEdge(reading.Buttons, previous, DesktopControllerButtons.Y, profile.ForwardAction) ? 0 : 1;
                failures += HandleEdge(reading.Buttons, previous, DesktopControllerButtons.View, profile.EscapeAction) ? 0 : 1;
                previous = reading.Buttons;
                if (failures >= 3) { reason = DesktopControlStopReason.InjectionFailures; break; }
            }
        }
        catch (OperationCanceledException) { reason = _requestedStopReason ?? DesktopControlStopReason.Cancelled; }
        finally
        {
            injector.ReleaseAll();
            Complete(reason);
        }
    }

    private bool HandleEdge(DesktopControllerButtons current, DesktopControllerButtons previous, DesktopControllerButtons button, DesktopControlAction? action)
    {
        if (!current.HasFlag(button) || previous.HasFlag(button) || action is null or DesktopControlAction.None) return true;
        DesktopInputInjectionResult result = action switch
        {
            DesktopControlAction.LeftClick => injector.Click(DesktopMouseButton.Left),
            DesktopControlAction.RightClick => injector.Click(DesktopMouseButton.Right),
            DesktopControlAction.MiddleClick => injector.Click(DesktopMouseButton.Middle),
            DesktopControlAction.Escape => injector.SendApprovedKey(DesktopApprovedKey.Escape),
            DesktopControlAction.Enter => injector.SendApprovedKey(DesktopApprovedKey.Enter),
            DesktopControlAction.Space => injector.SendApprovedKey(DesktopApprovedKey.Space),
            DesktopControlAction.Tab => injector.SendApprovedKey(DesktopApprovedKey.Tab),
            DesktopControlAction.ShiftTab => injector.SendApprovedKey(DesktopApprovedKey.ShiftTab),
            DesktopControlAction.BrowserBack => injector.SendApprovedKey(DesktopApprovedKey.BrowserBack),
            DesktopControlAction.BrowserForward => injector.SendApprovedKey(DesktopApprovedKey.BrowserForward),
            DesktopControlAction.MediaPlayPause => injector.SendApprovedKey(DesktopApprovedKey.MediaPlayPause),
            DesktopControlAction.VolumeUp => injector.SendApprovedKey(DesktopApprovedKey.VolumeUp),
            DesktopControlAction.VolumeDown => injector.SendApprovedKey(DesktopApprovedKey.VolumeDown),
            DesktopControlAction.Mute => injector.SendApprovedKey(DesktopApprovedKey.Mute),
            _ => new(true, "The action does not inject desktop input."),
        };
        return result.Succeeded;
    }

    private void Complete(DesktopControlStopReason reason)
    {
        bool raise;
        lock (_gate)
        {
            raise = IsActive;
            IsActive = false;
            _sessionCancellation?.Dispose();
            _sessionCancellation = null;
            _loop = null;
        }
        if (raise) Stopped?.Invoke(this, reason);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionCancellation?.Cancel();
        injector.ReleaseAll();
        _sessionCancellation?.Dispose();
    }
}
