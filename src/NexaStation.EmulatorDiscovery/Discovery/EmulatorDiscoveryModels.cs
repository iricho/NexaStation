using NexaStation.EmulatorDiscovery.Definitions;

namespace NexaStation.EmulatorDiscovery.Discovery;

[Flags] public enum EmulatorDetectionSources { None = 0, ExistingRecords = 1, InstalledApplications = 2, StartMenu = 4, Registry = 8, PackagedApplications = 16, KnownPaths = 32, ApprovedPortableFolders = 64, UserSelectedExecutable = 128 }
public enum EmulatorCandidateSourceType { ExistingRecord, InstalledApplication, StartMenu, Registry, PackagedApplication, KnownPath, ApprovedPortableFolder, UserSelectedExecutable }
public enum EmulatorCandidateStatus { DetectedAndSupported, DetectedNeedsSetup, DetectedWithGenericAdapter, DetectedUnsupported, Missing, InvalidInstallation, ReviewRequired }
public enum EmulatorDiscoveryRefreshMode { Cached, Refresh }

public sealed record EmulatorDiscoveryRequest
{
    public EmulatorDetectionSources DetectionSources { get; init; } = EmulatorDetectionSources.ExistingRecords | EmulatorDetectionSources.StartMenu | EmulatorDetectionSources.Registry | EmulatorDetectionSources.KnownPaths;
    public IReadOnlyList<EmulatorDefinition> KnownDefinitions { get; init; } = BuiltInEmulatorDefinitions.All;
    public IReadOnlyList<string> ApprovedFolders { get; init; } = [];
    public bool IncludePortableApplications { get; init; }
    public bool IncludeDisabledDefinitions { get; init; }
    public EmulatorDiscoveryRefreshMode RefreshMode { get; init; } = EmulatorDiscoveryRefreshMode.Cached;
    public string? UserSelectedExecutable { get; init; }
    public int MaximumFilesInspected { get; init; } = 10_000;
    public int MaximumRecursionDepth { get; init; } = 4;
}

public sealed record EmulatorCandidate(string Id, string DefinitionId, string DisplayName, string ExecutablePath, string NormalizedExecutablePath, string? Version, EmulatorCandidateSourceType DetectionSource, double Confidence, EmulatorCandidateStatus Status, string? ExistingEmulatorId, IReadOnlyList<string> Evidence, bool IsPortable = false);
public sealed record EmulatorValidationResult(bool IsValid, EmulatorCandidateStatus Status, double Confidence, string Message, IReadOnlyList<string> Evidence, string? Version = null, string? ExistingEmulatorId = null);
public sealed record EmulatorDiscoveryResult(IReadOnlyList<EmulatorCandidate> Candidates, int FilesInspected, TimeSpan Duration, bool WasCancelled, IReadOnlyList<string> Warnings);

public interface IEmulatorDiscoveryService
{
    Task<EmulatorDiscoveryResult> DiscoverAsync(EmulatorDiscoveryRequest request, CancellationToken cancellationToken = default);
    Task<EmulatorValidationResult> ValidateAsync(EmulatorCandidate candidate, CancellationToken cancellationToken = default);
}

public sealed record EmulatorSourceCandidate(string ExecutablePath, EmulatorCandidateSourceType SourceType, string? ExistingEmulatorId = null, string? DefinitionHint = null, bool UserApproved = false);
public interface IEmulatorDiscoverySource
{
    EmulatorCandidateSourceType SourceType { get; }
    Task<IReadOnlyList<EmulatorSourceCandidate>> DiscoverAsync(EmulatorDiscoveryRequest request, IReadOnlyList<EmulatorDefinition> definitions, CancellationToken cancellationToken);
}
