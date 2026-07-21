using System.IO.Compression;
using Microsoft.Data.Sqlite;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Recovery.Tests;

public sealed class RecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NexaStation-Recovery-Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void PathsRemainUnderConfiguredRoot()
    {
        NexaStationPaths paths = new(_root);
        paths.EnsureCreated();
        Assert.All(new[] { paths.Data, paths.State, paths.Logs, paths.Backups, paths.Cache }, path => Assert.StartsWith(paths.Root, path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MissingDatabaseDoesNotCreateAFileDuringHealthCheck()
    {
        string path = Path.Combine(_root, "missing.db");
        DatabaseHealthResult result = await new DatabaseHealthService().CheckAsync(path);
        Assert.Equal(DatabaseHealthStatus.Missing, result.Status);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task HealthyAndCorruptDatabasesAreDistinguished()
    {
        string healthy = await CreateDatabaseAsync();
        Assert.Equal(DatabaseHealthStatus.Healthy, (await new DatabaseHealthService().CheckAsync(healthy)).Status);
        string corrupt = Path.Combine(_root, "corrupt.db");
        await File.WriteAllTextAsync(corrupt, "not a sqlite database");
        Assert.Equal(DatabaseHealthStatus.Corrupt, (await new DatabaseHealthService().CheckAsync(corrupt)).Status);
    }

    [Fact]
    public async Task BackupRoundTripValidatesAndRestoresDatabase()
    {
        string database = await CreateDatabaseAsync();
        NexaStationPaths paths = new(_root);
        DatabaseBackupService service = new(paths, new DatabaseHealthService());
        BackupResult backup = await service.CreateAsync(database, "manual test");
        await ExecuteAsync(database, "INSERT INTO Items(Name) VALUES('changed');");

        await service.RestoreAsync(backup.BackupPath, database);

        Assert.Equal(0L, await ScalarAsync(database, "SELECT COUNT(*) FROM Items;"));
        Assert.NotEmpty(Directory.GetFiles(paths.Backups, "*before-restore*.nxbackup"));
    }

    [Fact]
    public async Task TamperedBackupIsRejectedWithoutReplacingDatabase()
    {
        string database = await CreateDatabaseAsync();
        NexaStationPaths paths = new(_root);
        DatabaseBackupService service = new(paths, new DatabaseHealthService());
        BackupResult backup = await service.CreateAsync(database, "tamper");
        using (ZipArchive archive = ZipFile.Open(backup.BackupPath, ZipArchiveMode.Update))
        {
            ZipArchiveEntry entry = archive.GetEntry("data/nexastation.db")!;
            await using Stream stream = entry.Open();
            stream.SetLength(0);
            await stream.WriteAsync(new byte[] { 1, 2, 3 });
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(backup.BackupPath, database));
        Assert.Equal(DatabaseHealthStatus.Healthy, (await new DatabaseHealthService().CheckAsync(database)).Status);
    }

    [Fact]
    public async Task ThreeRecentFailuresRequestSafeMode()
    {
        DateTimeOffset now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        NexaStationPaths paths = new(_root);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            StartupHealthService service = new(paths, () => now.AddMinutes(attempt));
            await service.BeginStartupAsync();
            await service.MarkFailureAsync(StartupStage.DatabaseReady, "test");
        }

        StartupDecision decision = await new StartupHealthService(paths, () => now.AddMinutes(4)).BeginStartupAsync();
        Assert.True(decision.ShouldEnterSafeMode);
        Assert.Equal(3, decision.FailureCount);
    }

    [Fact]
    public async Task HealthyDashboardClearsCrashLoopFailures()
    {
        NexaStationPaths paths = new(_root);
        StartupHealthService first = new(paths);
        await first.BeginStartupAsync();
        await first.MarkFailureAsync(StartupStage.DatabaseReady, "test");
        StartupHealthService second = new(paths);
        await second.BeginStartupAsync();
        await second.MarkStageAsync(StartupStage.DashboardReady);
        StartupDecision decision = await new StartupHealthService(paths).BeginStartupAsync();
        Assert.Equal(0, decision.FailureCount);
    }

    [Fact]
    public async Task ReadyOnboardingIsAHealthyInteractiveStartup()
    {
        NexaStationPaths paths = new(_root);
        StartupHealthService first = new(paths);
        await first.BeginStartupAsync();
        await first.MarkFailureAsync(StartupStage.ServicesReady, "test");
        StartupHealthService second = new(paths);
        await second.BeginStartupAsync();
        await second.MarkStageAsync(StartupStage.OnboardingReady);
        StartupDecision decision = await new StartupHealthService(paths).BeginStartupAsync();
        Assert.Equal(0, decision.FailureCount);
        Assert.False(decision.ShouldEnterSafeMode);
    }

    [Fact]
    public void RedactorRemovesHomePathSecretsAndEmail()
    {
        SensitiveDataRedactor redactor = new(new NexaStationPaths(_root));
        string result = redactor.Redact($"{_root} api_key=abcdef user@example.com Bearer abc.def");
        Assert.DoesNotContain(_root, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abcdef", result, StringComparison.Ordinal);
        Assert.DoesNotContain("user@example.com", result, StringComparison.Ordinal);
        Assert.DoesNotContain("abc.def", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnosticsNeverIncludeTheDatabase()
    {
        NexaStationPaths paths = new(_root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.Database, "private library");
        await File.WriteAllTextAsync(Path.Combine(paths.Logs, "nexastation-test.jsonl"), $"path={_root}; token=secret");
        string archivePath = await new DiagnosticsExportService(paths, new SensitiveDataRedactor(paths)).ExportAsync("0.8.0", "Development", new());
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains(".db", StringComparison.OrdinalIgnoreCase));
        using StreamReader reader = new(archive.GetEntry("logs/nexastation-test.jsonl")!.Open());
        string contents = await reader.ReadToEndAsync();
        Assert.DoesNotContain(_root, contents, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", contents, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        Directory.CreateDirectory(_root);
        string database = Path.Combine(_root, "library.db");
        await ExecuteAsync(database, "CREATE TABLE Items(Id INTEGER PRIMARY KEY, Name TEXT NOT NULL); CREATE TABLE SchemaMigrations(Version INTEGER PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL); INSERT INTO SchemaMigrations VALUES(1,'initial','2026-01-01');");
        return database;
    }

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={database};Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string database, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={database};Pooling=False");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
