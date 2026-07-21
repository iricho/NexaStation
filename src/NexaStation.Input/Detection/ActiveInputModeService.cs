using NexaStation.Input.Devices;

namespace NexaStation.Input.Detection;

public interface IActiveInputModeService
{
    InputMode CurrentMode { get; }
    string? ActiveDeviceId { get; }
    event EventHandler<ActiveInputModeChangedEventArgs>? ModeChanged;
    void ReportInput(InputActivity activity);
}

public interface IInputDeviceLifecycleService
{
    void ReportDisconnected(string deviceId);
}

public sealed class ActiveInputModeService : IActiveInputModeService, IInputDeviceLifecycleService
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _stabilityThreshold;
    private readonly double _mouseThreshold;
    private readonly double _controllerThreshold;
    private readonly object _gate = new();
    private DateTimeOffset _lastAcceptedAt = DateTimeOffset.MinValue;
    private InputMode _currentMode = InputMode.Keyboard;
    private string? _activeDeviceId;

    public ActiveInputModeService(
        TimeProvider? timeProvider = null,
        TimeSpan? stabilityThreshold = null,
        double mouseThreshold = 4,
        double controllerThreshold = 0.55)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _stabilityThreshold = stabilityThreshold ?? TimeSpan.FromMilliseconds(140);
        _mouseThreshold = Math.Clamp(mouseThreshold, 1, 100);
        _controllerThreshold = Math.Clamp(controllerThreshold, 0.1, 0.95);
    }

    public InputMode CurrentMode { get { lock (_gate) return _currentMode; } }
    public string? ActiveDeviceId { get { lock (_gate) return _activeDeviceId; } }
    public event EventHandler<ActiveInputModeChangedEventArgs>? ModeChanged;

    public void ReportInput(InputActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (!IsMeaningful(activity)) return;

        ActiveInputModeChangedEventArgs? changed = null;
        lock (_gate)
        {
            DateTimeOffset now = activity.OccurredAt ?? _timeProvider.GetUtcNow();
            if (activity.Mode != InputMode.Touch && activity.Mode != _currentMode && now - _lastAcceptedAt < _stabilityThreshold) return;
            _lastAcceptedAt = now;
            if (activity.Mode == _currentMode && string.Equals(activity.DeviceId, _activeDeviceId, StringComparison.Ordinal)) return;
            InputMode previous = _currentMode;
            _currentMode = activity.Mode;
            _activeDeviceId = activity.DeviceId;
            changed = new(previous, _currentMode, _activeDeviceId);
        }

        ModeChanged?.Invoke(this, changed);
    }

    public void ReportDisconnected(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        ActiveInputModeChangedEventArgs? changed = null;
        lock (_gate)
        {
            if (!string.Equals(_activeDeviceId, deviceId, StringComparison.Ordinal)) return;
            InputMode previous = _currentMode;
            _currentMode = InputMode.Keyboard;
            _activeDeviceId = null;
            _lastAcceptedAt = _timeProvider.GetUtcNow();
            changed = new(previous, _currentMode, null);
        }

        ModeChanged?.Invoke(this, changed);
    }

    private bool IsMeaningful(InputActivity activity)
    {
        if (activity.IsSynthetic) return false;
        return activity.Mode switch
        {
            InputMode.Touch => true,
            InputMode.Mouse => activity.Magnitude >= _mouseThreshold,
            InputMode.XboxController or InputMode.PlayStationController or InputMode.NintendoController or InputMode.GenericController or InputMode.UnknownController
                => activity.IsButtonPress || activity.Magnitude >= _controllerThreshold,
            InputMode.Keyboard => activity.IsButtonPress || activity.Magnitude > 0,
            _ => false,
        };
    }
}
