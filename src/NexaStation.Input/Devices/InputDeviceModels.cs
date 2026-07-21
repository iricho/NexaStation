namespace NexaStation.Input.Devices;

public enum InputMode
{
    Keyboard,
    Mouse,
    Touch,
    XboxController,
    PlayStationController,
    NintendoController,
    GenericController,
    UnknownController,
}

public enum ControllerFamily { Xbox, PlayStation, Nintendo, Generic, Unknown }

public sealed record InputActivity(
    InputMode Mode,
    string? DeviceId = null,
    ControllerFamily ControllerFamily = ControllerFamily.Unknown,
    double Magnitude = 1,
    bool IsButtonPress = false,
    bool IsSynthetic = false,
    DateTimeOffset? OccurredAt = null);

public sealed class ActiveInputModeChangedEventArgs(
    InputMode previousMode,
    InputMode currentMode,
    string? activeDeviceId) : EventArgs
{
    public InputMode PreviousMode { get; } = previousMode;
    public InputMode CurrentMode { get; } = currentMode;
    public string? ActiveDeviceId { get; } = activeDeviceId;
}

public sealed record ControllerDescriptor(
    string DeviceId,
    string? DisplayName,
    string? VendorId = null,
    string? ProductId = null,
    string? InputBackend = null,
    ControllerFamily? UserOverride = null);
