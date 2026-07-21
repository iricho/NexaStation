using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Metadata;

public sealed class SqliteArtworkAssignmentStore(ISqliteConnectionFactory connections, INexaStationPaths paths) : IArtworkAssignmentStore
{
    private const int RetainedHistoryCount = 2;

    public Task<ArtworkAssignment?> GetCurrentAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default) =>
        ReadOneAsync("WHERE LibraryItemId=$item AND AssetType=$type AND IsCurrent=1 ORDER BY AppliedAtUtc DESC LIMIT 1", libraryItemId, assetType, cancellationToken);

    public async Task<IReadOnlyList<ArtworkAssignment>> GetHistoryAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = Select + " WHERE LibraryItemId=$item AND AssetType=$type ORDER BY IsCurrent DESC,AppliedAtUtc DESC;";
        command.Parameters.AddWithValue("$item", libraryItemId);
        command.Parameters.AddWithValue("$type", (int)assetType);
        List<ArtworkAssignment> result = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    public async Task ApplyAsync(ArtworkAssignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        string managedRoot = Path.GetFullPath(Path.Combine(paths.Data, "Artwork"));
        string localPath = Path.GetFullPath(assignment.LocalPath);
        if (!localPath.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Artwork assignments must reference a NexaStation-managed copy.");
        if (!File.Exists(localPath)) throw new FileNotFoundException("The managed artwork file is missing.", localPath);

        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (SqliteCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "UPDATE ArtworkAssignments SET IsCurrent=0 WHERE LibraryItemId=$item AND AssetType=$type AND IsCurrent=1;";
            clear.Parameters.AddWithValue("$item", assignment.LibraryItemId);
            clear.Parameters.AddWithValue("$type", (int)assignment.AssetType);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO ArtworkAssignments(Id,LibraryItemId,AssetType,LocalPath,ContentHash,SourceProviderId,SourceGameId,SourceAssetId,SourceUrlHash,OriginalWidth,OriginalHeight,DownloadedWidth,DownloadedHeight,FileSize,Confidence,UserSelected,Locked,AttributionJson,LicenceMetadata,AppliedAtUtc,IsCurrent,HistoryPinned)
                VALUES($id,$item,$type,$path,$hash,$provider,$game,$asset,$urlHash,$ow,$oh,$dw,$dh,$size,$confidence,$user,$locked,$attribution,$licence,$applied,1,$pinned);
                """;
            Add(insert, "$id", assignment.Id); Add(insert, "$item", assignment.LibraryItemId); Add(insert, "$type", (int)assignment.AssetType);
            Add(insert, "$path", localPath); Add(insert, "$hash", assignment.ContentHash); Add(insert, "$provider", assignment.SourceProviderId);
            Add(insert, "$game", assignment.SourceGameId); Add(insert, "$asset", assignment.SourceAssetId); Add(insert, "$urlHash", assignment.SourceUrlHash);
            Add(insert, "$ow", assignment.OriginalWidth); Add(insert, "$oh", assignment.OriginalHeight); Add(insert, "$dw", assignment.DownloadedWidth);
            Add(insert, "$dh", assignment.DownloadedHeight); Add(insert, "$size", assignment.FileSize); Add(insert, "$confidence", assignment.Confidence);
            Add(insert, "$user", assignment.UserSelected ? 1 : 0); Add(insert, "$locked", assignment.Locked ? 1 : 0);
            Add(insert, "$attribution", assignment.Attribution is null ? null : JsonSerializer.Serialize(assignment.Attribution));
            Add(insert, "$licence", assignment.LicenceMetadata); Add(insert, "$applied", assignment.AppliedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(insert, "$pinned", assignment.HistoryPinned ? 1 : 0);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await PruneAsync(assignment.LibraryItemId, assignment.AssetType, managedRoot, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ArtworkAssignment?> UndoAsync(string libraryItemId, ArtworkKind assetType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ArtworkAssignment> history = await GetHistoryAsync(libraryItemId, assetType, cancellationToken).ConfigureAwait(false);
        ArtworkAssignment? previous = history.Where(item => !item.IsCurrent).OrderByDescending(item => item.AppliedAtUtc).FirstOrDefault();
        if (previous is null || !File.Exists(previous.LocalPath)) return null;
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE ArtworkAssignments SET IsCurrent=0 WHERE LibraryItemId=$item AND AssetType=$type; UPDATE ArtworkAssignments SET IsCurrent=1,AppliedAtUtc=$now WHERE Id=$id;";
        Add(command, "$item", libraryItemId); Add(command, "$type", (int)assetType); Add(command, "$id", previous.Id); Add(command, "$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return previous with { IsCurrent = true, AppliedAtUtc = DateTimeOffset.UtcNow };
    }

    public async Task SetLockedAsync(string libraryItemId, ArtworkKind assetType, bool locked, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE ArtworkAssignments SET Locked=$locked WHERE LibraryItemId=$item AND AssetType=$type AND IsCurrent=1;";
        Add(command, "$locked", locked ? 1 : 0); Add(command, "$item", libraryItemId); Add(command, "$type", (int)assetType);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ArtworkAssignment?> ReadOneAsync(string where, string item, ArtworkKind type, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = Select + " " + where;
        Add(command, "$item", item); Add(command, "$type", (int)type);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    private async Task PruneAsync(string item, ArtworkKind type, string managedRoot, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArtworkAssignment> history = await GetHistoryAsync(item, type, cancellationToken).ConfigureAwait(false);
        foreach (ArtworkAssignment obsolete in history.Where(value => !value.IsCurrent && !value.HistoryPinned).OrderByDescending(value => value.AppliedAtUtc).Skip(RetainedHistoryCount))
        {
            await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "DELETE FROM ArtworkAssignments WHERE Id=$id AND IsCurrent=0 AND HistoryPinned=0;"; Add(command, "$id", obsolete.Id);
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0)
            {
                string full = Path.GetFullPath(obsolete.LocalPath);
                if (full.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !history.Any(value => value.Id != obsolete.Id && string.Equals(value.LocalPath, full, StringComparison.OrdinalIgnoreCase)))
                    try { File.Delete(full); } catch (IOException) { }
            }
        }
    }

    private static ArtworkAssignment Read(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), (ArtworkKind)r.GetInt32(2), r.GetString(3), r.GetString(4), Text(r, 5), Text(r, 6), Text(r, 7), Text(r, 8),
        Int(r, 9), Int(r, 10), Int(r, 11), Int(r, 12), r.GetInt64(13), r.GetInt32(14), r.GetInt32(15) != 0, r.GetInt32(16) != 0,
        Json(Text(r, 17)), Text(r, 18), DateTimeOffset.Parse(r.GetString(19), CultureInfo.InvariantCulture), r.GetInt32(20) != 0, r.GetInt32(21) != 0);
    private static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static int? Int(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt32(i);
    private static Dictionary<string, string>? Json(string? value) => string.IsNullOrWhiteSpace(value) ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(value);
    private static void Add(SqliteCommand c, string name, object? value) => c.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private const string Select = "SELECT Id,LibraryItemId,AssetType,LocalPath,ContentHash,SourceProviderId,SourceGameId,SourceAssetId,SourceUrlHash,OriginalWidth,OriginalHeight,DownloadedWidth,DownloadedHeight,FileSize,Confidence,UserSelected,Locked,AttributionJson,LicenceMetadata,AppliedAtUtc,IsCurrent,HistoryPinned FROM ArtworkAssignments";
}

public sealed class SqliteArtworkJobService(ISqliteConnectionFactory connections) : IArtworkJobService
{
    public async Task<string> CreateAsync(ArtworkJobRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string id = Guid.NewGuid().ToString("N"); DateTimeOffset now = DateTimeOffset.UtcNow;
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction; command.CommandText = "INSERT INTO ArtworkJobs(Id,JobType,Status,ScopeJson,SettingsJson,TotalItems,CreatedAtUtc,LastUpdatedAtUtc) VALUES($id,0,$status,$scope,$settings,$total,$now,$now);";
            Add(command, "$id", id); Add(command, "$status", (int)ArtworkJobStatus.Pending); Add(command, "$scope", JsonSerializer.Serialize(new { request.Name, request.LibraryItemIds }));
            Add(command, "$settings", JsonSerializer.Serialize(new { request.AssetTypes, request.FillMissingOnly, request.NeverReplaceUserArtwork, request.AutoApplyThreshold, request.MaximumConcurrency }));
            Add(command, "$total", request.LibraryItemIds.Distinct(StringComparer.Ordinal).Count()); Add(command, "$now", now.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (string item in request.LibraryItemIds.Distinct(StringComparer.Ordinal))
        {
            await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO ArtworkJobItems(Id,ArtworkJobId,LibraryItemId,Status,UpdatedAtUtc) VALUES($id,$job,$item,$status,$now);";
            Add(command, "$id", Guid.NewGuid().ToString("N")); Add(command, "$job", id); Add(command, "$item", item); Add(command, "$status", (int)ArtworkJobItemStatus.Pending); Add(command, "$now", now.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return id;
    }
    public Task StartAsync(string jobId, CancellationToken cancellationToken = default) => TransitionAsync(jobId, ArtworkJobStatus.Identifying, true, false, cancellationToken);
    public Task PauseAsync(string jobId, CancellationToken cancellationToken = default) => TransitionAsync(jobId, ArtworkJobStatus.Paused, false, false, cancellationToken);
    public Task ResumeAsync(string jobId, CancellationToken cancellationToken = default) => TransitionAsync(jobId, ArtworkJobStatus.Identifying, false, false, cancellationToken);
    public Task CancelAsync(string jobId, CancellationToken cancellationToken = default) => TransitionAsync(jobId, ArtworkJobStatus.Cancelled, false, true, cancellationToken);
    public async Task<ArtworkJobSnapshot> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        string id, name; ArtworkJobStatus status; int total, completed, matched, review, failed; DateTimeOffset created; DateTimeOffset? started, finished;
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id,ScopeJson,Status,TotalItems,CompletedItems,MatchedItems,ReviewItems,FailedItems,CreatedAtUtc,StartedAtUtc,CompletedAtUtc FROM ArtworkJobs WHERE Id=$id;"; Add(command, "$id", jobId);
            await using SqliteDataReader r = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException("The artwork job was not found.");
            using JsonDocument scope = JsonDocument.Parse(r.GetString(1)); name = scope.RootElement.TryGetProperty("Name", out JsonElement n) ? n.GetString() ?? "Artwork job" : "Artwork job";
            id=r.GetString(0);status=(ArtworkJobStatus)r.GetInt32(2);total=r.GetInt32(3);completed=r.GetInt32(4);matched=r.GetInt32(5);review=r.GetInt32(6);failed=r.GetInt32(7);created=At(r.GetString(8));started=r.IsDBNull(9)?null:At(r.GetString(9));finished=r.IsDBNull(10)?null:At(r.GetString(10));
        }
        int applied=0,noMatch=0,skipped=0,active=0;
        await using(SqliteCommand counts=connection.CreateCommand())
        {
            counts.CommandText="SELECT SUM(CASE WHEN Status=$applied THEN 1 ELSE 0 END),SUM(CASE WHEN Status=$nomatch THEN 1 ELSE 0 END),SUM(CASE WHEN Status=$skipped THEN 1 ELSE 0 END),SUM(CASE WHEN Status IN($pending,$identifying,$searching,$match,$downloading,$limited) THEN 1 ELSE 0 END) FROM ArtworkJobItems WHERE ArtworkJobId=$id;";
            Add(counts,"$applied",(int)ArtworkJobItemStatus.Applied);Add(counts,"$nomatch",(int)ArtworkJobItemStatus.NoMatch);Add(counts,"$skipped",(int)ArtworkJobItemStatus.Skipped);Add(counts,"$pending",(int)ArtworkJobItemStatus.Pending);Add(counts,"$identifying",(int)ArtworkJobItemStatus.Identifying);Add(counts,"$searching",(int)ArtworkJobItemStatus.Searching);Add(counts,"$match",(int)ArtworkJobItemStatus.HighConfidenceMatch);Add(counts,"$downloading",(int)ArtworkJobItemStatus.Downloading);Add(counts,"$limited",(int)ArtworkJobItemStatus.RateLimited);Add(counts,"$id",jobId);
            await using SqliteDataReader r=await counts.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);if(await r.ReadAsync(cancellationToken).ConfigureAwait(false)){applied=Int64(r,0);noMatch=Int64(r,1);skipped=Int64(r,2);active=Int64(r,3);}
        }
        return new(id,name,status,total,completed,matched,review,failed,created,started,finished,applied,noMatch,skipped,active,Outcome(status,total,applied,noMatch,review,failed));
    }
    public async Task<IReadOnlyList<ArtworkJobSnapshot>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT Id FROM ArtworkJobs ORDER BY LastUpdatedAtUtc DESC LIMIT 100;";List<string> ids=[];await using(SqliteDataReader reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false)){while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))ids.Add(reader.GetString(0));}
        List<ArtworkJobSnapshot> result=[];foreach(string id in ids)result.Add(await GetAsync(id,cancellationToken).ConfigureAwait(false));return result;
    }
    public async Task<ArtworkJobRequest> GetRequestAsync(string jobId,CancellationToken cancellationToken=default)
    {
        await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT ScopeJson,SettingsJson FROM ArtworkJobs WHERE Id=$id;";Add(command,"$id",jobId);await using SqliteDataReader r=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);if(!await r.ReadAsync(cancellationToken).ConfigureAwait(false))throw new KeyNotFoundException("The artwork job was not found.");using JsonDocument scope=JsonDocument.Parse(r.GetString(0));using JsonDocument settings=JsonDocument.Parse(r.GetString(1));JsonElement s=scope.RootElement;JsonElement v=settings.RootElement;string name=s.TryGetProperty("Name",out JsonElement nameElement)?nameElement.GetString()??"Artwork job":"Artwork job";string[] ids=s.TryGetProperty("LibraryItemIds",out JsonElement idValues)?idValues.EnumerateArray().Select(value=>value.GetString()).Where(value=>value is not null).Cast<string>().ToArray():[];ArtworkKind[] types=v.TryGetProperty("AssetTypes",out JsonElement typeValues)?typeValues.EnumerateArray().Select(value=>(ArtworkKind)value.GetInt32()).ToArray():[ArtworkKind.CoverPortrait,ArtworkKind.HeroBackground];return new(name,ids,types,v.TryGetProperty("FillMissingOnly",out JsonElement fill)&&fill.GetBoolean(),!v.TryGetProperty("NeverReplaceUserArtwork",out JsonElement protect)||protect.GetBoolean(),v.TryGetProperty("AutoApplyThreshold",out JsonElement threshold)?threshold.GetInt32():900,v.TryGetProperty("MaximumConcurrency",out JsonElement concurrency)?concurrency.GetInt32():2);
    }
    public async Task<IReadOnlyList<ArtworkJobItemSnapshot>> ListItemsAsync(string jobId,CancellationToken cancellationToken=default)
    {
        await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();
        command.CommandText="SELECT ji.Id,ji.ArtworkJobId,ji.LibraryItemId,li.Title,ji.Status,ji.SelectedProviderId,ji.SelectedExternalGameId,ji.MatchConfidence,ji.FailureCode,ji.FailureMessage,ji.UpdatedAtUtc FROM ArtworkJobItems ji JOIN LibraryItems li ON li.Id=ji.LibraryItemId WHERE ji.ArtworkJobId=$job ORDER BY ji.UpdatedAtUtc DESC,li.Title;";Add(command,"$job",jobId);List<ArtworkJobItemSnapshot> result=[];await using SqliteDataReader r=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);while(await r.ReadAsync(cancellationToken).ConfigureAwait(false))result.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),(ArtworkJobItemStatus)r.GetInt32(4),Text(r,5),Text(r,6),r.IsDBNull(7)?null:r.GetInt32(7),Text(r,8),Text(r,9),At(r.GetString(10))));return result;
    }
    public async IAsyncEnumerable<ArtworkJobSnapshot> WatchAsync(string jobId,[EnumeratorCancellation] CancellationToken cancellationToken=default)
    {
        ArtworkJobSnapshot? previous=null;
        while(!cancellationToken.IsCancellationRequested)
        {
            ArtworkJobSnapshot current=await GetAsync(jobId,cancellationToken).ConfigureAwait(false);
            if(previous is null||current!=previous){yield return current;previous=current;}
            if(current.Status is ArtworkJobStatus.Completed or ArtworkJobStatus.CompletedWithErrors or ArtworkJobStatus.Cancelled or ArtworkJobStatus.Failed or ArtworkJobStatus.AwaitingReview)yield break;
            await Task.Delay(300,cancellationToken).ConfigureAwait(false);
        }
    }
    private async Task TransitionAsync(string id, ArtworkJobStatus status, bool start, bool complete, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE ArtworkJobs SET Status=$status,StartedAtUtc=CASE WHEN $start=1 THEN COALESCE(StartedAtUtc,$now) ELSE StartedAtUtc END,CompletedAtUtc=CASE WHEN $complete=1 THEN $now ELSE CompletedAtUtc END,LastUpdatedAtUtc=$now WHERE Id=$id AND Status NOT IN($completed,$cancelled);";
        Add(command, "$status", (int)status); Add(command, "$start", start ? 1 : 0); Add(command, "$complete", complete ? 1 : 0); Add(command, "$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); Add(command, "$id", id); Add(command, "$completed", (int)ArtworkJobStatus.Completed); Add(command, "$cancelled", (int)ArtworkJobStatus.Cancelled);
        if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 0) throw new InvalidOperationException("The artwork job cannot transition from its current state.");
    }
    private static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    private static int Int64(SqliteDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?0:checked((int)reader.GetInt64(ordinal));
    private static string? Text(SqliteDataReader reader,int ordinal)=>reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
    private static ArtworkJobOutcome Outcome(ArtworkJobStatus status,int total,int applied,int noMatch,int review,int failed)
    {
        if(status==ArtworkJobStatus.Cancelled)return ArtworkJobOutcome.Cancelled;if(status==ArtworkJobStatus.Failed)return ArtworkJobOutcome.Failed;if(status==ArtworkJobStatus.AwaitingReview||review>0)return ArtworkJobOutcome.NeedsReview;
        if(status is not (ArtworkJobStatus.Completed or ArtworkJobStatus.CompletedWithErrors))return status==ArtworkJobStatus.Pending?ArtworkJobOutcome.None:ArtworkJobOutcome.Running;
        if(total>0&&noMatch>=total)return ArtworkJobOutcome.NoArtworkFound;if(applied>0&&(noMatch>0||failed>0))return ArtworkJobOutcome.Partial;if(failed>0)return ArtworkJobOutcome.Failed;if(applied>0)return ArtworkJobOutcome.Succeeded;return noMatch>0?ArtworkJobOutcome.NoArtworkFound:ArtworkJobOutcome.Succeeded;
    }
    private static void Add(SqliteCommand c, string name, object? value) => c.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
