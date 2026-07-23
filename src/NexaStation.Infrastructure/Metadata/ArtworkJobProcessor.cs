using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Metadata;

/// <summary>Executes persisted artwork jobs without ever treating progress as complete before durable state is written.</summary>
public sealed class ArtworkJobProcessor(
    ISqliteConnectionFactory connections,
    IGameIdentityResolver identities,
    IGameMatchScorer scorer,
    IEnumerable<IArtworkProvider> providers,
    IArtworkAssignmentStore assignments,
    IMetadataProvenanceStore provenance,
    ISettingsService settings,
    INexaStationPaths paths,
    IAppLogger logger,
    IArtworkPresenceService? presence = null) : IArtworkJobProcessor, IDisposable
{
    private readonly SemaphoreSlim _singleRunner = new(1, 1);

    public void Dispose() => _singleRunner.Dispose();

    public async Task RunAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        await _singleRunner.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            JobDefinition job = await ReadJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (job.Status is ArtworkJobStatus.Cancelled or ArtworkJobStatus.Completed or ArtworkJobStatus.CompletedWithErrors) return;
            await SetJobStatusAsync(jobId, ArtworkJobStatus.Identifying, start: true, cancellationToken).ConfigureAwait(false);

            using SemaphoreSlim concurrency = new(Math.Clamp(job.MaximumConcurrency, 1, 4));
            List<Task> work = [];
            foreach (JobItem item in await ReadPendingItemsAsync(jobId, cancellationToken).ConfigureAwait(false))
            {
                await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
                work.Add(ProcessAndReleaseAsync(job, item, concurrency, cancellationToken));
            }
            await Task.WhenAll(work).ConfigureAwait(false);

            ArtworkJobStatus current = await ReadStatusAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (current is ArtworkJobStatus.Paused or ArtworkJobStatus.Cancelled or ArtworkJobStatus.Offline or ArtworkJobStatus.RateLimited) return;
            (int incomplete, int review, int failures) = await ReadOutcomeAsync(jobId, cancellationToken).ConfigureAwait(false);
            ArtworkJobStatus final = review > 0 ? ArtworkJobStatus.AwaitingReview
                : incomplete > 0 || failures > 0 ? ArtworkJobStatus.CompletedWithErrors
                : ArtworkJobStatus.Completed;
            await CompleteJobAsync(jobId, final, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            await SetJobStatusAsync(jobId, ArtworkJobStatus.Offline, start: false, CancellationToken.None).ConfigureAwait(false);
            logger.LogError("ArtworkJobOffline", "An artwork job paused because its provider could not be reached.", properties: new Dictionary<string, object?> { ["JobId"] = jobId, ["StatusCode"] = exception.StatusCode });
        }
        catch (Exception exception)
        {
            await CompleteJobAsync(jobId, ArtworkJobStatus.Failed, CancellationToken.None).ConfigureAwait(false);
            logger.LogError("ArtworkJobFailed", "An artwork job stopped after a durable failure state was recorded.", exception, new Dictionary<string, object?> { ["JobId"] = jobId });
        }
        finally
        {
            _singleRunner.Release();
        }
    }

    public async Task ResumeInterruptedAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM ArtworkJobs WHERE Status IN($identifying,$searching,$downloading) ORDER BY CreatedAtUtc;";
        command.Parameters.AddWithValue("$identifying", (int)ArtworkJobStatus.Identifying);
        command.Parameters.AddWithValue("$searching", (int)ArtworkJobStatus.Searching);
        command.Parameters.AddWithValue("$downloading", (int)ArtworkJobStatus.Downloading);
        List<string> ids = [];
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) ids.Add(reader.GetString(0));
        foreach (string id in ids) await RunAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessAndReleaseAsync(JobDefinition job, JobItem item, SemaphoreSlim concurrency, CancellationToken token)
    {
        try { await ProcessItemAsync(job, item, token).ConfigureAwait(false); }
        finally { concurrency.Release(); }
    }

    private async Task ProcessItemAsync(JobDefinition job, JobItem item, CancellationToken token)
    {
        ArtworkJobStatus jobStatus = await ReadStatusAsync(job.Id, token).ConfigureAwait(false);
        if (jobStatus is ArtworkJobStatus.Paused or ArtworkJobStatus.Cancelled) return;
        try
        {
            await SetItemStatusAsync(item.Id, ArtworkJobItemStatus.Identifying, token: token).ConfigureAwait(false);
            GameIdentity identity = await identities.ResolveAsync(item.LibraryItemId, token).ConfigureAwait(false);
            bool allowOnline = await settings.GetAsync("Metadata.Privacy.AllowOnlineArtwork", false, token).ConfigureAwait(false);
            bool allowSerial = await settings.GetAsync("Metadata.Privacy.AllowSerialLookup", false, token).ConfigureAwait(false);
            bool allowRegion = await settings.GetAsync("Metadata.Privacy.IncludeRegion", true, token).ConfigureAwait(false);
            bool allowFuzzy = await settings.GetAsync("Metadata.Privacy.AllowFuzzyTitleLookup", false, token).ConfigureAwait(false);
            GameIdentity sharedIdentity = identity with
            {
                GameSerial = allowSerial ? identity.GameSerial : null,
                TitleId = allowSerial ? identity.TitleId : null,
                Region = allowRegion ? identity.Region : null,
                OptionalHashes = null,
            };
            List<ArtworkKind> missing=[];foreach(ArtworkKind type in job.AssetTypes)if(presence is null||!await presence.IsPresentAsync(item.LibraryItemId,type,token).ConfigureAwait(false)||!job.FillMissingOnly)missing.Add(type);if(missing.Count==0){await FinishItemAsync(job.Id,item.Id,ArtworkJobItemStatus.Skipped,matched:false,review:false,failed:false,token).ConfigureAwait(false);return;}

            await SetItemStatusAsync(item.Id, ArtworkJobItemStatus.Searching, token: token).ConfigureAwait(false);
            await SetJobStatusAsync(job.Id, ArtworkJobStatus.Searching, start: false, token).ConfigureAwait(false);
            List<ArtworkMatchCandidate> candidates = [];List<string> providerIssues=[];int searchedProviders=0;
            foreach (IArtworkProvider provider in await EnabledProvidersAsync(allowOnline,token).ConfigureAwait(false))
            {
                ArtworkProviderStatus status = await provider.GetStatusAsync(token).ConfigureAwait(false);
                if (!status.Available || !status.Authenticated){providerIssues.Add($"{provider.DisplayName}: {status.FailureCode??"not_ready"}");continue;}
                try{searchedProviders++;IReadOnlyList<ArtworkMatchCandidate> matches = await provider.SearchAsync(new(sharedIdentity, PlatformId: sharedIdentity.PlatformId, Region: sharedIdentity.Region), token).ConfigureAwait(false);candidates.AddRange(matches.Select(candidate=>string.Equals(candidate.ProviderId,"local",StringComparison.OrdinalIgnoreCase)?candidate:scorer.Score(sharedIdentity,candidate)).Select(candidate=>allowFuzzy||StrongOrExactTitle(sharedIdentity,candidate)?candidate:candidate with{Decision=MatchDecision.Reject,Conflicts=[..(candidate.Conflicts??[]),"Fuzzy title lookup is disabled."]}));}
                catch(ArtworkProviderOperationException exception)when(exception.FailureCode=="rate_limited"){await SetItemStatusAsync(item.Id,ArtworkJobItemStatus.RateLimited,failureCode:exception.FailureCode,failureMessage:exception.Message,token:token).ConfigureAwait(false);await SetJobStatusAsync(job.Id,ArtworkJobStatus.RateLimited,start:false,token).ConfigureAwait(false);return;}
                catch(ArtworkProviderOperationException exception){providerIssues.Add($"{provider.DisplayName}: {exception.FailureCode}");}
            }
            ArtworkMatchCandidate? best = candidates.Where(candidate => candidate.Decision != MatchDecision.Reject).OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.ProviderId, StringComparer.Ordinal).ThenBy(candidate => candidate.ProviderGameId, StringComparer.Ordinal).FirstOrDefault();
            await SaveCandidatesAsync(item.Id, candidates, token).ConfigureAwait(false);
            if (best is null)
            {
                string code=searchedProviders==0?"no_ready_provider":"no_candidates";string message=searchedProviders==0?"No enabled provider was ready.":"No matching game was found.";if(providerIssues.Count>0)message+=" "+string.Join("; ",providerIssues);
                await SetItemStatusAsync(item.Id,ArtworkJobItemStatus.NoMatch,failureCode:code,failureMessage:message,token:token).ConfigureAwait(false);
                await FinishItemAsync(job.Id, item.Id, ArtworkJobItemStatus.NoMatch, matched: false, review: false, failed: false, token).ConfigureAwait(false);
                return;
            }
            if (best.Decision != MatchDecision.AutoApply || best.Score < job.AutoApplyThreshold)
            {
                await SetItemStatusAsync(item.Id, ArtworkJobItemStatus.AwaitingReview, best, token: token).ConfigureAwait(false);
                await FinishItemAsync(job.Id, item.Id, ArtworkJobItemStatus.AwaitingReview, matched: true, review: true, failed: false, token).ConfigureAwait(false);
                return;
            }

            IArtworkProvider selectedProvider = providers.First(provider => string.Equals(provider.Id, best.ProviderId, StringComparison.OrdinalIgnoreCase));
            await SetItemStatusAsync(item.Id, ArtworkJobItemStatus.Downloading, best, token: token).ConfigureAwait(false);
            await SetJobStatusAsync(job.Id, ArtworkJobStatus.Downloading, start: false, token).ConfigureAwait(false);
            IReadOnlyList<RemoteArtworkAsset> assets = await selectedProvider.GetAssetsAsync(new(best.ProviderGameId, missing, Region: sharedIdentity.Region), token).ConfigureAwait(false);
            int applied = 0;string? downloadFailure=null;
            foreach (ArtworkKind type in missing)
            {
                ArtworkAssignment? current = await assignments.GetCurrentAsync(item.LibraryItemId, type, token).ConfigureAwait(false);
                if (current is { Locked: true } || job.NeverReplaceUserArtwork && current is { UserSelected: true }) continue;
                RemoteArtworkAsset? asset = assets.Where(value => Compatible(type,value.Type) && !value.Animated).OrderByDescending(value => (long)(value.Width ?? 0) * (value.Height ?? 0)).ThenBy(value => value.AssetId, StringComparer.Ordinal).FirstOrDefault();
                if (asset is null) continue;
                ArtworkDownloadResult download = await selectedProvider.DownloadAsync(asset, new(item.LibraryItemId, Path.Combine(paths.Data, "Artwork")), token).ConfigureAwait(false);
                if (!download.Succeeded || download.LocalPath is null || download.ContentHash is null){downloadFailure=download.FailureCode??"download_failed";continue;}
                string sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(asset.DownloadUri.AbsoluteUri))).ToLowerInvariant();
                await assignments.ApplyAsync(new(
                    Guid.NewGuid().ToString("N"), item.LibraryItemId, type, download.LocalPath, download.ContentHash,
                    best.ProviderId, best.ProviderGameId, asset.AssetId, sourceHash, asset.Width, asset.Height,
                    download.Width, download.Height, download.FileSize, best.Score, false, false, asset.Attribution,
                    asset.Licence, DateTimeOffset.UtcNow), token).ConfigureAwait(false);
                applied++;
            }
            if (applied == 0)
            {
                string code=downloadFailure??(assets.Count==0?"no_usable_assets":"requested_asset_unavailable");await SetItemStatusAsync(item.Id,ArtworkJobItemStatus.NoMatch,best,failureCode:code,failureMessage:downloadFailure is null?"The provider match had no usable requested artwork.":"The selected artwork could not be downloaded safely.",token:token).ConfigureAwait(false);
                await FinishItemAsync(job.Id, item.Id, ArtworkJobItemStatus.NoMatch, matched: true, review: false, failed: false, token).ConfigureAwait(false);
                return;
            }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await provenance.SaveExternalIdentityAsync(new(Guid.NewGuid().ToString("N"), item.LibraryItemId, best.ProviderId, best.ProviderGameId, ExternalIdentityType.ProviderGameId, IdentityConfidence.VerifiedProviderId, false, now, now), token).ConfigureAwait(false);
            await FinishItemAsync(job.Id, item.Id, ArtworkJobItemStatus.Applied, matched: true, review: false, failed: false, token).ConfigureAwait(false);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception exception)
        {
            await SetItemStatusAsync(item.Id, ArtworkJobItemStatus.Failed, failureCode: "item_failed", failureMessage: SafeMessage(exception), token: token).ConfigureAwait(false);
            await FinishItemAsync(job.Id, item.Id, ArtworkJobItemStatus.Failed, matched: false, review: false, failed: true, token).ConfigureAwait(false);
            logger.LogError("ArtworkJobItemFailed", "One artwork item failed; the remaining job will continue.", exception, new Dictionary<string, object?> { ["JobId"] = job.Id, ["LibraryItemId"] = item.LibraryItemId });
        }
    }

    private async Task<JobDefinition> ReadJobAsync(string id, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Status,SettingsJson FROM ArtworkJobs WHERE Id=$id;"; command.Parameters.AddWithValue("$id", id);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new KeyNotFoundException("The artwork job was not found.");
        using JsonDocument settingsDocument = JsonDocument.Parse(reader.GetString(1)); JsonElement value = settingsDocument.RootElement;
        ArtworkKind[] types = value.TryGetProperty("AssetTypes", out JsonElement typeValues) ? typeValues.EnumerateArray().Select(element => (ArtworkKind)element.GetInt32()).ToArray() : [ArtworkKind.CoverPortrait, ArtworkKind.HeroBackground];
        return new(id, (ArtworkJobStatus)reader.GetInt32(0), types,
            value.TryGetProperty("FillMissingOnly", out JsonElement fill) && fill.GetBoolean(),
            !value.TryGetProperty("NeverReplaceUserArtwork", out JsonElement protect) || protect.GetBoolean(),
            value.TryGetProperty("AutoApplyThreshold", out JsonElement threshold) ? threshold.GetInt32() : 900,
            value.TryGetProperty("MaximumConcurrency", out JsonElement maximum) ? maximum.GetInt32() : 2);
    }

    private async Task<IReadOnlyList<JobItem>> ReadPendingItemsAsync(string jobId, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id,LibraryItemId FROM ArtworkJobItems WHERE ArtworkJobId=$job AND Status IN($pending,$identifying,$searching,$downloading,$limited) ORDER BY UpdatedAtUtc;";
        command.Parameters.AddWithValue("$job", jobId); command.Parameters.AddWithValue("$pending", (int)ArtworkJobItemStatus.Pending); command.Parameters.AddWithValue("$identifying", (int)ArtworkJobItemStatus.Identifying); command.Parameters.AddWithValue("$searching", (int)ArtworkJobItemStatus.Searching); command.Parameters.AddWithValue("$downloading", (int)ArtworkJobItemStatus.Downloading); command.Parameters.AddWithValue("$limited", (int)ArtworkJobItemStatus.RateLimited);
        List<JobItem> result = []; await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false); while (await reader.ReadAsync(token).ConfigureAwait(false)) result.Add(new(reader.GetString(0), reader.GetString(1))); return result;
    }

    private async Task<IReadOnlyList<IArtworkProvider>> EnabledProvidersAsync(bool allowOnline,CancellationToken token)
    {
        await using SqliteConnection connection=await connections.OpenAsync(token).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT Id FROM MetadataProviders WHERE Enabled=1 ORDER BY Priority,Id;";List<string> ids=[];await using(SqliteDataReader reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false)){while(await reader.ReadAsync(token).ConfigureAwait(false))ids.Add(reader.GetString(0));}IEnumerable<IArtworkProvider> configured=ids.Select(id=>providers.FirstOrDefault(provider=>string.Equals(provider.Id,id,StringComparison.OrdinalIgnoreCase))).Where(provider=>provider is not null).Cast<IArtworkProvider>();IEnumerable<IArtworkProvider> keyless=providers.Where(provider=>provider.Id is "steam-public" or "gog-public" or "ps2-covers");return configured.Concat(keyless).DistinctBy(provider=>provider.Id,StringComparer.OrdinalIgnoreCase).Where(provider=>provider.Id=="local"||allowOnline).ToArray();
    }

    private async Task SaveCandidatesAsync(string itemId, IReadOnlyList<ArtworkMatchCandidate> candidates, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using (SqliteCommand clear = connection.CreateCommand()) { clear.Transaction = transaction; clear.CommandText = "DELETE FROM ArtworkCandidates WHERE ArtworkJobItemId=$item;"; clear.Parameters.AddWithValue("$item", itemId); await clear.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        foreach (ArtworkMatchCandidate candidate in candidates.OrderByDescending(value => value.Score).Take(50))
        {
            await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO ArtworkCandidates(Id,ArtworkJobItemId,ProviderId,ProviderGameId,Title,PlatformId,Region,Score,Decision,ReasonsJson,ConflictsJson,MetadataJson,CreatedAtUtc) VALUES($id,$item,$provider,$game,$title,$platform,$region,$score,$decision,$reasons,$conflicts,$metadata,$now);";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N")); command.Parameters.AddWithValue("$item", itemId); command.Parameters.AddWithValue("$provider", candidate.ProviderId); command.Parameters.AddWithValue("$game", candidate.ProviderGameId); command.Parameters.AddWithValue("$title", candidate.Title); command.Parameters.AddWithValue("$platform", (object?)candidate.PlatformId ?? DBNull.Value); command.Parameters.AddWithValue("$region", (object?)candidate.Region ?? DBNull.Value); command.Parameters.AddWithValue("$score", candidate.Score); command.Parameters.AddWithValue("$decision", (int)candidate.Decision); command.Parameters.AddWithValue("$reasons", JsonSerializer.Serialize(candidate.Reasons ?? [])); command.Parameters.AddWithValue("$conflicts", JsonSerializer.Serialize(candidate.Conflicts ?? [])); command.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(candidate)); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    private async Task SetItemStatusAsync(string itemId, ArtworkJobItemStatus status, ArtworkMatchCandidate? selected = null, string? failureCode = null, string? failureMessage = null, CancellationToken token = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE ArtworkJobItems SET Status=$status,SelectedProviderId=$provider,SelectedExternalGameId=$game,MatchConfidence=$confidence,FailureCode=$code,FailureMessage=$message,UpdatedAtUtc=$now WHERE Id=$id;";
        command.Parameters.AddWithValue("$status", (int)status); command.Parameters.AddWithValue("$provider", (object?)selected?.ProviderId ?? DBNull.Value); command.Parameters.AddWithValue("$game", (object?)selected?.ProviderGameId ?? DBNull.Value); command.Parameters.AddWithValue("$confidence", (object?)selected?.Score ?? DBNull.Value); command.Parameters.AddWithValue("$code", (object?)failureCode ?? DBNull.Value); command.Parameters.AddWithValue("$message", (object?)failureMessage ?? DBNull.Value); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("$id", itemId); await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task FinishItemAsync(string jobId, string itemId, ArtworkJobItemStatus status, bool matched, bool review, bool failed, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        await using (SqliteCommand item = connection.CreateCommand()) { item.Transaction = transaction; item.CommandText = "UPDATE ArtworkJobItems SET Status=$status,UpdatedAtUtc=$now WHERE Id=$id;"; item.Parameters.AddWithValue("$status", (int)status); item.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); item.Parameters.AddWithValue("$id", itemId); await item.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        await using (SqliteCommand job = connection.CreateCommand()) { job.Transaction = transaction; job.CommandText = "UPDATE ArtworkJobs SET CompletedItems=CompletedItems+1,MatchedItems=MatchedItems+$matched,ReviewItems=ReviewItems+$review,FailedItems=FailedItems+$failed,LastUpdatedAtUtc=$now WHERE Id=$id;"; job.Parameters.AddWithValue("$matched", matched ? 1 : 0); job.Parameters.AddWithValue("$review", review ? 1 : 0); job.Parameters.AddWithValue("$failed", failed ? 1 : 0); job.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); job.Parameters.AddWithValue("$id", jobId); await job.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }

    private async Task SetJobStatusAsync(string id, ArtworkJobStatus status, bool start, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE ArtworkJobs SET Status=$status,StartedAtUtc=CASE WHEN $start=1 THEN COALESCE(StartedAtUtc,$now) ELSE StartedAtUtc END,LastUpdatedAtUtc=$now WHERE Id=$id AND Status<>$cancelled;"; command.Parameters.AddWithValue("$status", (int)status); command.Parameters.AddWithValue("$start", start ? 1 : 0); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$cancelled", (int)ArtworkJobStatus.Cancelled); await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task CompleteJobAsync(string id, ArtworkJobStatus status, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE ArtworkJobs SET Status=$status,CompletedAtUtc=$now,LastUpdatedAtUtc=$now WHERE Id=$id AND Status<>$cancelled;"; command.Parameters.AddWithValue("$status", (int)status); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$cancelled", (int)ArtworkJobStatus.Cancelled); await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task<ArtworkJobStatus> ReadStatusAsync(string id, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Status FROM ArtworkJobs WHERE Id=$id;"; command.Parameters.AddWithValue("$id", id); object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false); return result is long value ? (ArtworkJobStatus)value : throw new KeyNotFoundException("The artwork job was not found.");
    }

    private async Task<(int Incomplete, int Review, int Failures)> ReadOutcomeAsync(string id, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT SUM(CASE WHEN Status IN($applied,$skipped,$nomatch,$review,$failed) THEN 0 ELSE 1 END),SUM(CASE WHEN Status=$review THEN 1 ELSE 0 END),SUM(CASE WHEN Status=$failed THEN 1 ELSE 0 END) FROM ArtworkJobItems WHERE ArtworkJobId=$id;"; command.Parameters.AddWithValue("$applied", (int)ArtworkJobItemStatus.Applied); command.Parameters.AddWithValue("$skipped", (int)ArtworkJobItemStatus.Skipped); command.Parameters.AddWithValue("$nomatch", (int)ArtworkJobItemStatus.NoMatch); command.Parameters.AddWithValue("$review", (int)ArtworkJobItemStatus.AwaitingReview); command.Parameters.AddWithValue("$failed", (int)ArtworkJobItemStatus.Failed); command.Parameters.AddWithValue("$id", id); await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false); await reader.ReadAsync(token).ConfigureAwait(false); return (reader.IsDBNull(0) ? 0 : checked((int)reader.GetInt64(0)), reader.IsDBNull(1) ? 0 : checked((int)reader.GetInt64(1)), reader.IsDBNull(2) ? 0 : checked((int)reader.GetInt64(2)));
    }

    private static string SafeMessage(Exception exception) => exception.Message.Length <= 240 ? exception.Message : exception.Message[..240];
    private static bool Compatible(ArtworkKind requested,ArtworkKind available)=>requested==available||requested==ArtworkKind.CoverPortrait&&available==ArtworkKind.BoxFront||requested==ArtworkKind.HeroBackground&&available==ArtworkKind.Screenshot||requested==ArtworkKind.GridWide&&available==ArtworkKind.Marquee;
    private static bool StrongOrExactTitle(GameIdentity identity,ArtworkMatchCandidate candidate)=>string.Equals(identity.NormalizedTitle,GameTitleNormalizer.Normalize(candidate.Title),StringComparison.Ordinal)||!string.IsNullOrWhiteSpace(identity.GameSerial)&&string.Equals(identity.GameSerial,candidate.GameSerial,StringComparison.OrdinalIgnoreCase)||!string.IsNullOrWhiteSpace(identity.SteamAppId)&&candidate.ExternalIds?.TryGetValue("steam",out string? steam)==true&&string.Equals(identity.SteamAppId,steam,StringComparison.OrdinalIgnoreCase);
    private sealed record JobDefinition(string Id, ArtworkJobStatus Status, ArtworkKind[] AssetTypes, bool FillMissingOnly, bool NeverReplaceUserArtwork, int AutoApplyThreshold, int MaximumConcurrency);
    private sealed record JobItem(string Id, string LibraryItemId);
}
