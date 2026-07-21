using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;

namespace NexaStation.Infrastructure.Metadata;

public sealed class GameIdentityResolver(ISqliteConnectionFactory connections) : IGameIdentityResolver
{
    public async Task<GameIdentity> ResolveAsync(string libraryItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryItemId);
        await using SqliteConnection connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT li.ContentType,li.Title,lis.ProviderId,lis.ProviderItemId,
                   e.PlatformId,e.GameIdentifier,e.Region,e.Revision,e.DiscNumber,e.GamePath,e.FileExtension,e.FileSize,
                   CASE WHEN EXISTS(SELECT 1 FROM GameExternalIdentities x WHERE x.LibraryItemId=li.Id AND x.ConfirmedByUser=1) THEN 1 ELSE 0 END
            FROM LibraryItems li
            LEFT JOIN LibraryItemSources lis ON lis.LibraryItemId=li.Id AND lis.Installed=1
            LEFT JOIN EmulatedGameSources e ON e.LibraryItemId=li.Id AND e.Available=1
            WHERE li.Id=$item
            ORDER BY CASE WHEN e.Id IS NULL THEN 1 ELSE 0 END,lis.LastSeenAtUtc DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$item", libraryItemId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) throw new KeyNotFoundException("The library item was not found.");
        string title = reader.GetString(1);
        string? provider = Text(reader, 2);
        string? providerItem = Text(reader, 3);
        string? serial = Text(reader, 5);
        bool confirmed = reader.GetInt32(12) != 0;
        IdentityConfidence confidence = confirmed ? IdentityConfidence.ExactExternalId
            : serial is not null ? IdentityConfidence.VerifiedSerial
            : providerItem is not null ? IdentityConfidence.VerifiedProviderId
            : Text(reader, 4) is not null ? IdentityConfidence.PlatformAndTitle
            : IdentityConfidence.FilenameOnly;
        return new(
            libraryItemId, (ContentType)reader.GetInt32(0), GameTitleNormalizer.Normalize(title), title,
            Text(reader, 4), provider, providerItem,
            string.Equals(provider, "steam", StringComparison.OrdinalIgnoreCase) ? providerItem : null,
            serial, null, Text(reader, 6), Text(reader, 7), reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Text(reader, 9) is { } path ? Path.GetFileName(path) : null, Text(reader, 10), reader.IsDBNull(11) ? null : reader.GetInt64(11),
            null, confidence);
    }

    private static string? Text(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
