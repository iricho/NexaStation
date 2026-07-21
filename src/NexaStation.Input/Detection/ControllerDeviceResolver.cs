using NexaStation.Input.Devices;
using NexaStation.Input.Mapping;

namespace NexaStation.Input.Detection;

public sealed class ControllerDeviceResolver(IControllerFamilyDetector familyDetector) : IControllerDeviceResolver
{
    public Task<ControllerResolutionResult> ResolveAsync(
        IReadOnlyList<ControllerBackendDevice> candidates,
        ControllerResolutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new();

        HashSet<string> disabled = options.DisabledRepresentationKeys is null
            ? []
            : new(options.DisabledRepresentationKeys, StringComparer.OrdinalIgnoreCase);

        List<ControllerDevice> resolved = [];
        List<string> warnings = [];
        foreach (ControllerBackendDevice candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string identityKey = BuildIdentityKey(candidate);
            if (disabled.Contains(identityKey) || disabled.Contains($"{candidate.BackendId}:{identityKey}")) continue;
            ControllerFamily detected = options.UserFamilyOverride ?? familyDetector.Detect(new(
                candidate.RuntimeId,
                candidate.DisplayName,
                candidate.VendorId,
                candidate.ProductId,
                candidate.BackendId));
            string mappingProfile = candidate.CompatibilityProfileId
                ?? BuiltInRawControllerMappingProfiles.Find(candidate.VendorId, candidate.ProductId, candidate.DisplayName, candidate.ConnectionType)?.Id
                ?? DefaultInputBindings.BuiltInProfileId;
            string glyphSet = options.UserGlyphSetOverride ?? detected switch
            {
                ControllerFamily.PlayStation => "PlayStation",
                ControllerFamily.Nintendo => "Nintendo",
                ControllerFamily.Xbox => "Xbox",
                _ => "Generic",
            };
            resolved.Add(new(
                candidate.RuntimeId,
                string.IsNullOrWhiteSpace(candidate.DisplayName) ? $"{detected} Controller" : candidate.DisplayName.Trim(),
                detected,
                candidate.BackendId,
                candidate.BackendDeviceId ?? candidate.RuntimeId,
                identityKey,
                candidate.IsVirtual ? identityKey : null,
                NormalizeHex(candidate.VendorId),
                NormalizeHex(candidate.ProductId),
                candidate.IsVirtual ? ControllerConnectionType.Virtual : candidate.ConnectionType,
                candidate.ConnectionType is ControllerConnectionType.Bluetooth or ControllerConnectionType.WirelessAdapter,
                candidate.IsVirtual,
                false,
                glyphSet,
                mappingProfile,
                "builtin.default",
                new(ProvidesVendorProduct: !string.IsNullOrWhiteSpace(candidate.VendorId) || !string.IsNullOrWhiteSpace(candidate.ProductId), ProvidesRawLabels: candidate.ButtonCount > 0 || candidate.AxisCount > 0 || candidate.SwitchCount > 0),
                null,
                DateTimeOffset.UtcNow));
        }

        List<ControllerDevice> suppressed = [];
        List<ControllerDevice> activeCandidates = [];
        foreach (IGrouping<string, ControllerDevice> group in resolved.GroupBy(device => device.PhysicalIdentityKey, StringComparer.OrdinalIgnoreCase))
        {
            ControllerDevice[] devices = group.ToArray();
            if (devices.Length == 1)
            {
                activeCandidates.Add(devices[0]);
                continue;
            }
            if (!devices.Any(device => device.IsVirtual) || !devices.Any(device => !device.IsVirtual))
            {
                activeCandidates.AddRange(devices);
                continue;
            }

            ControllerDevice preferred = SelectPreferredDuplicate(devices, options);
            activeCandidates.Add(preferred with { IsDuplicateCandidate = true });
            suppressed.AddRange(devices.Where(device => !string.Equals(device.RuntimeId, preferred.RuntimeId, StringComparison.Ordinal)).Select(device => device with { IsDuplicateCandidate = true }));
            warnings.Add($"Suppressed {devices.Length - 1} duplicate representation(s) for {preferred.DisplayName}. Use Controller Diagnostics to choose a different representation.");
        }

        ControllerDevice? active = SelectActive(activeCandidates, options);
        return Task.FromResult(new ControllerResolutionResult(activeCandidates, active, suppressed, warnings));
    }

    private static ControllerDevice SelectPreferredDuplicate(ControllerDevice[] devices, ControllerResolutionOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PreferredRuntimeId))
        {
            ControllerDevice? preferred = devices.FirstOrDefault(device => string.Equals(device.RuntimeId, options.PreferredRuntimeId, StringComparison.Ordinal));
            if (preferred is not null) return preferred;
        }

        return options.RepresentationPreference switch
        {
            ControllerRepresentationPreference.PhysicalController => devices.FirstOrDefault(device => !device.IsVirtual) ?? devices[0],
            ControllerRepresentationPreference.VirtualXboxController => devices.FirstOrDefault(device => device.IsVirtual && device.ControllerFamily == ControllerFamily.Xbox) ?? devices.FirstOrDefault(device => device.IsVirtual) ?? devices[0],
            _ => devices
                .OrderBy(device => device.IsVirtual && devices.Any(other => !other.IsVirtual && other.ControllerFamily == ControllerFamily.PlayStation) ? 1 : 0)
                .ThenBy(device => device.ControllerFamily == ControllerFamily.Unknown ? 1 : 0)
                .First(),
        };
    }

    private static ControllerDevice? SelectActive(List<ControllerDevice> devices, ControllerResolutionOptions options)
    {
        if (options.RepresentationPreference == ControllerRepresentationPreference.Disabled) return null;
        if (!string.IsNullOrWhiteSpace(options.PreferredRuntimeId))
        {
            ControllerDevice? preferred = devices.FirstOrDefault(device => string.Equals(device.RuntimeId, options.PreferredRuntimeId, StringComparison.Ordinal));
            if (preferred is not null) return preferred;
        }

        return options.RepresentationPreference switch
        {
            ControllerRepresentationPreference.PhysicalController => devices.FirstOrDefault(device => !device.IsVirtual),
            ControllerRepresentationPreference.VirtualXboxController => devices.FirstOrDefault(device => device.IsVirtual && device.ControllerFamily == ControllerFamily.Xbox) ?? devices.FirstOrDefault(device => device.IsVirtual),
            _ => devices.Count == 0 ? null : devices[0],
        };
    }

    private static string BuildIdentityKey(ControllerBackendDevice device)
    {
        string vendor = NormalizeHex(device.VendorId);
        string product = NormalizeHex(device.ProductId);
        if (!string.IsNullOrWhiteSpace(vendor) || !string.IsNullOrWhiteSpace(product))
        {
            return $"vid:{vendor};pid:{product}";
        }

        string name = string.IsNullOrWhiteSpace(device.DisplayName) ? "unknown" : device.DisplayName.Trim().ToUpperInvariant();
        return $"name:{name};backend:{device.BackendId.ToUpperInvariant()}";
    }

    private static string FamilyHint(ControllerBackendDevice device)
    {
        string signal = string.Join(' ', device.DisplayName, device.VendorId, device.ProductId, device.BackendId).ToLowerInvariant();
        if (signal.Contains("dualsense") || signal.Contains("dualshock") || signal.Contains("playstation") || signal.Contains("sony") || signal.Contains("054c")) return "playstation";
        if (signal.Contains("xbox") || signal.Contains("xinput") || signal.Contains("045e")) return "xbox";
        if (signal.Contains("nintendo") || signal.Contains("057e")) return "nintendo";
        return "unknown";
    }

    private static bool IsXboxCompatible(ControllerBackendDevice device)
    {
        string signal = string.Join(' ', device.DisplayName, device.VendorId, device.ProductId, device.BackendId).ToLowerInvariant();
        return signal.Contains("xbox") || signal.Contains("xinput") || signal.Contains("045e");
    }

    private static string NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[2..];
        return trimmed.PadLeft(4, '0').ToUpperInvariant();
    }
}
