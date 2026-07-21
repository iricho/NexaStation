using NexaStation.Core;
using NexaStation.Input.Devices;

namespace NexaStation.Input.Mapping;

public enum SemanticPhysicalButton
{
    Unknown,
    FaceSouth,
    FaceEast,
    FaceWest,
    FaceNorth,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    LeftStick,
    RightStick,
    Options,
    Create,
    Guide,
    TouchpadClick,
}

public enum SemanticAxis { LeftStickX, LeftStickY, RightStickX, RightStickY, LeftTrigger, RightTrigger, Unknown }

public sealed record RawButtonMapping(int RawIndex, SemanticPhysicalButton Button, bool ActiveHigh = true, int DebounceMilliseconds = 35);
public sealed record RawAxisMapping(int RawIndex, SemanticAxis Axis, double Minimum, double Center, double Maximum, bool Inverted = false, double Deadzone = 0.2, double ActivationThreshold = 0.55);
public sealed record RawSwitchMapping(int RawIndex, string SemanticSwitch, IReadOnlyDictionary<int, SemanticPhysicalButton> Values);
public sealed record RawDeadzoneSettings(double LeftStick = 0.24, double RightStick = 0.24, double Trigger = 0.18);
public sealed record RawTriggerSettings(double Rest = 0, double Pressed = 1, double ActivationThreshold = 0.55);
public sealed record RawHatSettings(bool EightWay = false);

public sealed record RawControllerMappingProfile(
    string Id,
    string Name,
    ControllerFamily ControllerFamily,
    string? VendorId,
    string? ProductId,
    ControllerConnectionType ConnectionType,
    string BackendId,
    IReadOnlyList<RawButtonMapping> ButtonMappings,
    IReadOnlyList<RawAxisMapping> AxisMappings,
    IReadOnlyList<RawSwitchMapping> SwitchMappings,
    RawDeadzoneSettings DeadzoneSettings,
    RawTriggerSettings TriggerSettings,
    RawHatSettings HatSettings,
    string GlyphSetId,
    bool BuiltIn,
    bool UserModified,
    int Version);

public static class BuiltInRawControllerMappingProfiles
{
    public const string DualSenseProfileId = "builtin.sony.dualsense";
    public const string DualShockProfileId = "builtin.sony.dualshock";
    public const string XboxProfileId = "builtin.xbox.standard";

    private static readonly RawControllerMappingProfile[] Profiles =
    [
        new(
            DualSenseProfileId,
            "Sony DualSense Controller",
            ControllerFamily.PlayStation,
            "054C",
            "0CE6",
            ControllerConnectionType.Unknown,
            "Windows.Gaming.Input",
            ButtonMappings: [
                new(0, SemanticPhysicalButton.FaceSouth),
                new(1, SemanticPhysicalButton.FaceEast),
                new(2, SemanticPhysicalButton.FaceWest),
                new(3, SemanticPhysicalButton.FaceNorth),
                new(4, SemanticPhysicalButton.LeftShoulder),
                new(5, SemanticPhysicalButton.RightShoulder),
                new(6, SemanticPhysicalButton.LeftTrigger),
                new(7, SemanticPhysicalButton.RightTrigger),
                new(8, SemanticPhysicalButton.Create),
                new(9, SemanticPhysicalButton.Options),
                new(10, SemanticPhysicalButton.LeftStick),
                new(11, SemanticPhysicalButton.RightStick),
                new(12, SemanticPhysicalButton.Guide),
                new(13, SemanticPhysicalButton.TouchpadClick),
            ],
            AxisMappings: [
                new(0, SemanticAxis.LeftStickX, -1, 0, 1),
                new(1, SemanticAxis.LeftStickY, -1, 0, 1, Inverted: true),
                new(2, SemanticAxis.RightStickX, -1, 0, 1),
                new(3, SemanticAxis.RightStickY, -1, 0, 1, Inverted: true),
                new(4, SemanticAxis.LeftTrigger, 0, 0, 1, ActivationThreshold: 0.55),
                new(5, SemanticAxis.RightTrigger, 0, 0, 1, ActivationThreshold: 0.55),
            ],
            SwitchMappings: [],
            DeadzoneSettings: new(LeftStick: 0.24, RightStick: 0.24, Trigger: 0.18),
            TriggerSettings: new(ActivationThreshold: 0.55),
            HatSettings: new(),
            GlyphSetId: "PlayStation",
            BuiltIn: true,
            UserModified: false,
            Version: 1),
        new(
            DualShockProfileId,
            "Sony DualShock Controller",
            ControllerFamily.PlayStation,
            "054C",
            null,
            ControllerConnectionType.Unknown,
            "Windows.Gaming.Input",
            ButtonMappings: [],
            AxisMappings: [],
            SwitchMappings: [],
            DeadzoneSettings: new(),
            TriggerSettings: new(),
            HatSettings: new(),
            GlyphSetId: "PlayStation",
            BuiltIn: true,
            UserModified: false,
            Version: 1),
        new(
            XboxProfileId,
            "Xbox Compatible Controller",
            ControllerFamily.Xbox,
            "045E",
            null,
            ControllerConnectionType.Unknown,
            "Windows.Gaming.Input",
            ButtonMappings: [],
            AxisMappings: [],
            SwitchMappings: [],
            DeadzoneSettings: new(),
            TriggerSettings: new(),
            HatSettings: new(),
            GlyphSetId: "Xbox",
            BuiltIn: true,
            UserModified: false,
            Version: 1),
    ];

    public static IReadOnlyList<RawControllerMappingProfile> All => Profiles;

    public static RawControllerMappingProfile? Find(string? vendorId, string? productId, string? displayName, ControllerConnectionType connectionType)
    {
        string vendor = NormalizeHex(vendorId);
        string product = NormalizeHex(productId);
        string signal = string.Join(' ', displayName, vendor, product).ToLowerInvariant();
        return Profiles.FirstOrDefault(profile =>
            Matches(profile.VendorId, vendor) &&
            (profile.ProductId is null || Matches(profile.ProductId, product)) &&
            (profile.ConnectionType == ControllerConnectionType.Unknown || profile.ConnectionType == connectionType))
            ?? (signal.Contains("dualsense") ? Profiles.First(profile => profile.Id == DualSenseProfileId) : null)
            ?? (signal.Contains("dualshock") ? Profiles.First(profile => profile.Id == DualShockProfileId) : null)
            ?? (signal.Contains("xbox") || signal.Contains("xinput") ? Profiles.First(profile => profile.Id == XboxProfileId) : null);
    }

    public static IReadOnlyDictionary<InputAction, SemanticPhysicalButton> DefaultSemanticButtons(ControllerFamily family) => family switch
    {
        ControllerFamily.PlayStation => new Dictionary<InputAction, SemanticPhysicalButton>
        {
            [InputAction.Confirm] = SemanticPhysicalButton.FaceSouth,
            [InputAction.Cancel] = SemanticPhysicalButton.FaceEast,
            [InputAction.ContextMenu] = SemanticPhysicalButton.FaceWest,
            [InputAction.Favorite] = SemanticPhysicalButton.FaceNorth,
            [InputAction.Options] = SemanticPhysicalButton.Options,
            [InputAction.QuickMenu] = SemanticPhysicalButton.Create,
            [InputAction.PreviousSection] = SemanticPhysicalButton.LeftShoulder,
            [InputAction.NextSection] = SemanticPhysicalButton.RightShoulder,
            [InputAction.PreviousPage] = SemanticPhysicalButton.LeftTrigger,
            [InputAction.NextPage] = SemanticPhysicalButton.RightTrigger,
            [InputAction.NavigateUp] = SemanticPhysicalButton.DpadUp,
            [InputAction.NavigateDown] = SemanticPhysicalButton.DpadDown,
            [InputAction.NavigateLeft] = SemanticPhysicalButton.DpadLeft,
            [InputAction.NavigateRight] = SemanticPhysicalButton.DpadRight,
        },
        _ => new Dictionary<InputAction, SemanticPhysicalButton>
        {
            [InputAction.Confirm] = SemanticPhysicalButton.FaceSouth,
            [InputAction.Cancel] = SemanticPhysicalButton.FaceEast,
            [InputAction.ContextMenu] = SemanticPhysicalButton.FaceWest,
            [InputAction.Favorite] = SemanticPhysicalButton.FaceNorth,
            [InputAction.Options] = SemanticPhysicalButton.Options,
            [InputAction.QuickMenu] = SemanticPhysicalButton.Create,
        },
    };

    private static bool Matches(string? expected, string actual) => string.IsNullOrWhiteSpace(expected) || string.Equals(NormalizeHex(expected), actual, StringComparison.OrdinalIgnoreCase);
    private static string NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[2..];
        return trimmed.PadLeft(4, '0').ToUpperInvariant();
    }
}
