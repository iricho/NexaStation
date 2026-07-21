using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.MediaPlayer.ExternalPlayers;
using NexaStation.MediaPlayer.Playback;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqlitePhase13Store(ISqliteConnectionFactory factory) : IMediaPlaybackProgressStore, IMediaPlayerPreferencesStore, IExternalMediaPlayerRegistrationStore
{
    public async Task<MediaPlaybackProgress?> GetAsync(string mediaItemId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT PositionSeconds,DurationSeconds,Completed,IsPrivate,LastPlayedAtUtc,UpdatedAtUtc FROM MediaPlaybackProgress WHERE MediaItemId=$id;"; command.Parameters.AddWithValue("$id", mediaItemId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(mediaItemId, reader.GetDouble(0), reader.GetDouble(1), reader.GetBoolean(2), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture), DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture), reader.GetBoolean(3));
    }

    public async Task SaveAsync(MediaPlaybackProgress progress, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO MediaPlaybackProgress(MediaItemId,PositionSeconds,DurationSeconds,Completed,IsPrivate,LastPlayedAtUtc,UpdatedAtUtc) VALUES($id,$position,$duration,$completed,$private,$played,$updated) ON CONFLICT(MediaItemId) DO UPDATE SET PositionSeconds=excluded.PositionSeconds,DurationSeconds=excluded.DurationSeconds,Completed=excluded.Completed,IsPrivate=excluded.IsPrivate,LastPlayedAtUtc=excluded.LastPlayedAtUtc,UpdatedAtUtc=excluded.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$id", progress.MediaItemId); command.Parameters.AddWithValue("$position", progress.PositionSeconds); command.Parameters.AddWithValue("$duration", progress.DurationSeconds); command.Parameters.AddWithValue("$completed", progress.Completed); command.Parameters.AddWithValue("$private", progress.IsPrivate); command.Parameters.AddWithValue("$played", progress.LastPlayedAtUtc.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("$updated", progress.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string mediaItemId, CancellationToken cancellationToken = default) { await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "DELETE FROM MediaPlaybackProgress WHERE MediaItemId=$id;"; command.Parameters.AddWithValue("$id", mediaItemId); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }

    async Task<MediaPlayerPreferences> IMediaPlayerPreferencesStore.GetAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Volume,Muted,PlaybackRate,RememberPlaybackRate,AspectRatio,ControlsInactivitySeconds FROM MediaPlayerPreferences WHERE Id=1;"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return new(ControlsInactivityDelay: TimeSpan.FromSeconds(4));
        return new(reader.GetDouble(0), reader.GetBoolean(1), reader.GetDouble(2), reader.GetBoolean(3), Enum.TryParse(reader.GetString(4), out MediaAspectRatioMode ratio) ? ratio : MediaAspectRatioMode.Fit, TimeSpan.FromSeconds(reader.GetDouble(5)));
    }

    public async Task SaveAsync(MediaPlayerPreferences preferences, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO MediaPlayerPreferences(Id,Volume,Muted,PlaybackRate,RememberPlaybackRate,AspectRatio,ControlsInactivitySeconds,UpdatedAtUtc) VALUES(1,$volume,$muted,$rate,$remember,$ratio,$delay,$updated) ON CONFLICT(Id) DO UPDATE SET Volume=excluded.Volume,Muted=excluded.Muted,PlaybackRate=excluded.PlaybackRate,RememberPlaybackRate=excluded.RememberPlaybackRate,AspectRatio=excluded.AspectRatio,ControlsInactivitySeconds=excluded.ControlsInactivitySeconds,UpdatedAtUtc=excluded.UpdatedAtUtc;"; command.Parameters.AddWithValue("$volume", preferences.Volume); command.Parameters.AddWithValue("$muted", preferences.Muted); command.Parameters.AddWithValue("$rate", preferences.PlaybackRate); command.Parameters.AddWithValue("$remember", preferences.RememberPlaybackRate); command.Parameters.AddWithValue("$ratio", preferences.AspectRatio.ToString()); command.Parameters.AddWithValue("$delay", (preferences.ControlsInactivityDelay ?? TimeSpan.FromSeconds(4)).TotalSeconds); command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task<IReadOnlyList<ExternalMediaPlayerDescriptor>> IExternalMediaPlayerRegistrationStore.GetAsync(CancellationToken cancellationToken)
    {
        List<ExternalMediaPlayerDescriptor> result = []; await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,DisplayName,ExecutablePath,ApplicationIdentity,IconPath,DetectionSource,SupportedExtensionsJson,UserAdded,Preferred,ArgumentsTemplate FROM ExternalMediaPlayers WHERE Enabled=1;"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { string? path = reader.IsDBNull(2) ? null : reader.GetString(2); IReadOnlySet<string> extensions = (JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase); result.Add(new(reader.GetString(0), reader.GetString(1), path, reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), Enum.TryParse(reader.GetString(5), out ExternalPlayerDetectionSource source) ? source : ExternalPlayerDetectionSource.UserAdded, extensions, reader.GetBoolean(7), ExternalMediaPlayerService.ValidateExecutable(path), reader.GetBoolean(8), reader.GetString(9))); }
        return result;
    }
}
