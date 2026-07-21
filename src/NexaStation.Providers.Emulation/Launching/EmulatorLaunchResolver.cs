using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Launching;

public sealed class EmulatorLaunchResolver(IEmulationPersistence persistence, IEmulatorAdapterRegistry adapters) : IEmulatorLaunchResolver
{
    public async Task<ResolvedLaunchTarget> ResolveAsync(EmulatedGameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        EmulatedGameSource game = await persistence.GetEmulatedGameAsync(request.EmulatedGameSourceId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The emulated game source was not found.");
        if (!game.Available || (!File.Exists(game.GamePath) && !Directory.Exists(game.GamePath))) throw new FileNotFoundException("The game file could not be located.", game.GamePath);

        EmulatorPlatform platform = (await persistence.GetPlatformsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(value => string.Equals(value.Id, game.PlatformId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The game's platform definition is unavailable.");
        IReadOnlyList<EmulatorInstallation> installations = await persistence.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
        string? preferredId = request.EmulatorInstallationId ?? game.PreferredEmulatorInstallationId;
        EmulatorInstallation? installation = installations.FirstOrDefault(value => string.Equals(value.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        if (installation is not null && !IsAvailable(installation))
        {
            if (!request.FallbackConfirmed) throw new InvalidOperationException($"The preferred emulator '{installation.DisplayName}' is unavailable. Confirm a fallback before launching.");
            installation = null;
        }

        if (installation is null && preferredId is not null && !request.FallbackConfirmed)
            throw new InvalidOperationException("The preferred emulator is unavailable. Confirm a fallback before launching.");

        if (installation is null)
        {
            GameSource? source = (await persistence.GetGameSourcesAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(value => value.Id == game.GameSourceId);
            installation = installations.FirstOrDefault(value => value.Id == source?.AdapterPreferenceId && IsAvailable(value))
                ?? installations.FirstOrDefault(value => platform.DefaultAdapterIds.Contains(value.AdapterId, StringComparer.OrdinalIgnoreCase) && IsAvailable(value));
        }
        installation ??= installations.FirstOrDefault(value => IsAvailable(value) && adapters.GetById(value.AdapterId)?.SupportedPlatformIds.Contains(platform.Id, StringComparer.OrdinalIgnoreCase) == true);
        if (installation is null) throw new InvalidOperationException("No valid compatible emulator is configured for this game.");

        IEmulatorAdapter adapter = adapters.GetById(installation.AdapterId) ?? throw new InvalidOperationException($"Emulator adapter '{installation.AdapterId}' is not registered.");
        if (!adapter.SupportedPlatformIds.Contains(platform.Id, StringComparer.OrdinalIgnoreCase) && !string.Equals(adapter.Id, "custom", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{installation.DisplayName} does not support {platform.Name}.");
        EmulatorLaunchProfile? profile = request.LaunchProfileId is not null ? await persistence.GetLaunchProfileAsync(request.LaunchProfileId, cancellationToken).ConfigureAwait(false) : game.LaunchProfileId is null ? null : await persistence.GetLaunchProfileAsync(game.LaunchProfileId, cancellationToken).ConfigureAwait(false);
        RetroArchCore? core = game.RetroArchCoreId is null ? null : await persistence.GetRetroArchCoreAsync(game.RetroArchCoreId, cancellationToken).ConfigureAwait(false);
        EmulatorLaunchDefinition definition = await adapter.BuildLaunchDefinitionAsync(new(installation, platform, game, profile, core), cancellationToken).ConfigureAwait(false);
        LaunchTarget target = new(LaunchType.Executable, definition.Executable, definition.WorkingDirectory, ExpectedProcessName: definition.ExpectedProcess, HideFrontendWhileRunning: true, RestoreFrontendAfterExit: true)
        {
            Id = Guid.NewGuid().ToString("N"),
            LibraryItemId = game.LibraryItemId,
            Name = installation.DisplayName,
            ProviderId = "emulation",
            UserManaged = profile?.UserManaged == true,
            ArgumentList = definition.ArgumentList,
            EnvironmentOverrides = definition.EnvironmentOverrides,
            EmulatedGameSourceId = game.Id,
            EmulatorInstallationId = installation.Id,
            EmulatorAdapterId = installation.AdapterId,
            EmulatorVersion = installation.Version,
            EmulatedPlatformId = platform.Id,
            LaunchProfileId = profile?.Id,
        };
        return new(target);
    }

    private static bool IsAvailable(EmulatorInstallation installation) => installation.Enabled && (installation.ValidationStatus is EmulatorValidationStatus.Valid or EmulatorValidationStatus.ProbeFailed) && File.Exists(installation.ExecutablePath);
}
