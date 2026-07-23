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

public interface IAutomaticImportArtworkService
{
    Task QueueMissingArtworkAsync(string providerId, IReadOnlyCollection<string> libraryItemIds, CancellationToken cancellationToken = default);
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

public sealed class ProviderImportService(
    IProviderRegistry registry,
    IProviderPersistence persistence,
    IAutomaticImportArtworkService? automaticArtwork = null,
    IAppLogger? logger = null) : IProviderImportService
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

    public async Task<ProviderImportResult> ImportAsync(string providerId, IReadOnlyCollection<ImportSelection> selections, CancellationToken cancellationToken = default)
    {
        EnsureProvider(providerId);
        ProviderImportResult result = await persistence.ImportAsync(providerId, selections, cancellationToken).ConfigureAwait(false);
        if (SupportsAutomaticArtwork(providerId))
        {
            List<string> importedIds = [];
            foreach (ImportSelection selection in selections.Where(value => value.Action != ImportSelectionAction.Skip))
            {
                ProviderSource? source = await persistence.FindSourceAsync(providerId, selection.Item.ProviderItemId, cancellationToken).ConfigureAwait(false);
                if (source is not null) importedIds.Add(source.LibraryItemId);
            }
            await TryQueueArtworkAsync(providerId, importedIds, cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    public async Task<ProviderRefreshResult> RefreshAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ILibraryProvider provider = EnsureProvider(providerId);
        SemaphoreSlim gate=_refreshGates.GetOrAdd(provider.Id,_=>new(1,1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            HashSet<string> existingIds = SupportsAutomaticArtwork(providerId)
                ? (await persistence.GetSourcesAsync(providerId, cancellationToken).ConfigureAwait(false)).Select(value => value.LibraryItemId).ToHashSet(StringComparer.Ordinal)
                : [];
            ProviderScanResult scan = await provider.ScanAsync(new ProviderScanRequest(), cancellationToken).ConfigureAwait(false);
            if (scan.WasCancelled)
                return new(providerId, new(0, 0, 0, 0, 0, 0, 0, scan.Warnings.Count, scan.CompletedAtUtc - scan.StartedAtUtc), scan.Errors);
            ProviderRefreshResult result = await persistence.ApplyRefreshAsync(scan, cancellationToken).ConfigureAwait(false);
            if (SupportsAutomaticArtwork(providerId))
            {
                string[] addedIds = (await persistence.GetSourcesAsync(providerId, cancellationToken).ConfigureAwait(false))
                    .Select(value => value.LibraryItemId)
                    .Where(value => !existingIds.Contains(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                await TryQueueArtworkAsync(providerId, addedIds, cancellationToken).ConfigureAwait(false);
            }
            return result;
        }
        finally{gate.Release();}
    }

    private async Task TryQueueArtworkAsync(string providerId, IReadOnlyCollection<string> libraryItemIds, CancellationToken cancellationToken)
    {
        if (automaticArtwork is null || libraryItemIds.Count == 0) return;
        try
        {
            await automaticArtwork.QueueMissingArtworkAsync(providerId, libraryItemIds.Distinct(StringComparer.Ordinal).ToArray(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger?.LogError("AutomaticImportArtworkFailed", "The game import succeeded, but automatic artwork could not be queued.", exception,
                new Dictionary<string, object?> { ["ProviderId"] = providerId, ["ItemCount"] = libraryItemIds.Count });
        }
    }

    private static bool SupportsAutomaticArtwork(string providerId) =>
        string.Equals(providerId, "epic", StringComparison.OrdinalIgnoreCase)
        || string.Equals(providerId, "gog", StringComparison.OrdinalIgnoreCase);

    private ILibraryProvider EnsureProvider(string providerId) =>
        registry.GetById(providerId) ?? throw new KeyNotFoundException($"Provider '{providerId}' is not registered.");
}
