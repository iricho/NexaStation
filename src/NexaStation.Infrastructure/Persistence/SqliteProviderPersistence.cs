using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteProviderPersistence(ISqliteConnectionFactory factory, ILibraryRepository library) : IProviderPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProviderSource?> FindSourceAsync(string providerId, string providerItemId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM LibraryItemSources WHERE ProviderId=$provider AND ProviderItemId=$item LIMIT 1;";
        command.Parameters.AddWithValue("$provider", providerId);
        command.Parameters.AddWithValue("$item", providerItemId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSource(reader) : null;
    }

    public async Task<LibraryItem?> FindPossibleDuplicateAsync(DiscoveredLibraryItem item, CancellationToken cancellationToken = default)
    {
        if (item.LaunchUri is not null)
        {
            LibraryItem? targetDuplicate = await library.FindDuplicateAsync(new LaunchTarget(LaunchType.Uri, Uri: item.LaunchUri), cancellationToken).ConfigureAwait(false);
            if (targetDuplicate is not null) return targetDuplicate;
        }

        IReadOnlyList<LibraryItem> titleMatches = await library.QueryAsync(new LibraryQuery(SearchText: item.Title, IncludeHidden: true, MaximumResults: 20), cancellationToken).ConfigureAwait(false);
        return titleMatches.FirstOrDefault(candidate => string.Equals(candidate.Title, item.Title, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ProviderSource>> GetSourcesAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM LibraryItemSources WHERE ProviderId=$provider ORDER BY ProviderItemId;";
        command.Parameters.AddWithValue("$provider", providerId);
        List<ProviderSource> result = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadSource(reader));
        return result;
    }

    public async Task<ProviderImportResult> ImportAsync(string providerId, IReadOnlyCollection<ImportSelection> selections, CancellationToken cancellationToken = default)
    {
        Stopwatch timer = Stopwatch.StartNew();
        int added = 0, updated = 0, unchanged = 0, skipped = 0, failed = 0;
        List<string> errors = [];
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureProviderAsync(connection, transaction, providerId, cancellationToken).ConfigureAwait(false);
            foreach (ImportSelection selection in selections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selection.Action == ImportSelectionAction.Skip) { skipped++; continue; }
                if (!string.Equals(selection.Item.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)) { failed++; errors.Add($"Item {selection.Item.ProviderItemId} belongs to another provider."); continue; }

                string? existingSourceItemId = await FindSourceItemIdAsync(connection, transaction, providerId, selection.Item.ProviderItemId, cancellationToken).ConfigureAwait(false);
                if (existingSourceItemId is not null) { unchanged++; continue; }

                string libraryItemId;
                if (selection.Action == ImportSelectionAction.Link)
                {
                    if (string.IsNullOrWhiteSpace(selection.ExistingLibraryItemId)) { failed++; errors.Add($"A target item is required to link {selection.Item.Title}."); continue; }
                    libraryItemId = selection.ExistingLibraryItemId;
                    await EnsureLaunchTargetAsync(connection, transaction, libraryItemId, selection.Item, cancellationToken).ConfigureAwait(false);
                    updated++;
                }
                else
                {
                    libraryItemId = Guid.NewGuid().ToString("N");
                    await InsertLibraryItemAsync(connection, transaction, libraryItemId, selection.Item, cancellationToken).ConfigureAwait(false);
                    added++;
                }

                string? effectiveLibrarySourceId=await UpsertLibrarySourceAsync(connection, transaction, selection.Item, cancellationToken).ConfigureAwait(false);
                DiscoveredLibraryItem persistedItem=effectiveLibrarySourceId is null?selection.Item:selection.Item with{ProviderLibrarySourceId=effectiveLibrarySourceId};
                await InsertProviderSourceAsync(connection, transaction, libraryItemId, persistedItem, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        timer.Stop();
        return new(providerId, new(selections.Count, added, updated, unchanged, 0, skipped, failed, 0, timer.Elapsed), errors);
    }

    public async Task<ProviderRefreshResult> ApplyRefreshAsync(ProviderScanResult scanResult, CancellationToken cancellationToken = default)
    {
        Stopwatch timer = Stopwatch.StartNew();
        int updated = 0, unchanged = 0, missing = 0, failed = scanResult.Errors.Count, remaining = 0;
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureProviderAsync(connection, transaction, scanResult.ProviderId, cancellationToken).ConfigureAwait(false);
            Dictionary<string,string> effectiveLibrarySourceIds=new(StringComparer.OrdinalIgnoreCase);
            foreach (ProviderLibrarySource source in scanResult.LibrarySources)
                effectiveLibrarySourceIds[source.Id]=await UpsertLibrarySourceAsync(connection, transaction, source, scanResult.CompletedAtUtc, cancellationToken).ConfigureAwait(false);

            Dictionary<string, DiscoveredLibraryItem> discovered = scanResult.DiscoveredItems
                .GroupBy(item => item.ProviderItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => RemapLibrarySource(group.First(),effectiveLibrarySourceIds), StringComparer.OrdinalIgnoreCase);
            HashSet<string> availableSourceIds = scanResult.LibrarySources.Where(source => source.Available).Select(source => effectiveLibrarySourceIds[source.Id]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ProviderSource> current = await ReadSourcesAsync(connection, transaction, scanResult.ProviderId, cancellationToken).ConfigureAwait(false);

            foreach (ProviderSource source in current)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (discovered.Remove(source.ProviderItemId, out DiscoveredLibraryItem? item))
                {
                    bool changed = source.Installed != item.Installed ||
                        !string.Equals(source.InstallationPath, item.InstallationPath, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(JsonSerializer.Serialize(source.ProviderData, JsonOptions), JsonSerializer.Serialize(item.ProviderData, JsonOptions), StringComparison.Ordinal);
                    await UpdateProviderItemAsync(connection, transaction, source, item, scanResult.CompletedAtUtc, cancellationToken).ConfigureAwait(false);
                    if (changed) updated++; else unchanged++;
                }
                else if (source.ProviderLibrarySourceId is not null && availableSourceIds.Contains(source.ProviderLibrarySourceId))
                {
                    await SetInstalledAsync(connection, transaction, source.Id, false, scanResult.CompletedAtUtc, cancellationToken).ConfigureAwait(false);
                    missing++;
                }
                else
                {
                    unchanged++;
                }
            }
            remaining = discovered.Count;

            await RecordScanAsync(connection, transaction, scanResult, updated, missing, failed, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        timer.Stop();
        ProviderRefreshSummary summary = new(scanResult.DiscoveredItems.Count, 0, updated, unchanged, missing, remaining, failed, scanResult.Warnings.Count, timer.Elapsed);
        return new(scanResult.ProviderId, summary, scanResult.Errors);
    }

    public Task AddMetadataOverrideAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default) =>
        ExecuteOverrideAsync("INSERT INTO MetadataOverrides (LibraryItemId,FieldName,UpdatedAtUtc) VALUES($item,$field,$at) ON CONFLICT(LibraryItemId,FieldName) DO UPDATE SET UpdatedAtUtc=excluded.UpdatedAtUtc;", libraryItemId, fieldName, cancellationToken);

    public Task RemoveMetadataOverrideAsync(string libraryItemId, string fieldName, CancellationToken cancellationToken = default) =>
        ExecuteOverrideAsync("DELETE FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName=$field;", libraryItemId, fieldName, cancellationToken);

    private async Task ExecuteOverrideAsync(string sql, string item, string field, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql;
        command.Parameters.AddWithValue("$item", item); command.Parameters.AddWithValue("$field", field); command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertLibraryItemAsync(SqliteConnection c, SqliteTransaction tx, string itemId, DiscoveredLibraryItem item, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow; string targetId = Guid.NewGuid().ToString("N");
        await using (SqliteCommand command = c.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = "INSERT INTO LibraryItems (Id,Title,SortTitle,ContentType,Favorite,Hidden,AddedAtUtc,UpdatedAtUtc,TotalPlaytimeSeconds,DefaultLaunchTargetId) VALUES($id,$title,$sort,$type,0,0,$at,$at,0,$target);";
            Add(command,"$id",itemId); Add(command,"$title",item.Title); Add(command,"$sort",item.SortTitle); Add(command,"$type",(int)item.ContentType); Add(command,"$at",now.ToString("O")); Add(command,"$target",targetId);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        await InsertLaunchTargetAsync(c, tx, targetId, itemId, item, now, ct).ConfigureAwait(false);
        await InsertArtworkAsync(c, tx, itemId, item.ArtworkCandidates, now, ct).ConfigureAwait(false);
    }

    private static async Task EnsureLaunchTargetAsync(SqliteConnection c, SqliteTransaction tx, string itemId, DiscoveredLibraryItem item, CancellationToken ct)
    {
        await using SqliteCommand find = c.CreateCommand(); find.Transaction = tx;
        find.CommandText = "SELECT 1 FROM LaunchTargets WHERE LibraryItemId=$item AND Uri=$uri LIMIT 1;"; Add(find,"$item",itemId); Add(find,"$uri",item.LaunchUri?.AbsoluteUri);
        if (await find.ExecuteScalarAsync(ct).ConfigureAwait(false) is null)
            await InsertLaunchTargetAsync(c, tx, Guid.NewGuid().ToString("N"), itemId, item, DateTime.UtcNow, ct).ConfigureAwait(false);
    }

    private static async Task InsertLaunchTargetAsync(SqliteConnection c, SqliteTransaction tx, string id, string itemId, DiscoveredLibraryItem item, DateTime now, CancellationToken ct)
    {
        await using SqliteCommand command = c.CreateCommand(); command.Transaction = tx;
        command.CommandText = "INSERT INTO LaunchTargets (Id,LibraryItemId,Name,LaunchType,WorkingDirectory,Uri,HideFrontendWhileRunning,RestoreFrontendAfterExit,CreatedAtUtc,UpdatedAtUtc,ProviderId,IsEnabled,UserManaged) VALUES($id,$item,'Steam',$type,$working,$uri,1,1,$at,$at,$provider,1,0);";
        Add(command,"$id",id); Add(command,"$item",itemId); Add(command,"$type",(int)LaunchType.Uri); Add(command,"$working",item.InstallationPath); Add(command,"$uri",item.LaunchUri?.AbsoluteUri); Add(command,"$at",now.ToString("O")); Add(command,"$provider",item.ProviderId);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task InsertArtworkAsync(SqliteConnection c, SqliteTransaction tx, string itemId, IReadOnlyList<ArtworkCandidate> artwork, DateTime now, CancellationToken ct)
    {
        foreach (ArtworkCandidate candidate in artwork.OrderByDescending(value => value.Priority).DistinctBy(value => value.SuggestedType))
        {
            if (!File.Exists(candidate.Path)) continue;
            string id = Guid.NewGuid().ToString("N");
            await using SqliteCommand command = c.CreateCommand(); command.Transaction = tx;
            command.CommandText = "INSERT INTO Assets (Id,LibraryItemId,AssetType,LocalPath,Width,Height,FileSize,AddedAtUtc) VALUES($id,$item,$type,$path,$width,$height,$size,$at);";
            Add(command,"$id",id); Add(command,"$item",itemId); Add(command,"$type",(int)candidate.SuggestedType); Add(command,"$path",candidate.Path); Add(command,"$width",candidate.Width); Add(command,"$height",candidate.Height); Add(command,"$size",new FileInfo(candidate.Path).Length); Add(command,"$at",now.ToString("O"));
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            string column = candidate.SuggestedType == AssetType.Cover ? "CoverAssetId" : candidate.SuggestedType == AssetType.Background ? "BackgroundAssetId" : string.Empty;
            if (column.Length > 0) { await using SqliteCommand update = c.CreateCommand(); update.Transaction=tx; update.CommandText=$"UPDATE LibraryItems SET {column}=$asset WHERE Id=$item;"; Add(update,"$asset",id);Add(update,"$item",itemId);await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
        }
    }

    private static async Task<string?> UpsertLibrarySourceAsync(SqliteConnection c, SqliteTransaction tx, DiscoveredLibraryItem item, CancellationToken ct)
    {
        if (item.ProviderLibrarySourceId is null || item.ManifestPath is null) return null;
        string libraryRoot = ProviderLibraryIdentity.CanonicalRootFromManifest(item.ManifestPath);
        ProviderLibrarySource source = new(item.ProviderLibrarySourceId,item.ProviderId,libraryRoot,ProviderLibraryIdentity.NormalizePath(libraryRoot),true,true);
        return await UpsertLibrarySourceAsync(c,tx,source,DateTime.UtcNow,ct).ConfigureAwait(false);
    }

    private static async Task<string> UpsertLibrarySourceAsync(SqliteConnection c, SqliteTransaction tx, ProviderLibrarySource source, DateTime seen, CancellationToken ct)
    {
        string normalized=ProviderLibraryIdentity.NormalizePath(source.Path);
        string stableId=ProviderLibraryIdentity.CreateId(source.ProviderId,source.Path);
        string? pathId=await LibrarySourceIdAsync(c,tx,"ProviderId=$provider AND NormalizedPath=$normalized",source.ProviderId,normalized,ct).ConfigureAwait(false);
        string? idPath=await LibrarySourcePathAsync(c,tx,source.Id,ct).ConfigureAwait(false);

        if(pathId is not null)
        {
            if(!pathId.Equals(source.Id,StringComparison.OrdinalIgnoreCase)&&idPath is not null&&
                (ProviderLibraryIdentity.IsLegacyChildPath(source.Path,idPath)||ProviderLibraryIdentity.IsLegacyChildPath(idPath,source.Path)))
                await MergeLibrarySourceAsync(c,tx,source.Id,pathId,ct).ConfigureAwait(false);
            await UpdateLibrarySourceAsync(c,tx,pathId,source,normalized,seen,ct).ConfigureAwait(false);
            return pathId;
        }

        if(idPath is not null)
        {
            if(ProviderLibraryIdentity.IsLegacyChildPath(source.Path,idPath)||ProviderLibraryIdentity.IsLegacyChildPath(idPath,source.Path))
            {
                await UpdateLibrarySourceAsync(c,tx,source.Id,source,normalized,seen,ct).ConfigureAwait(false);
                return source.Id;
            }

            source=source with{Id=stableId};
        }

        await using SqliteCommand command = c.CreateCommand(); command.Transaction=tx;
        command.CommandText="INSERT INTO ProviderLibrarySources (Id,ProviderId,ExternalId,Path,NormalizedPath,Enabled,FirstSeenAtUtc,LastSeenAtUtc) VALUES($id,$provider,$external,$path,$normalized,$enabled,$seen,$seen) ON CONFLICT(Id) DO UPDATE SET ExternalId=excluded.ExternalId,Path=excluded.Path,NormalizedPath=excluded.NormalizedPath,Enabled=excluded.Enabled,LastSeenAtUtc=excluded.LastSeenAtUtc ON CONFLICT(ProviderId,NormalizedPath) DO UPDATE SET ExternalId=excluded.ExternalId,Path=excluded.Path,Enabled=excluded.Enabled,LastSeenAtUtc=excluded.LastSeenAtUtc;";
        Add(command,"$id",source.Id);Add(command,"$provider",source.ProviderId);Add(command,"$external",source.ExternalId);Add(command,"$path",source.Path);Add(command,"$normalized",normalized);Add(command,"$enabled",source.Enabled?1:0);Add(command,"$seen",seen.ToUniversalTime().ToString("O"));await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return source.Id;
    }

    private static async Task<string?> LibrarySourceIdAsync(SqliteConnection c,SqliteTransaction tx,string predicate,string provider,string normalized,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText=$"SELECT Id FROM ProviderLibrarySources WHERE {predicate} LIMIT 1;";Add(command,"$provider",provider);Add(command,"$normalized",normalized);return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;}
    private static async Task<string?> LibrarySourcePathAsync(SqliteConnection c,SqliteTransaction tx,string id,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="SELECT Path FROM ProviderLibrarySources WHERE Id=$id LIMIT 1;";Add(command,"$id",id);return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;}
    private static async Task MergeLibrarySourceAsync(SqliteConnection c,SqliteTransaction tx,string duplicateId,string retainedId,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="UPDATE LibraryItemSources SET ProviderLibrarySourceId=$retained WHERE ProviderLibrarySourceId=$duplicate; DELETE FROM ProviderLibrarySources WHERE Id=$duplicate;";Add(command,"$retained",retainedId);Add(command,"$duplicate",duplicateId);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
    private static async Task UpdateLibrarySourceAsync(SqliteConnection c,SqliteTransaction tx,string id,ProviderLibrarySource source,string normalized,DateTime seen,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="UPDATE ProviderLibrarySources SET ExternalId=$external,Path=$path,NormalizedPath=$normalized,Enabled=$enabled,LastSeenAtUtc=$seen WHERE Id=$id;";Add(command,"$external",source.ExternalId);Add(command,"$path",source.Path);Add(command,"$normalized",normalized);Add(command,"$enabled",source.Enabled?1:0);Add(command,"$seen",seen.ToUniversalTime().ToString("O"));Add(command,"$id",id);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
    private static DiscoveredLibraryItem RemapLibrarySource(DiscoveredLibraryItem item,Dictionary<string,string> effectiveIds)=>item.ProviderLibrarySourceId is { } id&&effectiveIds.TryGetValue(id,out string? effective)?item with{ProviderLibrarySourceId=effective}:item;

    private static async Task InsertProviderSourceAsync(SqliteConnection c, SqliteTransaction tx, string itemId, DiscoveredLibraryItem item, CancellationToken ct)
    {
        DateTime now=DateTime.UtcNow;await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;
        command.CommandText="INSERT INTO LibraryItemSources (Id,LibraryItemId,ProviderId,ProviderItemId,ProviderLibrarySourceId,InstallationPath,ManifestPath,Installed,ProviderDataJson,FirstSeenAtUtc,LastSeenAtUtc) VALUES($id,$item,$provider,$providerItem,$library,$path,$manifest,$installed,$data,$now,$now);";
        Add(command,"$id",Guid.NewGuid().ToString("N"));Add(command,"$item",itemId);Add(command,"$provider",item.ProviderId);Add(command,"$providerItem",item.ProviderItemId);Add(command,"$library",item.ProviderLibrarySourceId);Add(command,"$path",item.InstallationPath);Add(command,"$manifest",item.ManifestPath);Add(command,"$installed",item.Installed?1:0);Add(command,"$data",JsonSerializer.Serialize(item.ProviderData,JsonOptions));Add(command,"$now",now.ToString("O"));await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureProviderAsync(SqliteConnection c, SqliteTransaction tx, string providerId, CancellationToken ct)
    {
        DateTime now=DateTime.UtcNow;
        string displayName=providerId.ToLowerInvariant() switch
        {
            "steam"=>"Steam",
            "epic"=>"Epic Games",
            "gog"=>"GOG",
            "ubisoft"=>"Ubisoft Connect",
            "ea"=>"EA app",
            "battlenet"=>"Battle.net",
            _=>providerId
        };
        await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;
        command.CommandText="INSERT OR IGNORE INTO Providers(Id,DisplayName,Enabled,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$name,1,$now,$now);";
        Add(command,"$id",providerId);Add(command,"$name",displayName);Add(command,"$now",now.ToString("O"));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task UpdateProviderItemAsync(SqliteConnection c, SqliteTransaction tx, ProviderSource source, DiscoveredLibraryItem item, DateTime seen, CancellationToken ct)
    {
        await using(SqliteCommand command=c.CreateCommand()){command.Transaction=tx;command.CommandText="UPDATE LibraryItemSources SET ProviderLibrarySourceId=$library,InstallationPath=$path,ManifestPath=$manifest,Installed=$installed,ProviderDataJson=$data,LastSeenAtUtc=$seen WHERE Id=$id;";Add(command,"$library",item.ProviderLibrarySourceId);Add(command,"$path",item.InstallationPath);Add(command,"$manifest",item.ManifestPath);Add(command,"$installed",item.Installed?1:0);Add(command,"$data",JsonSerializer.Serialize(item.ProviderData,JsonOptions));Add(command,"$seen",seen.ToUniversalTime().ToString("O"));Add(command,"$id",source.Id);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
        await using(SqliteCommand command=c.CreateCommand()){command.Transaction=tx;command.CommandText="UPDATE LibraryItems SET Title=CASE WHEN EXISTS(SELECT 1 FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName='Title') THEN Title ELSE $title END, SortTitle=CASE WHEN EXISTS(SELECT 1 FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName='SortTitle') THEN SortTitle ELSE $sort END, ContentType=CASE WHEN EXISTS(SELECT 1 FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName='ContentType') THEN ContentType ELSE $type END, UpdatedAtUtc=$seen WHERE Id=$item;";Add(command,"$title",item.Title);Add(command,"$sort",item.SortTitle);Add(command,"$type",(int)item.ContentType);Add(command,"$seen",seen.ToUniversalTime().ToString("O"));Add(command,"$item",source.LibraryItemId);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
        await using(SqliteCommand command=c.CreateCommand()){command.Transaction=tx;command.CommandText="UPDATE LaunchTargets SET WorkingDirectory=$path,Uri=$uri,IsEnabled=$installed,UpdatedAtUtc=$seen WHERE LibraryItemId=$item AND ProviderId=$provider AND UserManaged=0;";Add(command,"$path",item.InstallationPath);Add(command,"$uri",item.LaunchUri?.AbsoluteUri);Add(command,"$installed",item.Installed?1:0);Add(command,"$seen",seen.ToUniversalTime().ToString("O"));Add(command,"$item",source.LibraryItemId);Add(command,"$provider",item.ProviderId);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
        await UpsertProviderArtworkAsync(c,tx,source.LibraryItemId,item.ArtworkCandidates,seen,ct).ConfigureAwait(false);
    }

    private static async Task UpsertProviderArtworkAsync(SqliteConnection c,SqliteTransaction tx,string itemId,IReadOnlyList<ArtworkCandidate> candidates,DateTime seen,CancellationToken ct)
    {
        foreach(ArtworkCandidate candidate in candidates.OrderByDescending(value=>value.Priority).DistinctBy(value=>value.SuggestedType))
        {
            if(!File.Exists(candidate.Path))continue;string field=candidate.SuggestedType switch{AssetType.Background=>"Background",AssetType.Logo=>"Logo",AssetType.Icon=>"Icon",_=>"Cover"};
            await using(SqliteCommand overridden=c.CreateCommand()){overridden.Transaction=tx;overridden.CommandText="SELECT 1 FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName=$field LIMIT 1;";Add(overridden,"$item",itemId);Add(overridden,"$field",field);if(await overridden.ExecuteScalarAsync(ct).ConfigureAwait(false) is not null)continue;}
            string? assetId;await using(SqliteCommand find=c.CreateCommand()){find.Transaction=tx;find.CommandText="SELECT Id FROM Assets WHERE LibraryItemId=$item AND AssetType=$type AND LocalPath=$path COLLATE NOCASE LIMIT 1;";Add(find,"$item",itemId);Add(find,"$type",(int)candidate.SuggestedType);Add(find,"$path",candidate.Path);assetId=await find.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;}
            if(assetId is null)
            {
                assetId=Guid.NewGuid().ToString("N");FileInfo info=new(candidate.Path);await using SqliteCommand insert=c.CreateCommand();insert.Transaction=tx;insert.CommandText="INSERT INTO Assets(Id,LibraryItemId,AssetType,LocalPath,Width,Height,FileSize,AddedAtUtc) VALUES($id,$item,$type,$path,$width,$height,$size,$at);";Add(insert,"$id",assetId);Add(insert,"$item",itemId);Add(insert,"$type",(int)candidate.SuggestedType);Add(insert,"$path",candidate.Path);Add(insert,"$width",candidate.Width);Add(insert,"$height",candidate.Height);Add(insert,"$size",info.Length);Add(insert,"$at",seen.ToUniversalTime().ToString("O"));await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            string? column=candidate.SuggestedType switch{AssetType.Cover=>"CoverAssetId",AssetType.Background=>"BackgroundAssetId",_=>null};if(column is not null){await using SqliteCommand update=c.CreateCommand();update.Transaction=tx;update.CommandText=$"UPDATE LibraryItems SET {column}=$asset WHERE Id=$item;";Add(update,"$asset",assetId);Add(update,"$item",itemId);await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}
        }
    }

    private static async Task SetInstalledAsync(SqliteConnection c,SqliteTransaction tx,string id,bool installed,DateTime seen,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="UPDATE LibraryItemSources SET Installed=$installed,LastSeenAtUtc=$seen WHERE Id=$id; UPDATE LaunchTargets SET IsEnabled=$installed,UpdatedAtUtc=$seen WHERE UserManaged=0 AND LibraryItemId=(SELECT LibraryItemId FROM LibraryItemSources WHERE Id=$id) AND ProviderId=(SELECT ProviderId FROM LibraryItemSources WHERE Id=$id);";Add(command,"$installed",installed?1:0);Add(command,"$seen",seen.ToUniversalTime().ToString("O"));Add(command,"$id",id);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}

    private static async Task<string?> FindSourceItemIdAsync(SqliteConnection c,SqliteTransaction tx,string provider,string providerItem,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="SELECT LibraryItemId FROM LibraryItemSources WHERE ProviderId=$provider AND ProviderItemId=$item LIMIT 1;";Add(command,"$provider",provider);Add(command,"$item",providerItem);return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;}

    private static async Task<IReadOnlyList<ProviderSource>> ReadSourcesAsync(SqliteConnection c,SqliteTransaction tx,string provider,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="SELECT * FROM LibraryItemSources WHERE ProviderId=$provider;";Add(command,"$provider",provider);List<ProviderSource> result=[];await using SqliteDataReader reader=await command.ExecuteReaderAsync(ct).ConfigureAwait(false);while(await reader.ReadAsync(ct).ConfigureAwait(false))result.Add(ReadSource(reader));return result;}

    private static async Task RecordScanAsync(SqliteConnection c,SqliteTransaction tx,ProviderScanResult scan,int updated,int missing,int failed,CancellationToken ct){await using SqliteCommand command=c.CreateCommand();command.Transaction=tx;command.CommandText="INSERT INTO ProviderScanHistory (Id,ProviderId,StartedAtUtc,CompletedAtUtc,Status,DiscoveredCount,UpdatedCount,MissingCount,FailedCount,WarningCount,ErrorSummary) VALUES($id,$provider,$start,$end,$status,$discovered,$updated,$missing,$failed,$warnings,$error); UPDATE Providers SET InstallationPath=$path,LastSuccessfulScanAtUtc=$end,LastScanStatus=$status,LastError=$error,UpdatedAtUtc=$end WHERE Id=$provider;";Add(command,"$id",Guid.NewGuid().ToString("N"));Add(command,"$provider",scan.ProviderId);Add(command,"$start",scan.StartedAtUtc.ToUniversalTime().ToString("O"));Add(command,"$end",scan.CompletedAtUtc.ToUniversalTime().ToString("O"));Add(command,"$status",(int)(scan.Errors.Count==0?ProviderScanStatus.Succeeded:ProviderScanStatus.Failed));Add(command,"$discovered",scan.DiscoveredItems.Count);Add(command,"$updated",updated);Add(command,"$missing",missing);Add(command,"$failed",failed);Add(command,"$warnings",scan.Warnings.Count);Add(command,"$error",scan.Errors.Count==0?null:string.Join(" | ",scan.Errors));Add(command,"$path",scan.InstallationPath);await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);}

    private static ProviderSource ReadSource(SqliteDataReader reader)=>new(reader.GetString(reader.GetOrdinal("Id")),reader.GetString(reader.GetOrdinal("LibraryItemId")),reader.GetString(reader.GetOrdinal("ProviderId")),reader.GetString(reader.GetOrdinal("ProviderItemId")),Text(reader,"InstallationPath"),Text(reader,"ManifestPath"),reader.GetInt32(reader.GetOrdinal("Installed"))!=0,Deserialize(Text(reader,"ProviderDataJson")),DateTime.Parse(reader.GetString(reader.GetOrdinal("FirstSeenAtUtc")),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),DateTime.Parse(reader.GetString(reader.GetOrdinal("LastSeenAtUtc")),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Text(reader,"ProviderLibrarySourceId"));
    private static Dictionary<string,string> Deserialize(string? json){if(string.IsNullOrWhiteSpace(json))return new Dictionary<string,string>();try{return JsonSerializer.Deserialize<Dictionary<string,string>>(json,JsonOptions)??new Dictionary<string,string>();}catch(JsonException){return new Dictionary<string,string>();}}
    private static string? Text(SqliteDataReader reader,string name){int i=reader.GetOrdinal(name);return reader.IsDBNull(i)?null:reader.GetString(i);}
    private static string NormalizePath(string path)=>ProviderLibraryIdentity.NormalizePath(path);
    private static void Add(SqliteCommand command,string name,object? value)=>command.Parameters.AddWithValue(name,value??DBNull.Value);
}
