using System.Globalization;
using System.Text.Json;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;

namespace NexaStation.Compatibility.Validation;

public sealed record CompatibilityValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static CompatibilityValidationResult Valid() => new(true, []);
}

public interface ICompatibilityProfileValidator
{
    CompatibilityValidationResult Validate(CompatibilityProfile profile);
    CompatibilityValidationResult ValidateFeedJson(JsonElement root);
}

public sealed class CompatibilityProfileValidator : ICompatibilityProfileValidator
{
    private static readonly HashSet<string> RejectedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "command", "commands", "script", "powershell", "registry", "delete", "move", "killProcess",
        "processKill", "download", "executable", "dll", "driver", "administrator", "environmentVariable",
    };

    public CompatibilityValidationResult Validate(CompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(profile.Id)) errors.Add("Profile ID is required.");
        if (profile.ProfileVersion <= 0) errors.Add("Profile version must be positive.");
        if (!HasStableIdentity(profile)) errors.Add("A stable provider, executable, AUMID, adapter, controller, display, or provider-wide identity is required; display-name matching is forbidden.");
        ValidateConstraint(profile.VersionConstraints.Application, "application", errors);
        ValidateConstraint(profile.VersionConstraints.Windows, "Windows", errors);
        ValidateConstraint(profile.VersionConstraints.Adapter, "adapter", errors);
        if (profile.Rules.Launch.StartupTimeoutSeconds is < 1 or > 600) errors.Add("Startup timeout must be between 1 and 600 seconds.");
        if (profile.Rules.Tracking.DetectionTimeoutSeconds is < 1 or > 600) errors.Add("Detection timeout must be between 1 and 600 seconds.");
        if (profile.Rules.Tracking.ExitGracePeriodSeconds is < 0 or > 120) errors.Add("Exit grace period must be between 0 and 120 seconds.");
        if (profile.Rules.Input.NavigationDeadzone is < 0 or > 0.95) errors.Add("Navigation deadzone must be between 0 and 0.95.");
        if (profile.Rules.Input.RepeatDelayMilliseconds is < 100 or > 5000) errors.Add("Repeat delay must be between 100 and 5000 milliseconds.");
        if (profile.Rules.Input.RepeatRateMilliseconds is < 25 or > 2000) errors.Add("Repeat rate must be between 25 and 2000 milliseconds.");
        if (profile.Rules.Performance.ProviderRefreshDelaySeconds is < 0 or > 3600) errors.Add("Provider refresh delay must be between 0 and 3600 seconds.");
        return new(errors.Count == 0, errors);
    }

    public CompatibilityValidationResult ValidateFeedJson(JsonElement root)
    {
        List<string> errors = [];
        Walk(root, "$", errors);
        return new(errors.Count == 0, errors);
    }

    private static bool HasStableIdentity(CompatibilityProfile profile)
    {
        CompatibilityTargetMatcher matcher = profile.TargetMatcher;
        return !string.IsNullOrWhiteSpace(matcher.ProviderItemId)
            || !string.IsNullOrWhiteSpace(matcher.ExecutableIdentity)
            || !string.IsNullOrWhiteSpace(matcher.AppUserModelId)
            || !string.IsNullOrWhiteSpace(matcher.PlatformId) && !string.IsNullOrWhiteSpace(matcher.EmulatorAdapterId)
            || !string.IsNullOrWhiteSpace(matcher.ControllerFamily)
            || !string.IsNullOrWhiteSpace(matcher.DisplayIdentity)
            || !string.IsNullOrWhiteSpace(matcher.ProviderId) && matcher.TargetType == Models.CompatibilityTargetType.Provider;
    }

    private static void ValidateConstraint(string? constraint, string label, List<string> errors)
    {
        if (!VersionConstraint.TryParse(constraint, out _)) errors.Add($"The {label} version constraint is invalid.");
    }

    private static void Walk(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (RejectedProperties.Contains(property.Name)) errors.Add($"Unsafe or unknown executable rule '{path}.{property.Name}' is rejected.");
                Walk(property.Value, $"{path}.{property.Name}", errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement child in element.EnumerateArray()) Walk(child, $"{path}[{index++}]", errors);
        }
    }
}

public sealed class VersionConstraint
{
    private static readonly string[] Operators = [">=", "<=", ">", "<", "="];
    private readonly IReadOnlyList<(string Operator, Version Version)> _parts;
    private VersionConstraint(IReadOnlyList<(string Operator, Version Version)> parts) => _parts = parts;

    public static bool TryParse(string? text, out VersionConstraint constraint)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() == "*")
        {
            constraint = new([]);
            return true;
        }

        List<(string, Version)> parts = [];
        foreach (string raw in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string op = Operators.FirstOrDefault(value => raw.StartsWith(value, StringComparison.Ordinal), "=");
            string versionText = op == "=" && !raw.StartsWith('=') ? raw : raw[op.Length..];
            if (!Version.TryParse(versionText.Trim(), out Version? version))
            {
                constraint = new([]);
                return false;
            }
            parts.Add((op, version));
        }
        constraint = new(parts);
        return parts.Count > 0;
    }

    public bool Matches(string? value)
    {
        if (_parts.Count == 0) return true;
        if (!Version.TryParse(value, out Version? version)) return false;
        return _parts.All(part => part.Operator switch
        {
            ">=" => version >= part.Version,
            "<=" => version <= part.Version,
            ">" => version > part.Version,
            "<" => version < part.Version,
            _ => version == part.Version,
        });
    }
}
