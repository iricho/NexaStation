namespace NexaStation.Input.Devices;

public enum ControllerConnectionType { Usb, Bluetooth, WirelessAdapter, Virtual, Unknown }
public enum ControllerRepresentationKind { Physical, Virtual, Unknown }
public enum ControllerRepresentationPreference { Automatic, PhysicalController, VirtualXboxController, Disabled }

public sealed record ControllerBackendCapabilities(
    bool ProvidesButtons = true,
    bool ProvidesAxes = true,
    bool ProvidesSwitches = false,
    bool ProvidesBattery = false,
    bool ProvidesVendorProduct = false,
    bool ProvidesRawLabels = false);

public sealed record ControllerBackendDevice(
    string RuntimeId,
    string BackendId,
    string? DisplayName,
    string? VendorId = null,
    string? ProductId = null,
    ControllerConnectionType ConnectionType = ControllerConnectionType.Unknown,
    bool IsVirtual = false,
    string? BackendDeviceId = null,
    int ButtonCount = 0,
    int AxisCount = 0,
    int SwitchCount = 0,
    string? CompatibilityProfileId = null);

public sealed record ControllerDevice(
    string RuntimeId,
    string DisplayName,
    ControllerFamily ControllerFamily,
    string BackendId,
    string BackendDeviceId,
    string PhysicalIdentityKey,
    string? VirtualIdentityKey,
    string? VendorId,
    string? ProductId,
    ControllerConnectionType ConnectionType,
    bool IsWireless,
    bool IsVirtual,
    bool IsDuplicateCandidate,
    string PreferredGlyphSet,
    string MappingProfileId,
    string CalibrationProfileId,
    ControllerBackendCapabilities Capabilities,
    string? BatteryStatus,
    DateTimeOffset LastSeenAtUtc);

public sealed record DisabledControllerRepresentation(string BackendId, string DeviceIdentityKey);

public sealed record ControllerResolutionOptions(
    string? PreferredRuntimeId = null,
    ControllerRepresentationPreference RepresentationPreference = ControllerRepresentationPreference.Automatic,
    ControllerFamily? UserFamilyOverride = null,
    string? UserGlyphSetOverride = null,
    IReadOnlySet<string>? DisabledRepresentationKeys = null);

public sealed record ControllerResolutionResult(
    IReadOnlyList<ControllerDevice> Devices,
    ControllerDevice? ActiveDevice,
    IReadOnlyList<ControllerDevice> SuppressedDuplicates,
    IReadOnlyList<string> Warnings);

public interface IControllerDeviceResolver
{
    Task<ControllerResolutionResult> ResolveAsync(
        IReadOnlyList<ControllerBackendDevice> candidates,
        ControllerResolutionOptions? options = null,
        CancellationToken cancellationToken = default);
}
