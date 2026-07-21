using System.Globalization;
using System.Runtime.CompilerServices;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;
using Windows.Gaming.Input;

namespace NexaStation.SystemIntegration.Controllers;

public sealed class WindowsControllerStatusService : IControllerStatusService, IDisposable
{
    private readonly IControllerFamilyDetector _familyDetector;
    private readonly IControllerDeviceResolver _deviceResolver;
    private readonly IActiveInputModeService _activeInput;

    public WindowsControllerStatusService(IControllerFamilyDetector familyDetector, IControllerDeviceResolver deviceResolver, IActiveInputModeService activeInput)
    {
        _familyDetector = familyDetector ?? throw new ArgumentNullException(nameof(familyDetector));
        _deviceResolver = deviceResolver ?? throw new ArgumentNullException(nameof(deviceResolver));
        _activeInput = activeInput ?? throw new ArgumentNullException(nameof(activeInput));
        Gamepad.GamepadAdded += OnChanged; Gamepad.GamepadRemoved += OnChanged;
    }

    public event EventHandler<ControllerStatusChangedEventArgs>? StatusChanged;

    public Task<IReadOnlyList<ControllerStatus>> GetControllersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Gamepad[] gamepads = Gamepad.Gamepads.ToArray();
        ControllerResolutionResult resolution = _deviceResolver.ResolveAsync(gamepads.Select(ToBackendDevice).ToArray(), cancellationToken: cancellationToken).GetAwaiter().GetResult();
        IReadOnlyList<ControllerStatus> result = gamepads.Select((gamepad, index) =>
        {
            string deviceId = RuntimeHelpers.GetHashCode(gamepad).ToString(CultureInfo.InvariantCulture);
            RawGameController? raw = RawGameController.FromGameController(gamepad);
            string displayName = raw?.DisplayName ?? "Game controller";
            string? vendorId = raw is null ? null : raw.HardwareVendorId.ToString("X4", CultureInfo.InvariantCulture);
            string? productId = raw is null ? null : raw.HardwareProductId.ToString("X4", CultureInfo.InvariantCulture);
            ControllerDevice? resolved = resolution.Devices.FirstOrDefault(device => string.Equals(device.RuntimeId, deviceId, StringComparison.Ordinal));
            ControllerFamily family = resolved?.ControllerFamily ?? _familyDetector.Detect(new(deviceId, displayName, vendorId, productId, "Windows.Gaming.Input"));
            return new ControllerStatus(deviceId, displayName, family.ToString(), true, resolved?.IsWireless, ControllerBatteryState.Unknown, null, index, string.Equals(_activeInput.ActiveDeviceId, deviceId, StringComparison.Ordinal), null);
        }).ToArray(); return Task.FromResult(result);
    }

    public void Dispose() { Gamepad.GamepadAdded -= OnChanged; Gamepad.GamepadRemoved -= OnChanged; }
    private async void OnChanged(object? sender, Gamepad gamepad) { _ = gamepad; StatusChanged?.Invoke(this, new(await GetControllersAsync())); }

    private static ControllerBackendDevice ToBackendDevice(Gamepad gamepad)
    {
        RawGameController? raw = RawGameController.FromGameController(gamepad);
        string deviceId = RuntimeHelpers.GetHashCode(gamepad).ToString(CultureInfo.InvariantCulture);
        string? displayName = raw?.DisplayName;
        string? vendorId = raw is null ? null : raw.HardwareVendorId.ToString("X4", CultureInfo.InvariantCulture);
        string? productId = raw is null ? null : raw.HardwareProductId.ToString("X4", CultureInfo.InvariantCulture);
        bool virtualDevice = IsVirtualXbox(displayName, vendorId);
        return new(deviceId, "Windows.Gaming.Input", displayName, vendorId, productId, ConnectionType(displayName, virtualDevice), virtualDevice, deviceId, raw?.ButtonCount ?? 0, raw?.AxisCount ?? 0, raw?.SwitchCount ?? 0);
    }

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
