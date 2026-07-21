using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.EmulatorDiscovery.Definitions;
using NexaStation.EmulatorDiscovery.Discovery;
using NexaStation.EmulatorDiscovery.Sources;
using Xunit;

namespace NexaStation.EmulatorDiscovery.Tests;

public sealed class EmulatorDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexa-emulators-{Guid.NewGuid():N}");
    public EmulatorDiscoveryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Fact] public async Task Portable_scan_is_limited_to_approved_root()
    {
        string path = Path.Combine(_root, "Dolphin.exe"); File.WriteAllBytes(path, [0]); BoundedFolderEmulatorSource source = new();
        IReadOnlyList<EmulatorSourceCandidate> found = await source.DiscoverAsync(new() { IncludePortableApplications = true, ApprovedFolders = [_root] }, BuiltInEmulatorDefinitions.All, default);
        Assert.Single(found); Assert.Equal(path, found[0].ExecutablePath);
        Assert.Empty(await source.DiscoverAsync(new() { IncludePortableApplications = true, ApprovedFolders = [] }, BuiltInEmulatorDefinitions.All, default));
    }

    [Fact] public async Task Filename_only_candidate_requires_review()
    {
        string path = Path.Combine(_root, "Dolphin.exe"); File.WriteAllBytes(path, [0]); EmulatorDiscoveryService service = new(new FakePersistence(), new FakeRegistry(), [new FixtureSource(path)]);
        EmulatorDiscoveryResult result = await service.DiscoverAsync(new() { DetectionSources = EmulatorDetectionSources.ApprovedPortableFolders });
        Assert.Single(result.Candidates); Assert.Equal(EmulatorCandidateStatus.ReviewRequired, result.Candidates[0].Status);
    }

    [Fact] public async Task Installer_is_rejected()
    {
        string path = Path.Combine(_root, "dolphin-setup.exe"); File.WriteAllBytes(path, [0]); EmulatorDiscoveryService service = new(new FakePersistence(), new FakeRegistry(), []);
        EmulatorCandidate candidate = new("id", "dolphin", "Dolphin", path, Path.GetFullPath(path).ToUpperInvariant(), null, EmulatorCandidateSourceType.UserSelectedExecutable, .5, EmulatorCandidateStatus.ReviewRequired, null, []);
        NexaStation.EmulatorDiscovery.Discovery.EmulatorValidationResult validation = await service.ValidateAsync(candidate); Assert.False(validation.IsValid); Assert.Equal(EmulatorCandidateStatus.InvalidInstallation, validation.Status);
    }

    [Fact] public async Task Existing_candidate_is_detected_as_duplicate()
    {
        string path = Path.Combine(_root, "Dolphin.exe"); File.WriteAllBytes(path, [0]); FakePersistence persistence = new() { Installations = [new() { Id = "existing", AdapterId = "dolphin", DisplayName = "Dolphin", ExecutablePath = path, InstallationDirectory = _root, AddedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }] };
        EmulatorDiscoveryService service = new(persistence, new FakeRegistry(), []); EmulatorDiscoveryResult result = await service.DiscoverAsync(new() { DetectionSources = EmulatorDetectionSources.ExistingRecords }); Assert.Equal("existing", Assert.Single(result.Candidates).ExistingEmulatorId);
    }

    private sealed class FixtureSource(string path) : IEmulatorDiscoverySource { public EmulatorCandidateSourceType SourceType => EmulatorCandidateSourceType.ApprovedPortableFolder; public Task<IReadOnlyList<EmulatorSourceCandidate>> DiscoverAsync(EmulatorDiscoveryRequest request, IReadOnlyList<EmulatorDefinition> definitions, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmulatorSourceCandidate>>([new(path, SourceType, UserApproved: true)]); }
    private sealed class FakeRegistry : IEmulatorAdapterRegistry { public IReadOnlyCollection<IEmulatorAdapter> Adapters => [new Adapter()]; public IEmulatorAdapter? GetById(string adapterId) => adapterId == "dolphin" ? Adapters.Single() : null; }
    private sealed class Adapter : IEmulatorAdapter { public string Id => "dolphin"; public string DisplayName => "Dolphin"; public IReadOnlyCollection<string> KnownExecutableNames => ["Dolphin.exe"]; public IReadOnlyCollection<string> SupportedPlatformIds => ["gamecube"]; public Task<EmulatorLaunchDefinition> BuildLaunchDefinitionAsync(EmulatorLaunchRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<GameIdentificationResult> IdentifyGameAsync(GameIdentificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<EmulatorProbeResult> ProbeAsync(string executablePath, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<NexaStation.Domain.EmulatorValidationResult> ValidateInstallationAsync(EmulatorInstallation installation, CancellationToken cancellationToken = default) => throw new NotSupportedException(); }
    private sealed class FakePersistence : IEmulationPersistence
    {
        public IReadOnlyList<EmulatorInstallation> Installations { get; init; } = []; public Task<IReadOnlyList<EmulatorInstallation>> GetInstallationsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Installations); public Task<EmulatorInstallation?> GetInstallationAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(Installations.FirstOrDefault(value => value.Id == id)); public Task<IReadOnlyList<EmulatorPlatform>> GetPlatformsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task SaveInstallationAsync(EmulatorInstallation installation, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task RelinkInstallationAsync(string id, string executablePath, EmulatorProbeResult probe, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task SetInstallationEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<IReadOnlyList<GameSource>> GetGameSourcesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task SaveGameSourceAsync(GameSource source, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<EmulatedGameSource?> GetEmulatedGameAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<IReadOnlyList<EmulatedGameSource>> GetEmulatedGamesByLibraryItemIdsAsync(IReadOnlyCollection<string> libraryItemIds, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<bool> ApplyIdentificationRepairAsync(string libraryItemId, GameIdentificationResult identification, string originalFilenameTitle, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<EmulatedGameImportResult> ImportAsync(GameSource source, IReadOnlyCollection<EmulatedGameImportSelection> selections, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task RelinkGameAsync(string id, string gamePath, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task SetPreferredEmulatorAsync(string id, string? emulatorInstallationId, string? retroArchCoreId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<IReadOnlyList<RetroArchCore>> GetRetroArchCoresAsync(string emulatorInstallationId, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task SaveRetroArchCoreAsync(RetroArchCore core, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<EmulatedGameImportResult> ApplyRefreshAsync(GameSourceScanResult scan, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<EmulatorLaunchProfile?> GetLaunchProfileAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException(); public Task<RetroArchCore?> GetRetroArchCoreAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
