using NexaStation.Input.Devices;
using NexaStation.Input.Mapping;

namespace NexaStation.Input.Detection;

public interface IControllerFamilyDetector
{
    ControllerFamily Detect(ControllerDescriptor descriptor);
    InputMode ToInputMode(ControllerFamily family);
}

public sealed class ControllerFamilyDetector : IControllerFamilyDetector
{
    private static readonly string[] PlayStationTokens = ["dualsense", "dualshock", "playstation", "sony", "054c"];
    private static readonly string[] NintendoTokens = ["nintendo", "joy-con", "joycon", "switch", "057e"];
    private static readonly string[] XboxTokens = ["xbox", "xinput", "microsoft", "045e"];

    public ControllerFamily Detect(ControllerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.UserOverride is { } userOverride) return userOverride;
        RawControllerMappingProfile? knownProfile = BuiltInRawControllerMappingProfiles.Find(descriptor.VendorId, descriptor.ProductId, descriptor.DisplayName, ControllerConnectionType.Unknown);
        if (knownProfile is not null) return knownProfile.ControllerFamily;
        string signal = string.Join(' ', descriptor.DisplayName, descriptor.VendorId, descriptor.ProductId, descriptor.InputBackend).ToLowerInvariant();
        if (PlayStationTokens.Any(signal.Contains)) return ControllerFamily.PlayStation;
        if (NintendoTokens.Any(signal.Contains)) return ControllerFamily.Nintendo;
        if (XboxTokens.Any(signal.Contains)) return ControllerFamily.Xbox;
        return string.IsNullOrWhiteSpace(signal) ? ControllerFamily.Unknown : ControllerFamily.Generic;
    }

    public InputMode ToInputMode(ControllerFamily family) => family switch
    {
        ControllerFamily.Xbox => InputMode.XboxController,
        ControllerFamily.PlayStation => InputMode.PlayStationController,
        ControllerFamily.Nintendo => InputMode.NintendoController,
        ControllerFamily.Generic => InputMode.GenericController,
        _ => InputMode.UnknownController,
    };
}
