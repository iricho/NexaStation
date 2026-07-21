using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Artwork;

namespace NexaStation.Providers.Emulation;

public sealed class EmulationLibraryService(IEmulatorAdapterRegistry adapters, IEmulationPersistence persistence, IGameSourceScanner scanner,IGameIdentifierReader identifier) : IEmulationLibraryService
{
    public async Task<EmulatorInstallation> AddInstallationAsync(string adapterId, string executablePath, string displayName, bool portable, CancellationToken cancellationToken = default)
    {
        IEmulatorAdapter adapter = adapters.GetById(adapterId) ?? throw new KeyNotFoundException($"Emulator adapter '{adapterId}' is not registered.");
        string path = Path.GetFullPath(executablePath);
        if (!File.Exists(path)) throw new FileNotFoundException("The selected emulator executable does not exist.", path);
        EmulatorProbeResult probe = await adapter.ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        DateTime now = DateTime.UtcNow;
        EmulatorInstallation installation = new()
        {
            Id = Guid.NewGuid().ToString("N"), AdapterId = adapter.Id, DisplayName = string.IsNullOrWhiteSpace(displayName) ? adapter.DisplayName : displayName.Trim(),
            ExecutablePath = path, InstallationDirectory = Path.GetDirectoryName(path)!, Version = probe.Version,
            Architecture = Environment.Is64BitOperatingSystem ? "x64/unknown" : "x86/unknown", Enabled = true, IsPortable = portable,
            DetectedCapabilities = probe.Capabilities, ValidationStatus = probe.Succeeded ? EmulatorValidationStatus.Valid : EmulatorValidationStatus.ProbeFailed,
            AddedAtUtc = now, UpdatedAtUtc = now, LastValidatedAtUtc = now,
        };
        await persistence.SaveInstallationAsync(installation, cancellationToken).ConfigureAwait(false);
        return installation;
    }

    public async Task<GameSourceScanResult> ScanAsync(GameSource source, IProgress<GameSourceScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        GameSourceScanResult result = await scanner.ScanAsync(source, progress, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EmulatorInstallation> installations = await persistence.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
        EmulatorPlatform? platform = (await persistence.GetPlatformsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(value => value.Id == source.PlatformId);
        bool hasEmulator = installations.Any(value => value.Enabled && File.Exists(value.ExecutablePath) && (value.Id == source.AdapterPreferenceId || platform?.DefaultAdapterIds.Contains(value.AdapterId, StringComparer.OrdinalIgnoreCase) == true));
        DiscoveredGame[] games = result.Games.Select(game => game with
        {
            Status = hasEmulator ? EmulatedImportStatus.New : EmulatedImportStatus.MissingEmulator,
            PreferredEmulatorInstallationId = source.AdapterPreferenceId,
            ArtworkCandidates = LocalGameArtworkDiscovery.Find(game.GamePath),
        }).ToArray();
        Dictionary<(string Platform, string Title), int> groups = games.GroupBy(game => (game.PlatformId, game.SuggestedTitle), new PlatformTitleComparer()).Where(group => group.Count() > 1 && group.Any(game => game.Identification.DiscNumber is not null)).ToDictionary(group => group.Key, group => group.Count(), new PlatformTitleComparer());
        games = games.Select(game => groups.ContainsKey((game.PlatformId, game.SuggestedTitle)) ? game with { Status = EmulatedImportStatus.PossibleMultiDiscGame } : game).ToArray();
        return result with { Games = games };
    }

    public Task<EmulatedGameImportResult> ImportAsync(GameSource source, IReadOnlyCollection<EmulatedGameImportSelection> selections, CancellationToken cancellationToken = default) => persistence.ImportAsync(source, selections, cancellationToken);

    public async Task<EmulatedIdentityRepairResult> RepairIdentitiesAsync(IReadOnlyCollection<string> libraryItemIds,CancellationToken cancellationToken=default)
    {
        IReadOnlyList<EmulatedGameSource> games=await persistence.GetEmulatedGamesByLibraryItemIdsAsync(libraryItemIds,cancellationToken).ConfigureAwait(false);int updated=0,cleaned=0,failed=0;List<string> errors=[];
        foreach(EmulatedGameSource game in games){cancellationToken.ThrowIfCancellationRequested();try{string extension=game.FileExtension??Path.GetExtension(game.GamePath);if(!identifier.CanIdentify(game.PlatformId,extension))continue;GameIdentificationResult result=await identifier.IdentifyAsync(new(game.PlatformId,game.GamePath,extension,1_048_576),cancellationToken).ConfigureAwait(false);string original=Path.GetFileNameWithoutExtension(game.GamePath);bool titleChanged=await persistence.ApplyIdentificationRepairAsync(game.LibraryItemId,result,original,cancellationToken).ConfigureAwait(false);if(result.GameIdentifier is not null||titleChanged)updated++;if(titleChanged)cleaned++;}catch(Exception exception)when(exception is IOException or UnauthorizedAccessException){failed++;errors.Add($"{Path.GetFileName(game.GamePath)}: {exception.Message}");}}
        return new(games.Count,updated,cleaned,failed,errors);
    }

    private sealed class PlatformTitleComparer : IEqualityComparer<(string Platform, string Title)>
    {
        public bool Equals((string Platform,string Title) x,(string Platform,string Title) y)=>string.Equals(x.Platform,y.Platform,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.Title,y.Title,StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Platform,string Title) value)=>HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Platform),StringComparer.OrdinalIgnoreCase.GetHashCode(value.Title));
    }
}
