using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Rules;

namespace NexaStation.Compatibility.Matching;

public sealed record ControllerCompatibilityIdentity(
    string ControllerFamily,
    string? VendorId = null,
    string? ProductId = null,
    string? ConnectionType = null,
    string? InputBackend = null);

public static class ControllerCompatibilityPolicy
{
    public static Task<CompatibilityResolutionResult> ResolveAsync(
        ICompatibilityProfileResolver resolver,
        ControllerCompatibilityIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(identity);
        if (string.IsNullOrWhiteSpace(identity.ControllerFamily)) throw new ArgumentException("A generalized controller family is required.", nameof(identity));
        return resolver.ResolveAsync(new(CompatibilityTargetType.Controller, ControllerFamily: identity.ControllerFamily), cancellationToken);
    }

    public static InputCompatibilityOverrides EffectiveInput(CompatibilityResolutionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return resolution.Rules.Input;
    }
}
