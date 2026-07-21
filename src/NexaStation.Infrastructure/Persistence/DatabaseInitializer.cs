using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Persistence;

public sealed class DatabaseStartupException(string message, DatabaseHealthStatus healthStatus, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public DatabaseHealthStatus HealthStatus { get; } = healthStatus;
}

public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly IAppLogger _logger;
    private readonly IDatabaseHealthService _healthService;
    private readonly IDatabaseBackupService? _backupService;
    public static int CurrentSchemaVersion => LoadMigrations().Max(migration => migration.Version);

    public DatabaseInitializer(ISqliteConnectionFactory factory, IAppLogger logger)
        : this(factory, logger, new DatabaseHealthService(), null)
    {
    }

    public DatabaseInitializer(
        ISqliteConnectionFactory factory,
        IAppLogger logger,
        IDatabaseHealthService healthService,
        IDatabaseBackupService? backupService)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _backupService = backupService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Migration[] migrations = LoadMigrations();
        long latestVersion = migrations.Max(migration => migration.Version);
        DatabaseHealthResult preflight = await _healthService.CheckAsync(_factory.DatabasePath, cancellationToken).ConfigureAwait(false);
        if (!preflight.CanStartNormally)
        {
            throw new DatabaseStartupException(preflight.Message, preflight.Status);
        }

        if (preflight.SchemaVersion > latestVersion)
        {
            throw new DatabaseStartupException(
                $"Database schema version {preflight.SchemaVersion} is newer than this application supports ({latestVersion}).",
                DatabaseHealthStatus.Inaccessible);
        }

        if (preflight.Status == DatabaseHealthStatus.Healthy && preflight.SchemaVersion < latestVersion && _backupService is not null)
        {
            BackupResult backup = await _backupService.CreateAsync(_factory.DatabasePath, $"before-migration-{preflight.SchemaVersion}-to-{latestVersion}", cancellationToken).ConfigureAwait(false);
            _logger.Information("DatabasePreMigrationBackupCreated", "A validated pre-migration database backup was created.", new Dictionary<string, object?> { ["BackupPath"] = backup.BackupPath, ["SchemaVersion"] = preflight.SchemaVersion });
        }

        await using SqliteConnection connection = await _factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS SchemaMigrations(Version INTEGER PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL);", cancellationToken).ConfigureAwait(false);

        long current = await ScalarAsync(connection, "SELECT COALESCE(MAX(Version),0) FROM SchemaMigrations;", cancellationToken).ConfigureAwait(false);
        if (current > latestVersion)
            throw new InvalidOperationException($"Database schema version {current} is newer than this application supports.");
        await ValidateHistoryAsync(connection, migrations, current, cancellationToken).ConfigureAwait(false);

        foreach (Migration migration in migrations.Where(m => m.Version > current))
        {
            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = "INSERT INTO SchemaMigrations(Version,Name,AppliedAtUtc) VALUES($version,$name,$at);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$version", migration.Version);
                command.Parameters.AddWithValue("$name", migration.Name);
                command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.Information("DatabaseMigrationApplied", $"Applied database migration {migration.Version}: {migration.Name}.");
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        await ValidateHistoryAsync(connection, migrations, latestVersion, cancellationToken).ConfigureAwait(false);
        await using SqliteCommand healthCommand = connection.CreateCommand();
        healthCommand.CommandText = "PRAGMA quick_check;";
        string? result = Convert.ToString(await healthCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new DatabaseStartupException($"Post-migration database validation failed: {result ?? "no result"}.", DatabaseHealthStatus.Corrupt);
        }
    }

    private static async Task ValidateHistoryAsync(SqliteConnection connection, IReadOnlyList<Migration> migrations, long currentVersion, CancellationToken cancellationToken)
    {
        Dictionary<int, string> expected = migrations.Where(migration => migration.Version <= currentVersion).ToDictionary(migration => migration.Version, migration => migration.Name);
        Dictionary<int, string> actual = [];
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Version, Name FROM SchemaMigrations ORDER BY Version;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            actual.Add(reader.GetInt32(0), reader.GetString(1));
        }

        if (actual.Count != expected.Count || expected.Any(migration => !actual.TryGetValue(migration.Key, out string? name) || !string.Equals(name, migration.Value, StringComparison.Ordinal)))
        {
            throw new DatabaseStartupException("Database migration history is missing, duplicated, or does not match this application build.", DatabaseHealthStatus.Inaccessible);
        }
    }

    private static Migration[] LoadMigrations()
    {
        Assembly assembly = typeof(DatabaseInitializer).Assembly;
        return assembly.GetManifestResourceNames().Where(name => name.Contains(".Persistence.Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                string file = name[(name.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..];
                int version = int.Parse(file[..file.IndexOf('_')], CultureInfo.InvariantCulture);
                using Stream stream = assembly.GetManifestResourceStream(name)!;
                using StreamReader reader = new(stream);
                return new Migration(version, Path.GetFileNameWithoutExtension(file), reader.ReadToEnd());
            }).OrderBy(m => m.Version).ToArray();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    { await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    { await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql; return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture); }
    private sealed record Migration(int Version, string Name, string Sql);
}
