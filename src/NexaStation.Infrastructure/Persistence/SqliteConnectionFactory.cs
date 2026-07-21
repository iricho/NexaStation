using Microsoft.Data.Sqlite;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    string DatabasePath { get; }
    Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(INexaStationPaths paths)
        : this((paths ?? throw new ArgumentNullException(nameof(paths))).Database)
    {
    }

    public SqliteConnectionFactory(string? databasePath = null)
    {
        DatabasePath = Path.GetFullPath(databasePath ?? new NexaStationPaths().Database);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared }.ToString();
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
