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

public sealed class ArtworkReviewService(
    ISqliteConnectionFactory connections,
    IGameIdentityResolver identities,
    IEnumerable<IArtworkProvider> providers,
    IArtworkAssignmentStore assignments,
    IMetadataProvenanceStore provenance,
    INexaStationPaths paths) : IArtworkReviewService
{
    public async Task<IReadOnlyList<ArtworkReviewItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT i.ArtworkJobId,i.Id,i.LibraryItemId,l.Title FROM ArtworkJobItems i JOIN LibraryItems l ON l.Id=i.LibraryItemId WHERE i.Status=$review ORDER BY i.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$review", (int)ArtworkJobItemStatus.AwaitingReview);
        List<(string Job, string Item, string Library, string Title)> rows = [];
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        List<ArtworkReviewItem> result = [];
        foreach (var row in rows)
        {
            GameIdentity identity = await identities.ResolveAsync(row.Library, cancellationToken).ConfigureAwait(false);
            result.Add(new(row.Job, row.Item, row.Library, row.Title, identity.PlatformId, identity.GameSerial, identity.FileName, await ReadCandidatesAsync(row.Item, cancellationToken).ConfigureAwait(false)));
        }
        return result;
    }

    public async Task AcceptAsync(string jobItemId, string providerId, string providerGameId, bool remember, CancellationToken cancellationToken = default)
    {
        IArtworkProvider provider = providers.FirstOrDefault(value => string.Equals(value.Id, providerId, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("The artwork provider is unavailable.");
        (string jobId, string libraryItemId, int score, ArtworkKind[] types) = await ReadSelectionAsync(jobItemId, providerId, providerGameId, cancellationToken).ConfigureAwait(false);
        GameIdentity identity = await identities.ResolveAsync(libraryItemId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RemoteArtworkAsset> assets = await provider.GetAssetsAsync(new(providerGameId, types, Region: identity.Region), cancellationToken).ConfigureAwait(false);
        int applied = 0;
        foreach (ArtworkKind type in types)
        {
            ArtworkAssignment? current = await assignments.GetCurrentAsync(libraryItemId, type, cancellationToken).ConfigureAwait(false);
            if (current is { Locked: true } || current is { UserSelected: true }) continue;
            RemoteArtworkAsset? asset = assets.Where(value => value.Type == type && !value.Animated).OrderByDescending(value => (long)(value.Width ?? 0) * (value.Height ?? 0)).FirstOrDefault(); if (asset is null) continue;
            ArtworkDownloadResult download = await provider.DownloadAsync(asset, new(libraryItemId, Path.Combine(paths.Data, "Artwork")), cancellationToken).ConfigureAwait(false); if (!download.Succeeded || download.LocalPath is null || download.ContentHash is null) continue;
            string urlHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(asset.DownloadUri.AbsoluteUri))).ToLowerInvariant();
            await assignments.ApplyAsync(new(Guid.NewGuid().ToString("N"), libraryItemId, type, download.LocalPath, download.ContentHash, providerId, providerGameId, asset.AssetId, urlHash, asset.Width, asset.Height, download.Width, download.Height, download.FileSize, score, true, false, asset.Attribution, asset.Licence, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false); applied++;
        }
        if (applied == 0) throw new InvalidOperationException("The selected candidate had no usable assets for this job.");
        DateTimeOffset now = DateTimeOffset.UtcNow; await provenance.SaveExternalIdentityAsync(new(Guid.NewGuid().ToString("N"), libraryItemId, providerId, providerGameId, ExternalIdentityType.ProviderGameId, IdentityConfidence.ExactExternalId, remember, now, now), cancellationToken).ConfigureAwait(false);
        await FinishReviewAsync(jobId, jobItemId, ArtworkJobItemStatus.Applied, cancellationToken).ConfigureAwait(false);
    }

    public async Task SkipAsync(string jobItemId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand lookup = connection.CreateCommand(); lookup.CommandText = "SELECT ArtworkJobId FROM ArtworkJobItems WHERE Id=$id AND Status=$review;"; lookup.Parameters.AddWithValue("$id", jobItemId); lookup.Parameters.AddWithValue("$review", (int)ArtworkJobItemStatus.AwaitingReview); string jobId = await lookup.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string ?? throw new InvalidOperationException("The review item is no longer pending."); await FinishReviewAsync(jobId, jobItemId, ArtworkJobItemStatus.Skipped, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ArtworkMatchCandidate>> ReadCandidatesAsync(string itemId, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT MetadataJson FROM ArtworkCandidates WHERE ArtworkJobItemId=$item AND Decision<>$reject ORDER BY Score DESC LIMIT 20;"; command.Parameters.AddWithValue("$item", itemId); command.Parameters.AddWithValue("$reject", (int)MatchDecision.Reject); List<ArtworkMatchCandidate> result = []; await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false); while (await reader.ReadAsync(token).ConfigureAwait(false)) if (JsonSerializer.Deserialize<ArtworkMatchCandidate>(reader.GetString(0)) is { } candidate) result.Add(candidate); return result;
    }

    private async Task<(string JobId, string LibraryItemId, int Score, ArtworkKind[] Types)> ReadSelectionAsync(string itemId, string providerId, string gameId, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT i.ArtworkJobId,i.LibraryItemId,c.Score,j.SettingsJson FROM ArtworkJobItems i JOIN ArtworkJobs j ON j.Id=i.ArtworkJobId JOIN ArtworkCandidates c ON c.ArtworkJobItemId=i.Id WHERE i.Id=$item AND i.Status=$review AND c.ProviderId=$provider AND c.ProviderGameId=$game LIMIT 1;"; command.Parameters.AddWithValue("$item", itemId); command.Parameters.AddWithValue("$review", (int)ArtworkJobItemStatus.AwaitingReview); command.Parameters.AddWithValue("$provider", providerId); command.Parameters.AddWithValue("$game", gameId); await using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false); if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("The candidate is no longer available for review."); using JsonDocument document = JsonDocument.Parse(reader.GetString(3)); ArtworkKind[] types = document.RootElement.GetProperty("AssetTypes").EnumerateArray().Select(value => (ArtworkKind)value.GetInt32()).ToArray(); return (reader.GetString(0), reader.GetString(1), reader.GetInt32(2), types);
    }

    private async Task FinishReviewAsync(string jobId, string itemId, ArtworkJobItemStatus status, CancellationToken token)
    {
        await using SqliteConnection connection = await connections.OpenAsync(token).ConfigureAwait(false); await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false); string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        await using (SqliteCommand item = connection.CreateCommand()) { item.Transaction = transaction; item.CommandText = "UPDATE ArtworkJobItems SET Status=$status,UpdatedAtUtc=$now WHERE Id=$id AND Status=$review;"; item.Parameters.AddWithValue("$status", (int)status); item.Parameters.AddWithValue("$now", now); item.Parameters.AddWithValue("$id", itemId); item.Parameters.AddWithValue("$review", (int)ArtworkJobItemStatus.AwaitingReview); if (await item.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("The review item is no longer pending."); }
        await using (SqliteCommand job = connection.CreateCommand()) { job.Transaction = transaction; job.CommandText = "UPDATE ArtworkJobs SET ReviewItems=MAX(0,ReviewItems-1),Status=CASE WHEN ReviewItems<=1 THEN CASE WHEN FailedItems>0 THEN $errors ELSE $completed END ELSE Status END,CompletedAtUtc=CASE WHEN ReviewItems<=1 THEN $now ELSE CompletedAtUtc END,LastUpdatedAtUtc=$now WHERE Id=$id;"; job.Parameters.AddWithValue("$errors", (int)ArtworkJobStatus.CompletedWithErrors); job.Parameters.AddWithValue("$completed", (int)ArtworkJobStatus.Completed); job.Parameters.AddWithValue("$now", now); job.Parameters.AddWithValue("$id", jobId); await job.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
