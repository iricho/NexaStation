using NexaStation.Domain;
using System.Collections.Concurrent;

namespace NexaStation.Core;

public interface ILibraryProvider
{
    string Id { get; }
    string DisplayName { get; }
    Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken = default);
    Task<ProviderScanResult> ScanAsync(ProviderScanRequest request, CancellationToken cancellationToken = default);
    Task<ProviderValidationResult> ValidateAsync(ProviderSource source, CancellationToken cancellationToken = default);
    Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source, CancellationToken cancellationToken = default);
}

public interface IProviderRegistry
{
    IReadOnlyCollection<ILibraryProvider> Providers { get; }
    ILibraryProvider? GetById(string providerId);
}

public interface IProviderImportService
{
    Task<ImportPreview> CreatePreviewAsync(string providerId, ProviderScanResult scanResult, CancellationToken cancellationToken = default);
    Task<ProviderImportResult> ImportAsync(string providerId, IReadOnlyCollection<ImportSelection> selections, CancellationToken cancellationToken = default);
    Task<ProviderRefreshResult> RefreshAsync(string providerId, CancellationToken cancellationToken = default);
}

public interface IProviderPersistence
{
    Task<ProviderSource?> FindSourceAsync(string providerId, string providerItemId, CancellationToken cancellationToken = default);
    Task<LibraryItem?> FindPossibleDuplicateAsync(DiscoveredLibraryItem item, CancellationToken cancellationToken = default);
    Task<ProviderImportResult> ImportAsync(string providerId, IReadOnlyCollection<ImportSelection> selections, CancellationToken cancellationToken = default);
    Task<ProviderRefreshResult> ApplyRefreshAsync(ProviderScanResult scanResult, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderSource>> GetSourcesAsync(string providerId, CancellationToken cancellationToken = default);
    Task AddMetadataOverrideAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default);
    Task RemoveMetadataOverrideAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default);
}

public sealed class ProviderRegistry(IEnumerable<ILibraryProvider> providers) : IProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ILibraryProvider> _providers = providers
        .ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ILibraryProvider> Providers => _providers.Values.ToArray();

    public ILibraryProvider? GetById(string providerId) =>
        _providers.TryGetValue(providerId, out ILibraryProvider? provider) ? provider : null;
}

public sealed class ProviderImportService(IProviderRegistry registry, IProviderPersistence persistence) : IProviderImportService
{
    private readonly ConcurrentDictionary<string,SemaphoreSlim> _refreshGates=new(StringComparer.OrdinalIgnoreCase);
    public async Task<ImportPreview> CreatePreviewAsync(string providerId, ProviderScanResult scanResult, CancellationToken cancellationToken = default)
    {
        EnsureProvider(providerId);
        List<ImportCandidate> candidates = [];
        foreach (DiscoveredLibraryItem item in scanResult.DiscoveredItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProviderSource? source = await persistence.FindSourceAsync(providerId, item.ProviderItemId, cancellationToken).ConfigureAwait(false);
            if (source is not null)
            {
                ImportCandidateStatus status = source.Installed == item.Installed &&
                    string.Equals(source.InstallationPath, item.InstallationPath, StringComparison.OrdinalIgnoreCase)
                    ? ImportCandidateStatus.AlreadyImported
                    : ImportCandidateStatus.Changed;
                candidates.Add(new(item, status, status == ImportCandidateStatus.Changed, source.LibraryItemId));
                continue;
            }

            LibraryItem? duplicate = await persistence.FindPossibleDuplicateAsync(item, cancellationToken).ConfigureAwait(false);
            ImportCandidateStatus candidateStatus = !item.Installed
                ? ImportCandidateStatus.Missing
                : duplicate is null ? ImportCandidateStatus.New : ImportCandidateStatus.PossibleDuplicate;
            candidates.Add(new(item, candidateStatus, candidateStatus == ImportCandidateStatus.New, duplicate?.Id));
        }

        return new(providerId, candidates, scanResult.Warnings, DateTime.UtcNow);
    }

    public Task<ProviderImportResult> ImportAsync(string providerId, IReadOnlyCollection<ImportSelection> selections, CancellationToken cancellationToken = default)
    {
        EnsureProvider(providerId);
        return persistence.ImportAsync(providerId, selections, cancellationToken);
    }

    public async Task<ProviderRefreshResult> RefreshAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ILibraryProvider provider = EnsureProvider(providerId);
        SemaphoreSlim gate=_refreshGates.GetOrAdd(provider.Id,_=>new(1,1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ProviderScanResult scan = await provider.ScanAsync(new ProviderScanRequest(), cancellationToken).ConfigureAwait(false);
            if (scan.WasCancelled)
                return new(providerId, new(0, 0, 0, 0, 0, 0, 0, scan.Warnings.Count, scan.CompletedAtUtc - scan.StartedAtUtc), scan.Errors);
            return await persistence.ApplyRefreshAsync(scan, cancellationToken).ConfigureAwait(false);
        }
        finally{gate.Release();}
    }

    private ILibraryProvider EnsureProvider(string providerId) =>
        registry.GetById(providerId) ?? throw new KeyNotFoundException($"Provider '{providerId}' is not registered.");
}
