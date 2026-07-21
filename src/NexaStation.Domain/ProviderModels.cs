namespace NexaStation.Domain;

public sealed record ProviderAvailabilityResult(
    string ProviderId,
    bool Detected,
    string? InstallationPath,
    string DisplayMessage,
    bool RequiresUserConfiguration,
    IReadOnlyList<string> DiagnosticDetails);

public sealed record ProviderScanRequest(
    bool FullRefresh = false,
    bool IncludeMissingLibraries = true,
    string? ConfiguredInstallationPath = null,
    IReadOnlyCollection<string>? EnabledLibrarySourceIds = null);

public sealed record ProviderScanResult(
    string ProviderId,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string? InstallationPath,
    IReadOnlyList<ProviderLibrarySource> LibrarySources,
    IReadOnlyList<DiscoveredLibraryItem> DiscoveredItems,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool WasCancelled = false);

public sealed record ProviderLibrarySource(
    string Id,
    string ProviderId,
    string Path,
    string NormalizedPath,
    bool Enabled,
    bool Available,
    string? ExternalId = null,
    string? DiagnosticMessage = null);

public sealed record ArtworkCandidate(
    string Path,
    AssetType SuggestedType,
    int Priority = 0,
    int? Width = null,
    int? Height = null);

public sealed record DiscoveredLibraryItem(
    string ProviderId,
    string ProviderItemId,
    string Title,
    string SortTitle,
    ContentType ContentType,
    string? InstallationPath,
    string? ManifestPath,
    Uri? LaunchUri,
    bool Installed,
    DateTime? ProviderUpdatedAtUtc,
    IReadOnlyDictionary<string, string> ProviderData,
    IReadOnlyList<ArtworkCandidate> ArtworkCandidates,
    string? ProviderLibrarySourceId = null);

public sealed record ProviderSource(
    string Id,
    string LibraryItemId,
    string ProviderId,
    string ProviderItemId,
    string? InstallationPath,
    string? ManifestPath,
    bool Installed,
    IReadOnlyDictionary<string, string> ProviderData,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    string? ProviderLibrarySourceId = null);

public enum ImportCandidateStatus { New, AlreadyImported, PossibleDuplicate, Changed, Missing, Invalid }

public sealed record ImportCandidate(
    DiscoveredLibraryItem Item,
    ImportCandidateStatus Status,
    bool SelectedByDefault,
    string? ExistingLibraryItemId = null,
    string? Message = null);

public sealed record ImportPreview(
    string ProviderId,
    IReadOnlyList<ImportCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    DateTime CreatedAtUtc);

public enum ImportSelectionAction { Import, Link, Skip }

public sealed record ImportSelection(
    DiscoveredLibraryItem Item,
    ImportSelectionAction Action = ImportSelectionAction.Import,
    string? ExistingLibraryItemId = null);

public sealed record ProviderRefreshSummary(
    int Discovered,
    int Added,
    int Updated,
    int Unchanged,
    int Missing,
    int Skipped,
    int Failed,
    int Warnings,
    TimeSpan Duration);

public sealed record ProviderImportResult(string ProviderId, ProviderRefreshSummary Summary, IReadOnlyList<string> Errors);
public sealed record ProviderRefreshResult(string ProviderId, ProviderRefreshSummary Summary, IReadOnlyList<string> Errors);
public sealed record ProviderValidationResult(bool IsValid, string Message, IReadOnlyList<string> Diagnostics);
public sealed record ResolvedLaunchTarget(LaunchTarget LaunchTarget, ProviderSource? Source = null);

public enum ProviderScanStatus { Running, Succeeded, Failed, Cancelled }

public sealed record ProviderScanHistoryEntry(
    string Id,
    string ProviderId,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    ProviderScanStatus Status,
    ProviderRefreshSummary Summary,
    string? ErrorSummary = null);

public sealed record MetadataOverride(string LibraryItemId, string FieldName, DateTime UpdatedAtUtc);
