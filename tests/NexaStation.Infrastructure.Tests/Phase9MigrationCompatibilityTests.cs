using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;
using NexaStation.Compatibility.Rules;
using NexaStation.Core;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Quality.CrashFingerprinting;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class Phase9MigrationCompatibilityTests
{
    [Theory]
    [InlineData(25)]
    [InlineData(32)]
    [InlineData(37)]
    public async Task SupportedAlphaSchemasUpgradeToCurrent(int sourceVersion)
    {
        await using MigrationFixture fixture = await MigrationFixture.CreateAsync(sourceVersion);
        await fixture.Initializer.InitializeAsync();
        Assert.Equal(74, await fixture.ScalarAsync("SELECT MAX(Version) FROM SchemaMigrations;"));
        Assert.Equal("ok", await fixture.TextAsync("PRAGMA quick_check;"));
    }

    [Fact]
    public async Task LargeCustomizedDatabaseSurvivesCompatibilityMigration()
    {
        await using MigrationFixture fixture = await MigrationFixture.CreateAsync(32);
        await using (SqliteConnection connection = await fixture.Factory.OpenAsync())
        await using (SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync())
        {
            for (int index = 0; index < 5000; index++)
            {
                await using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO LibraryItems(Id,Title,SortTitle,ContentType,Hidden,Favorite,AddedAtUtc,UpdatedAtUtc,TotalPlaytimeSeconds) VALUES($id,$title,$title,0,0,0,$at,$at,0);";
                command.Parameters.AddWithValue("$id", $"synthetic-{index:D5}"); command.Parameters.AddWithValue("$title", $"Synthetic {index:D5}"); command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        await fixture.Initializer.InitializeAsync();
        Assert.Equal(5000, await fixture.ScalarAsync("SELECT COUNT(*) FROM LibraryItems;"));
        Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='CompatibilityProfiles';"));
    }

    [Fact]
    public async Task CompatibilityAndCrashRecordsRoundTripAfterMigration()
    {
        await using MigrationFixture fixture = await MigrationFixture.CreateAsync(37);
        SqliteCompatibilityStore compatibility = new(fixture.Factory);
        CompatibilityProfile profile = new() { Id = "steam-42", Source = CompatibilityProfileSource.User, TargetMatcher = new() { TargetType = CompatibilityTargetType.SteamGame, ProviderId = "steam", ProviderItemId = "42" }, Rules = new() { Tracking = new() { DetectionTimeoutSeconds = 90 } }, UserModified = true };
        await compatibility.SaveAsync(profile);
        Assert.Equal(90, (await compatibility.GetProfilesAsync()).Single().Rules.Tracking.DetectionTimeoutSeconds);
        SqliteQualityStore quality = new(fixture.Factory);
        CrashFingerprintRecord crash = new("abc", "InvalidOperationException", "NexaStation.Test.Method", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "0.9.0", StartupStage.DatabaseReady, null);
        await quality.UpsertAsync(crash);
        Assert.Equal(1, (await quality.GetAsync("abc"))!.OccurrenceCount);
    }

    [Fact]
    public async Task DisabledProvidersInterfaceSettingsAndGallerySourcesSurviveUpgrade()
    {
        await using MigrationFixture fixture = await MigrationFixture.CreateAsync(32);
        await using (SqliteConnection connection = await fixture.Factory.OpenAsync())
        await using (SqliteCommand command = connection.CreateCommand())
        {
            string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            command.CommandText = """
                UPDATE Providers SET Enabled=0 WHERE Id='steam';
                INSERT INTO Settings(Key,Value,UpdatedAtUtc) VALUES('Interface.Layout','ModernDashboard',$at);
                INSERT INTO GallerySources(Id,Name,FolderPath,NormalizedFolderPath,SourceKind,IncludeSubfolders,Enabled,WatchForChanges,IncludeHiddenFiles,IncludeImages,IncludeVideos,PrivacyLevel,CreatedAtUtc,UpdatedAtUtc)
                VALUES('gallery-fixture','Synthetic Gallery','C:\\Synthetic','c:\\synthetic',0,1,0,0,0,1,1,1,$at,$at);
                """;
            command.Parameters.AddWithValue("$at", now);
            await command.ExecuteNonQueryAsync();
        }

        await fixture.Initializer.InitializeAsync();

        Assert.Equal(0, await fixture.ScalarAsync("SELECT Enabled FROM Providers WHERE Id='steam';"));
        Assert.Equal("ModernDashboard", await fixture.TextAsync("SELECT Value FROM Settings WHERE Key='Interface.Layout';"));
        Assert.Equal(0, await fixture.ScalarAsync("SELECT Enabled FROM GallerySources WHERE Id='gallery-fixture';"));
        Assert.Equal(74, await fixture.ScalarAsync("SELECT MAX(Version) FROM SchemaMigrations;"));
    }

    private sealed class MigrationFixture : IAsyncDisposable
    {
        private readonly string _directory;
        private MigrationFixture(string directory, SqliteConnectionFactory factory) { _directory = directory; Factory = factory; Initializer = new DatabaseInitializer(factory, new NullLogger()); }
        public SqliteConnectionFactory Factory { get; }
        public DatabaseInitializer Initializer { get; }

        public static async Task<MigrationFixture> CreateAsync(int version)
        {
            string directory = Path.Combine(Path.GetTempPath(), "NexaStation.MigrationFixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            SqliteConnectionFactory factory = new(Path.Combine(directory, "fixture.db"));
            await using SqliteConnection connection = await factory.OpenAsync();
            await using (SqliteCommand setup = connection.CreateCommand()) { setup.CommandText = "CREATE TABLE SchemaMigrations(Version INTEGER PRIMARY KEY, Name TEXT NOT NULL, AppliedAtUtc TEXT NOT NULL);"; await setup.ExecuteNonQueryAsync(); }
            foreach ((int number, string name, string sql) in LoadMigrations().Where(value => value.Number <= version))
            {
                await using SqliteCommand migration = connection.CreateCommand(); migration.CommandText = sql; await migration.ExecuteNonQueryAsync();
                migration.CommandText = "INSERT INTO SchemaMigrations(Version,Name,AppliedAtUtc) VALUES($version,$name,$at);"; migration.Parameters.Clear(); migration.Parameters.AddWithValue("$version", number); migration.Parameters.AddWithValue("$name", name); migration.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)); await migration.ExecuteNonQueryAsync();
            }
            return new(directory, factory);
        }

        public async Task<long> ScalarAsync(string sql) { await using SqliteConnection connection = await Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql; return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture); }
        public async Task<string> TextAsync(string sql) { await using SqliteConnection connection = await Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql; return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture)!; }
        public ValueTask DisposeAsync() { try { Directory.Delete(_directory, true); } catch (IOException) { } return ValueTask.CompletedTask; }

        private static IEnumerable<(int Number, string Name, string Sql)> LoadMigrations()
        {
            Assembly assembly = typeof(DatabaseInitializer).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.Contains(".Persistence.Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).Order())
            {
                string file = resource[(resource.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..];
                int number = int.Parse(file[..file.IndexOf('_')], CultureInfo.InvariantCulture);
                using Stream stream = assembly.GetManifestResourceStream(resource)!; using StreamReader reader = new(stream);
                yield return (number, Path.GetFileNameWithoutExtension(file), reader.ReadToEnd());
            }
        }
    }

    private sealed class NullLogger : IAppLogger
    {
        public void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void LogError(string eventName, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) { }
    }
}
