using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Identification;
using NexaStation.Providers.Emulation.Launching;

namespace NexaStation.Providers.Emulation.Adapters;

public abstract class EmulatorAdapterBase(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : IEmulatorAdapter
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyCollection<string> KnownExecutableNames { get; }
    public abstract IReadOnlyCollection<string> SupportedPlatformIds { get; }
    protected abstract string DefaultTemplate { get; }
    protected abstract EmulatorCapability DefaultCapabilities { get; }
    protected virtual IReadOnlyList<string> ProbeArguments => ["--version", "--help"];
    protected virtual bool RequiresConfirmedTemplate => false;

    public async Task<EmulatorProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        EmulatorProbeResult result = await probe.ProbeAsync(executablePath, new(ProbeArguments, TimeSpan.FromSeconds(3)), cancellationToken).ConfigureAwait(false);
        return result with { Capabilities = result.Capabilities | (result.Succeeded ? DefaultCapabilities : EmulatorCapability.None), SupportedPlatformIds = SupportedPlatformIds.ToArray() };
    }

    public async Task<EmulatorValidationResult> ValidateInstallationAsync(EmulatorInstallation installation, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installation.ExecutablePath)) return new(false, EmulatorValidationStatus.Missing, "The configured emulator executable is missing.", []);
        if (!string.Equals(installation.AdapterId, Id, StringComparison.OrdinalIgnoreCase)) return new(false, EmulatorValidationStatus.Invalid, "The emulator adapter does not match this installation.", []);
        EmulatorProbeResult result = await ProbeAsync(installation.ExecutablePath, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && string.IsNullOrWhiteSpace(installation.ArgumentsOverride)) return new(false, EmulatorValidationStatus.ProbeFailed, "Safe capability probing failed. Manual launch arguments may still be configured.", result.Warnings);
        return new(true, result.Succeeded ? EmulatorValidationStatus.Valid : EmulatorValidationStatus.ProbeFailed, result.Succeeded ? "The emulator installation is valid." : "Using manual configuration after probe failure.", result.Warnings);
    }

    public virtual Task<EmulatorLaunchDefinition> BuildLaunchDefinitionAsync(EmulatorLaunchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(request.Installation.ExecutablePath)) throw new FileNotFoundException("The configured emulator could not be found.", request.Installation.ExecutablePath);
        if (!File.Exists(request.Game.GamePath) && !Directory.Exists(request.Game.GamePath)) throw new FileNotFoundException("The game file could not be located.", request.Game.GamePath);
        string template = request.Profile?.ArgumentsTemplate ?? request.Installation.ArgumentsOverride ?? DefaultTemplate;
        if (RequiresConfirmedTemplate && request.Profile is null && string.IsNullOrWhiteSpace(request.Installation.ArgumentsOverride))
            throw new InvalidOperationException("This experimental adapter requires a user-confirmed launch template.");
        IReadOnlyList<string> arguments = LaunchTemplateRenderer.RenderArguments(template, request);
        string expected = request.Profile?.ExpectedProcessName ?? Path.GetFileNameWithoutExtension(request.Installation.ExecutablePath);
        return Task.FromResult(new EmulatorLaunchDefinition(
            request.Installation.ExecutablePath,
            arguments,
            request.Profile?.WorkingDirectory ?? request.Installation.InstallationDirectory,
            request.Profile?.AdditionalEnvironment ?? new Dictionary<string, string>(),
            expected,
            request.Profile?.ProcessDetectionMode ?? EmulatorProcessDetectionMode.LaunchedProcess,
            []));
    }

    public Task<GameIdentificationResult> IdentifyGameAsync(GameIdentificationRequest request, CancellationToken cancellationToken = default) =>
        identifier.IdentifyAsync(request, cancellationToken);
}
