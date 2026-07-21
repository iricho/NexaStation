using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;

namespace NexaStation.Infrastructure.Metadata;

public sealed class ArtworkPresenceService(ISqliteConnectionFactory connections,IArtworkAssignmentStore assignments) : IArtworkPresenceService
{
    public async Task<bool> IsPresentAsync(string libraryItemId,ArtworkKind assetType,CancellationToken cancellationToken=default)
    {
        ArtworkAssignment? current=await assignments.GetCurrentAsync(libraryItemId,assetType,cancellationToken).ConfigureAwait(false);if(current is not null&&(current.Locked||current.UserSelected||Suitable(current.LocalPath,current.DownloadedWidth??current.OriginalWidth,current.DownloadedHeight??current.OriginalHeight,assetType)))return true;
        AssetType? legacy=assetType switch{ArtworkKind.CoverPortrait or ArtworkKind.BoxFront=>AssetType.Cover,ArtworkKind.GridWide=>AssetType.Cover,ArtworkKind.HeroBackground=>AssetType.Background,_=>null};if(legacy is null)return false;
        await using SqliteConnection connection=await connections.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT a.LocalPath,a.Width,a.Height FROM Assets a JOIN LibraryItems li ON (li.CoverAssetId=a.Id OR li.BackgroundAssetId=a.Id) WHERE li.Id=$item AND a.AssetType=$type ORDER BY a.AddedAtUtc DESC LIMIT 1;";command.Parameters.AddWithValue("$item",libraryItemId);command.Parameters.AddWithValue("$type",(int)legacy.Value);await using SqliteDataReader r=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);return await r.ReadAsync(cancellationToken).ConfigureAwait(false)&&Suitable(r.GetString(0),r.IsDBNull(1)?null:r.GetInt32(1),r.IsDBNull(2)?null:r.GetInt32(2),assetType);
    }
    public static bool Suitable(string? path,int? width,int? height,ArtworkKind type)
    {
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path)||width is null||height is null||width<128||height<128)return false;return type switch{ArtworkKind.CoverPortrait or ArtworkKind.BoxFront=>height>=width,ArtworkKind.GridWide or ArtworkKind.HeroBackground=>width>height,_=>true};
    }
}
