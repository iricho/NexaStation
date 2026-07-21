namespace NexaStation.Core;

public sealed class InputActionEventArgs(InputAction action, string? physicalInputCode = null, string? deviceId = null) : EventArgs
{
    public InputAction Action { get; } = action;
    public string? PhysicalInputCode { get; } = physicalInputCode;
    public string? DeviceId { get; } = deviceId;
}
