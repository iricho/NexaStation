using System.Runtime.CompilerServices;
using NexaStation.Core;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.Input.Mapping;
using Windows.Gaming.Input;

namespace NexaStation.Infrastructure;

public sealed class WindowsGamepadInputService : IInputService, IInputCompatibilitySink, IDisposable
{
    private readonly IAppLogger _logger;
    private readonly IActiveInputModeService _activeInputMode;
    private readonly IInputDeviceLifecycleService _deviceLifecycle;
    private readonly IControllerFamilyDetector _familyDetector;
    private readonly IControllerDeviceResolver _deviceResolver;
    private readonly IInputBindingResolver _bindings;
    private readonly IInputContextService _context;
    private double _stickThreshold = 0.55;
    private bool _analogNavigationDisabled;
    private TimeSpan _repeatDelay = TimeSpan.FromMilliseconds(350);
    private TimeSpan _repeatRate = TimeSpan.FromMilliseconds(120);
    private CancellationTokenSource? _pollingCancellation;
    private GamepadButtons _previousButtons;
    private string? _repeatingDirection;
    private DateTimeOffset _nextRepeatAt;
    private string? _activeDeviceId;
    private bool _previousLeftTriggerPressed;
    private bool _previousRightTriggerPressed;

    public WindowsGamepadInputService(
        IAppLogger logger,
        IActiveInputModeService activeInputMode,
        IInputDeviceLifecycleService deviceLifecycle,
        IControllerFamilyDetector familyDetector,
        IControllerDeviceResolver deviceResolver,
        IInputBindingResolver bindings,
        IInputContextService context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeInputMode = activeInputMode ?? throw new ArgumentNullException(nameof(activeInputMode));
        _deviceLifecycle = deviceLifecycle ?? throw new ArgumentNullException(nameof(deviceLifecycle));
        _familyDetector = familyDetector ?? throw new ArgumentNullException(nameof(familyDetector));
        _deviceResolver = deviceResolver ?? throw new ArgumentNullException(nameof(deviceResolver));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public event EventHandler<InputActionEventArgs>? ActionTriggered;

    public void Start()
    {
        if (_pollingCancellation is not null) return;
        _pollingCancellation = new CancellationTokenSource();
        _ = PollAsync(_pollingCancellation.Token);
        _logger.Information("InputServiceStarted", "Adaptive Windows gamepad polling started.");
    }

    public void Stop()
    {
        CancellationTokenSource? cancellation = Interlocked.Exchange(ref _pollingCancellation, null);
        if (cancellation is null) return;
        cancellation.Cancel(); cancellation.Dispose(); ResetState();
        _logger.Information("InputServiceStopped", "Adaptive Windows gamepad polling stopped.");
    }

    public void Dispose() => Stop();

    public void ApplyCompatibility(InputNavigationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _stickThreshold = Math.Clamp(settings.NavigationDeadzone, 0.1, 0.95);
        _analogNavigationDisabled = settings.DisableAnalogNavigation || settings.UseDpadOnly;
        _repeatDelay = TimeSpan.FromMilliseconds(Math.Clamp(settings.RepeatDelayMilliseconds, 100, 2000));
        _repeatRate = TimeSpan.FromMilliseconds(Math.Clamp(settings.RepeatRateMilliseconds, 50, 1000));
        _repeatingDirection = null;
        _logger.Information("ControllerCompatibilityApplied", "Validated controller navigation compatibility settings were applied.", new Dictionary<string, object?>
        {
            ["NavigationDeadzone"] = _stickThreshold, ["AnalogNavigationDisabled"] = _analogNavigationDisabled,
            ["RepeatDelayMilliseconds"] = _repeatDelay.TotalMilliseconds, ["RepeatRateMilliseconds"] = _repeatRate.TotalMilliseconds,
        });
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(16));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                IReadOnlyList<Gamepad> gamepads = Gamepad.Gamepads;
                ControllerResolutionResult resolution = await _deviceResolver.ResolveAsync(gamepads.Select(ToBackendDevice).ToArray(), cancellationToken: cancellationToken).ConfigureAwait(false);
                Gamepad? gamepad = resolution.ActiveDevice is null ? null : gamepads.FirstOrDefault(candidate => RuntimeId(candidate) == resolution.ActiveDevice.RuntimeId);
                if (gamepad is null) { ReportDisconnect(); ResetState(); continue; }
                string deviceId = resolution.ActiveDevice?.RuntimeId ?? RuntimeId(gamepad);
                _activeDeviceId = deviceId;
                GamepadReading reading = gamepad.GetCurrentReading();
                HandleEdgeButtons(gamepad, deviceId, reading.Buttons);
                HandleTriggerEdges(gamepad, deviceId, reading.LeftTrigger, reading.RightTrigger);
                string? direction = DpadDirection(reading.Buttons) ?? StickDirection(reading.LeftThumbstickX, reading.LeftThumbstickY);
                HandleDirection(gamepad, deviceId, direction, Math.Max(Math.Abs(reading.LeftThumbstickX), Math.Abs(reading.LeftThumbstickY)));
                _previousButtons = reading.Buttons;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { _logger.Information("InputPollingCanceled", "Windows gamepad polling ended normally."); }
        catch (Exception exception) { _logger.LogError("InputServiceFailed", "Windows gamepad polling stopped unexpectedly.", exception); Stop(); }
    }

    private void HandleEdgeButtons(Gamepad gamepad, string deviceId, GamepadButtons current)
    {
        GamepadButtons pressed = current & ~_previousButtons;
        bool captureChordHeld = current.HasFlag(GamepadButtons.LeftShoulder) && current.HasFlag(GamepadButtons.RightShoulder) && current.HasFlag(GamepadButtons.X);
        bool emergencyChordHeld = current.HasFlag(GamepadButtons.LeftShoulder) && current.HasFlag(GamepadButtons.RightShoulder) && current.HasFlag(GamepadButtons.LeftThumbstick) && current.HasFlag(GamepadButtons.RightThumbstick);
        if (captureChordHeld && ((pressed & (GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.X)) != GamepadButtons.None))
            RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerChord, "LEFT_SHOULDER+RIGHT_SHOULDER+FACE_WEST"), 1);
        if (emergencyChordHeld && ((pressed & (GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder | GamepadButtons.LeftThumbstick | GamepadButtons.RightThumbstick)) != GamepadButtons.None))
            RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerChord, "LEFT_SHOULDER+RIGHT_SHOULDER+LEFT_STICK+RIGHT_STICK"), 1);
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.A, "FACE_SOUTH");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.B, "FACE_EAST");
        if (!captureChordHeld) RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.X, "FACE_WEST");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.Y, "FACE_NORTH");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.Menu, "MENU");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.View, "VIEW");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.LeftShoulder, "LEFT_SHOULDER");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.RightShoulder, "RIGHT_SHOULDER");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.LeftThumbstick, "LEFT_STICK");
        RaiseIfPressed(gamepad, deviceId, pressed, GamepadButtons.RightThumbstick, "RIGHT_STICK");
    }

    private void HandleTriggerEdges(Gamepad gamepad, string deviceId, double leftTrigger, double rightTrigger)
    {
        bool leftPressed = leftTrigger >= _stickThreshold;
        bool rightPressed = rightTrigger >= _stickThreshold;
        if (leftPressed && !_previousLeftTriggerPressed) RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerButton, "LEFT_TRIGGER"), leftTrigger);
        if (rightPressed && !_previousRightTriggerPressed) RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerButton, "RIGHT_TRIGGER"), rightTrigger);
        _previousLeftTriggerPressed = leftPressed;
        _previousRightTriggerPressed = rightPressed;
    }

    private void HandleDirection(Gamepad gamepad, string deviceId, string? code, double magnitude)
    {
        if (code is null) { _repeatingDirection = null; return; }
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (!string.Equals(_repeatingDirection, code, StringComparison.Ordinal))
        {
            _repeatingDirection = code; _nextRepeatAt = now + _repeatDelay;
            RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerButton, code), Math.Max(1, magnitude));
            return;
        }
        if (now < _nextRepeatAt) return;
        _nextRepeatAt = now + _repeatRate;
        RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerButton, code), Math.Max(1, magnitude));
    }

    private string? StickDirection(double x, double y)
    {
        if (_analogNavigationDisabled || (Math.Abs(x) < _stickThreshold && Math.Abs(y) < _stickThreshold)) return null;
        return Math.Abs(x) > Math.Abs(y) ? (x > 0 ? "DPAD_RIGHT" : "DPAD_LEFT") : (y > 0 ? "DPAD_UP" : "DPAD_DOWN");
    }

    private static string? DpadDirection(GamepadButtons buttons)
    {
        if (buttons.HasFlag(GamepadButtons.DPadUp)) return "DPAD_UP";
        if (buttons.HasFlag(GamepadButtons.DPadDown)) return "DPAD_DOWN";
        if (buttons.HasFlag(GamepadButtons.DPadLeft)) return "DPAD_LEFT";
        if (buttons.HasFlag(GamepadButtons.DPadRight)) return "DPAD_RIGHT";
        return null;
    }

    private void RaiseIfPressed(Gamepad gamepad, string deviceId, GamepadButtons pressed, GamepadButtons button, string code)
    {
        if ((pressed & button) == button) RaisePhysical(gamepad, deviceId, new(PhysicalInputKind.ControllerButton, code), 1);
    }

    private void RaisePhysical(Gamepad gamepad, string deviceId, PhysicalInput physicalInput, double magnitude)
    {
        RawGameController? raw = RawGameController.FromGameController(gamepad);
        string? displayName = raw?.DisplayName;
        string? vendorId = raw is null ? null : raw.HardwareVendorId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        string? productId = raw is null ? null : raw.HardwareProductId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        ControllerFamily family = _familyDetector.Detect(new(deviceId, displayName, vendorId, productId, "Windows.Gaming.Input"));
        _activeInputMode.ReportInput(new(_familyDetector.ToInputMode(family), deviceId, family, magnitude, IsButtonPress: true));
        InputAction? action = _bindings.ResolveAction(physicalInput, _context.Current, deviceId);
        if (action is { } resolved) ActionTriggered?.Invoke(this, new(resolved, physicalInput.NormalizedCode, deviceId));
    }

    private void ReportDisconnect()
    {
        string? disconnected = Interlocked.Exchange(ref _activeDeviceId, null);
        if (disconnected is not null) _deviceLifecycle.ReportDisconnected(disconnected);
    }

    private void ResetState() { _previousButtons = GamepadButtons.None; _previousLeftTriggerPressed = false; _previousRightTriggerPressed = false; _repeatingDirection = null; }

    private static ControllerBackendDevice ToBackendDevice(Gamepad gamepad)
    {
        RawGameController? raw = RawGameController.FromGameController(gamepad);
        string runtimeId = RuntimeId(gamepad);
        string? vendorId = raw is null ? null : raw.HardwareVendorId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        string? productId = raw is null ? null : raw.HardwareProductId.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        string? displayName = raw?.DisplayName;
        bool virtualDevice = IsVirtualXbox(displayName, vendorId);
        return new(runtimeId, "Windows.Gaming.Input", displayName, vendorId, productId, ConnectionType(displayName, virtualDevice), virtualDevice, runtimeId, raw?.ButtonCount ?? 0, raw?.AxisCount ?? 0, raw?.SwitchCount ?? 0);
    }

    private static string RuntimeId(Gamepad gamepad) => RuntimeHelpers.GetHashCode(gamepad).ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static bool IsVirtualXbox(string? displayName, string? vendorId)
    {
        string signal = string.Join(' ', displayName, vendorId).ToLowerInvariant();
        return signal.Contains("xinput") || signal.Contains("virtual") || signal.Contains("vigem") || signal.Contains("045e");
    }

    private static ControllerConnectionType ConnectionType(string? displayName, bool virtualDevice)
    {
        if (virtualDevice) return ControllerConnectionType.Virtual;
        string signal = displayName?.ToLowerInvariant() ?? string.Empty;
        if (signal.Contains("bluetooth") || signal.Contains("wireless")) return ControllerConnectionType.Bluetooth;
        if (signal.Contains("usb")) return ControllerConnectionType.Usb;
        return ControllerConnectionType.Unknown;
    }
}
