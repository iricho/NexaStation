using Microsoft.Data.Sqlite;
using NexaStation.Core;

namespace NexaStation.Infrastructure.Persistence;

public abstract class SqliteKeyValueRepository(ISqliteConnectionFactory factory, string table)
{
    protected async Task<string?> GetCoreAsync(string key, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT Value FROM {table} WHERE Key=$key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    protected async Task SetCoreAsync(string key, string value, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {table}(Key,Value,UpdatedAtUtc) VALUES($key,$value,$at) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAtUtc=excluded.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", value); command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SqliteSettingsRepository(ISqliteConnectionFactory factory) : SqliteKeyValueRepository(factory, "Settings"), ISettingsRepository
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => GetCoreAsync(key, cancellationToken);
    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => SetCoreAsync(key, value, cancellationToken);
}

public sealed class SqliteApplicationStateRepository(ISqliteConnectionFactory factory) : SqliteKeyValueRepository(factory, "ApplicationState"), IApplicationStateRepository
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => GetCoreAsync(key, cancellationToken);
    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) => SetCoreAsync(key, value, cancellationToken);
}
