using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace NexaStation.Recovery;

public enum DatabaseHealthStatus
{
    Missing,
    Healthy,
    Corrupt,
    Inaccessible,
}

public sealed record DatabaseHealthResult(DatabaseHealthStatus Status, string Message, long SchemaVersion = 0)
{
    public bool CanStartNormally => Status is DatabaseHealthStatus.Missing or DatabaseHealthStatus.Healthy;
}

public interface IDatabaseHealthService
{
    Task<DatabaseHealthResult> CheckAsync(string databasePath, CancellationToken cancellationToken = default);
}

public sealed class DatabaseHealthService : IDatabaseHealthService
{
    public async Task<DatabaseHealthResult> CheckAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
        if (!File.Exists(fullPath))
        {
            return new(DatabaseHealthStatus.Missing, "No database exists yet; a new local library will be created.");
        }

        try
        {
            string connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
            }.ToString();
            await using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqliteCommand healthCommand = connection.CreateCommand();
            healthCommand.CommandText = "PRAGMA quick_check;";
            string? quickCheck = Convert.ToString(await healthCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new(DatabaseHealthStatus.Corrupt, $"SQLite integrity check failed: {quickCheck ?? "no result"}.");
            }

            long schemaVersion = 0;
            await using SqliteCommand tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaMigrations';";
            long hasMigrationTable = Convert.ToInt64(await tableCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (hasMigrationTable > 0)
            {
                tableCommand.CommandText = "SELECT COALESCE(MAX(Version),0) FROM SchemaMigrations;";
                schemaVersion = Convert.ToInt64(await tableCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            }

            return new(DatabaseHealthStatus.Healthy, "The local library database passed its integrity check.", schemaVersion);
        }
        catch (SqliteException exception)
        {
            DatabaseHealthStatus status = exception.SqliteErrorCode is 11 or 26 ? DatabaseHealthStatus.Corrupt : DatabaseHealthStatus.Inaccessible;
            return new(status, $"The local library database could not be validated: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(DatabaseHealthStatus.Inaccessible, $"The local library database is inaccessible: {exception.Message}");
        }
    }
}

public sealed record BackupManifest(
    int FormatVersion,
    string Product,
    DateTimeOffset CreatedAtUtc,
    string Reason,
    string DatabaseEntry,
    string Sha256,
    long Length);

public sealed record BackupResult(string BackupPath, BackupManifest Manifest);

public interface IDatabaseBackupService
{
    Task<BackupResult> CreateAsync(string databasePath, string reason, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupPath, string databasePath, CancellationToken cancellationToken = default);
}

public sealed class DatabaseBackupService(INexaStationPaths paths, IDatabaseHealthService healthService) : IDatabaseBackupService
{
    private const string ManifestEntry = "manifest.json";
    private const string DatabaseEntry = "data/nexastation.db";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public async Task<BackupResult> CreateAsync(string databasePath, string reason, CancellationToken cancellationToken = default)
    {
        string sourcePath = Path.GetFullPath(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The database cannot be backed up because it does not exist.", sourcePath);
        }

        paths.EnsureCreated();
        DatabaseHealthResult sourceHealth = await healthService.CheckAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        if (sourceHealth.Status != DatabaseHealthStatus.Healthy)
        {
            throw new InvalidDataException($"A validated backup cannot be created because the database is not healthy: {sourceHealth.Message}");
        }

        string snapshotPath = Path.Combine(paths.Backups, $".snapshot-{Guid.NewGuid():N}.db");
        string hash;
        try
        {
            await CreateOnlineSnapshotAsync(sourcePath, snapshotPath, cancellationToken).ConfigureAwait(false);
            hash = await ComputeHashAsync(snapshotPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            File.Delete(snapshotPath);
            throw;
        }
        FileInfo sourceInfo = new(snapshotPath);
        BackupManifest manifest = new(1, "NexaStation", DateTimeOffset.UtcNow, SanitizeReason(reason), DatabaseEntry, hash, sourceInfo.Length);
        string fileName = $"nexastation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{FileNameReason(manifest.Reason)}.nxbackup";
        string targetPath = Path.Combine(paths.Backups, fileName);
        string temporaryPath = targetPath + ".tmp";

        try
        {
            await using FileStream archiveStream = new(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: true);
            ZipArchiveEntry databaseEntry = archive.CreateEntry(DatabaseEntry, CompressionLevel.Optimal);
            await using (Stream destination = databaseEntry.Open())
            await using (FileStream source = new(snapshotPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }

            ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
            await using Stream manifestStream = manifestEntry.Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
        finally
        {
            File.Delete(snapshotPath);
        }

        File.Move(temporaryPath, targetPath, false);
        return new(targetPath, manifest);
    }

    public async Task RestoreAsync(string backupPath, string databasePath, CancellationToken cancellationToken = default)
    {
        string sourcePath = Path.GetFullPath(backupPath ?? throw new ArgumentNullException(nameof(backupPath)));
        string targetPath = Path.GetFullPath(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected backup does not exist.", sourcePath);
        }

        string temporaryDatabase = targetPath + ".restore.tmp";
        File.Delete(temporaryDatabase);
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(sourcePath);
            ValidateEntries(archive);
            ZipArchiveEntry manifestEntry = archive.GetEntry(ManifestEntry) ?? throw new InvalidDataException("The backup manifest is missing.");
            BackupManifest manifest;
            await using (Stream stream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("The backup manifest is invalid.");
            }

            if (manifest.FormatVersion != 1 || !string.Equals(manifest.Product, "NexaStation", StringComparison.Ordinal) || !string.Equals(manifest.DatabaseEntry, DatabaseEntry, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The backup format is not supported by this version of NexaStation.");
            }

            ZipArchiveEntry databaseEntry = archive.GetEntry(DatabaseEntry) ?? throw new InvalidDataException("The backup database is missing.");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? throw new InvalidOperationException("The database path has no parent directory."));
            await using (Stream source = databaseEntry.Open())
            await using (FileStream destination = new(temporaryDatabase, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            FileInfo restoredInfo = new(temporaryDatabase);
            string restoredHash = await ComputeHashAsync(temporaryDatabase, cancellationToken).ConfigureAwait(false);
            if (restoredInfo.Length != manifest.Length || !string.Equals(restoredHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The backup database failed its size or SHA-256 validation.");
            }

            DatabaseHealthResult health = await healthService.CheckAsync(temporaryDatabase, cancellationToken).ConfigureAwait(false);
            if (health.Status != DatabaseHealthStatus.Healthy)
            {
                throw new InvalidDataException($"The restored database failed validation: {health.Message}");
            }

            if (File.Exists(targetPath))
            {
                await CreateAsync(targetPath, "before-restore", cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryDatabase, targetPath, true);
            DeleteSidecar(targetPath + "-wal");
            DeleteSidecar(targetPath + "-shm");
        }
        finally
        {
            File.Delete(temporaryDatabase);
        }
    }

    private static void ValidateEntries(ZipArchive archive)
    {
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string normalized = entry.FullName.Replace('\\', '/');
            if ((normalized.Length > 0 && normalized[0] == '/') || normalized.Contains("../", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
            {
                throw new InvalidDataException("The backup contains an unsafe path.");
            }

            if (!string.Equals(normalized, ManifestEntry, StringComparison.Ordinal) && !string.Equals(normalized, DatabaseEntry, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The backup contains an unexpected entry: {normalized}.");
            }
        }
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task CreateOnlineSnapshotAsync(string sourcePath, string snapshotPath, CancellationToken cancellationToken)
    {
        string sourceConnectionString = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        string snapshotConnectionString = new SqliteConnectionStringBuilder { DataSource = snapshotPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
        await using SqliteConnection source = new(sourceConnectionString);
        await using SqliteConnection destination = new(snapshotConnectionString);
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);
        source.BackupDatabase(destination);
        await destination.CloseAsync().ConfigureAwait(false);
        await source.CloseAsync().ConfigureAwait(false);
    }

    private static string SanitizeReason(string reason) => string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim()[..Math.Min(reason.Trim().Length, 80)];
    private static string FileNameReason(string reason) => string.Concat(reason.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')).Trim('-');
    private static void DeleteSidecar(string path) { if (File.Exists(path)) File.Delete(path); }
}
