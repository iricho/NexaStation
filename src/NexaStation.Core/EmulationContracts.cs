using NexaStation.Domain;

namespace NexaStation.Core;

public interface IEmulatorAdapter
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyCollection<string> KnownExecutableNames { get; }
    IReadOnlyCollection<string> SupportedPlatformIds { get; }
    Task<EmulatorProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default);
    Task<EmulatorValidationResult> ValidateInstallationAsync(EmulatorInstallation installation, CancellationToken cancellationToken = default);
    Task<EmulatorLaunchDefinition> BuildLaunchDefinitionAsync(EmulatorLaunchRequest request, CancellationToken cancellationToken = default);
    Task<GameIdentificationResult> IdentifyGameAsync(GameIdentificationRequest request, CancellationToken cancellationToken = default);
}

public interface IEmulatorAdapterRegistry
{
    IReadOnlyCollection<IEmulatorAdapter> Adapters { get; }
    IEmulatorAdapter? GetById(string adapterId);
}

public interface IEmulatorCapabilityProbe
{
    Task<EmulatorProbeResult> ProbeAsync(string executablePath, EmulatorProbeOptions options, CancellationToken cancellationToken = default);
}

public interface IGameSourceScanner
{
    Task<GameSourceScanResult> ScanAsync(GameSource source, IProgress<GameSourceScanProgress>? progress = null, CancellationToken cancellationToken = default);
}

public interface IGameIdentifierReader
{
    bool CanIdentify(string platformId, string fileExtension);
    Task<GameIdentificationResult> IdentifyAsync(GameIdentificationRequest request, CancellationToken cancellationToken = default);
}

public interface IRetroArchCoreDiscovery
{
    Task<IReadOnlyList<RetroArchCore>> DiscoverAsync(EmulatorInstallation installation, string coreFolder, CancellationToken cancellationToken = default);
}

public interface IEmulatorLaunchResolver
{
    Task<ResolvedLaunchTarget> ResolveAsync(EmulatedGameLaunchRequest request, CancellationToken cancellationToken = default);
}

public interface IEmulationPersistence
{
    Task<IReadOnlyList<EmulatorPlatform>> GetPlatformsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmulatorInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default);
    Task<EmulatorInstallation?> GetInstallationAsync(string id, CancellationToken cancellationToken = default);
    Task SaveInstallationAsync(EmulatorInstallation installation, CancellationToken cancellationToken = default);
    Task RelinkInstallationAsync(string id, string executablePath, EmulatorProbeResult probe, CancellationToken cancellationToken = default);
    Task SetInstallationEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameSource>> GetGameSourcesAsync(CancellationToken cancellationToken = default);
    Task SaveGameSourceAsync(GameSource source, CancellationToken cancellationToken = default);
    Task<EmulatedGameSource?> GetEmulatedGameAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmulatedGameSource>> GetEmulatedGamesByLibraryItemIdsAsync(IReadOnlyCollection<string> libraryItemIds, CancellationToken cancellationToken = default);
    Task<bool> ApplyIdentificationRepairAsync(string libraryItemId, GameIdentificationResult identification, string originalFilenameTitle, CancellationToken cancellationToken = default);
    Task<EmulatedGameImportResult> ImportAsync(GameSource source, IReadOnlyCollection<EmulatedGameImportSelection> selections, CancellationToken cancellationToken = default);
    Task RelinkGameAsync(string id, string gamePath, CancellationToken cancellationToken = default);
    Task SetPreferredEmulatorAsync(string id, string? emulatorInstallationId, string? retroArchCoreId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RetroArchCore>> GetRetroArchCoresAsync(string emulatorInstallationId, CancellationToken cancellationToken = default);
    Task SaveRetroArchCoreAsync(RetroArchCore core, CancellationToken cancellationToken = default);
    Task<EmulatedGameImportResult> ApplyRefreshAsync(GameSourceScanResult scan, CancellationToken cancellationToken = default);
    Task<EmulatorLaunchProfile?> GetLaunchProfileAsync(string id, CancellationToken cancellationToken = default);
    Task<RetroArchCore?> GetRetroArchCoreAsync(string id, CancellationToken cancellationToken = default);
}

public interface IEmulationLibraryService
{
    Task<EmulatorInstallation> AddInstallationAsync(string adapterId, string executablePath, string displayName, bool portable, CancellationToken cancellationToken = default);
    Task<GameSourceScanResult> ScanAsync(GameSource source, IProgress<GameSourceScanProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<EmulatedGameImportResult> ImportAsync(GameSource source, IReadOnlyCollection<EmulatedGameImportSelection> selections, CancellationToken cancellationToken = default);
    Task<EmulatedIdentityRepairResult> RepairIdentitiesAsync(IReadOnlyCollection<string> libraryItemIds, CancellationToken cancellationToken = default);
}
