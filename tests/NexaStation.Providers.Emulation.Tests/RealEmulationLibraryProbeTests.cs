using Microsoft.Data.Sqlite;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Identification;
using Xunit.Abstractions;

namespace NexaStation.Providers.Emulation.Tests;

public sealed class RealEmulationLibraryProbeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ReadOnlyPs2ArtworkProbeWhenExplicitlyEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("NEXASTATION_REAL_EMULATION_TEST"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string? configuredDatabasePath = Environment.GetEnvironmentVariable("NEXASTATION_REAL_EMULATION_DATABASE");
        string databasePath = string.IsNullOrWhiteSpace(configuredDatabasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexaStation",
                "Data",
                "nexastation.db")
            : Path.GetFullPath(configuredDatabasePath);
        Assert.True(File.Exists(databasePath), $"The NexaStation database was not found at {databasePath}.");

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
        };
        await using SqliteConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT li.Title,
                   egs.GameIdentifier,
                   egs.IdentificationConfidence,
                   COUNT(a.Id) AS ArtworkCount
            FROM EmulatedGameSources egs
            JOIN LibraryItems li ON li.Id = egs.LibraryItemId
            LEFT JOIN Assets a ON a.LibraryItemId = li.Id
            WHERE egs.PlatformId = 'playstation-2' AND egs.Available = 1
            GROUP BY li.Id, li.Title, egs.GameIdentifier, egs.IdentificationConfidence
            ORDER BY ArtworkCount, li.SortTitle
            LIMIT 50;
            """;

        List<(string Title, string? Identifier, int Confidence, int ArtworkCount)> games = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            games.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3)));
        }

        int missingArtwork = games.Count(game => game.ArtworkCount == 0);
        output.WriteLine($"REAL_PS2 sampled={games.Count} missing_artwork={missingArtwork}");
        foreach (var game in games.Take(10))
        {
            output.WriteLine($"PS2 title={game.Title} identifier={game.Identifier ?? "(none)"} confidence={game.Confidence} artwork={game.ArtworkCount}");
        }

        Assert.True(games.Count >= 10, $"Expected at least 10 available PS2 entries for the Phase 11 audit, but found {games.Count}.");
    }

    [Fact]
    public async Task ReadOnlyPs2IdentifierProbeWhenExplicitlyEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("NEXASTATION_REAL_EMULATION_TEST"), "1", StringComparison.Ordinal)) return;
        string databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("NEXASTATION_REAL_EMULATION_DATABASE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexaStation", "Data", "nexastation.db"));
        SqliteConnectionStringBuilder builder = new() { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private };
        await using SqliteConnection connection = new(builder.ConnectionString); await connection.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT GamePath FROM EmulatedGameSources WHERE PlatformId='playstation-2' AND Available=1 ORDER BY GamePath LIMIT 50;";
        List<string> paths = []; await using (SqliteDataReader reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) paths.Add(reader.GetString(0));
        FilenameGameIdentifier identifier = new(); List<GameIdentificationResult> results = []; System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew(); foreach (string path in paths) results.Add(await identifier.IdentifyAsync(new("playstation-2", path, Path.GetExtension(path), 1_048_576))); timer.Stop();
        int serials = results.Count(value => value.GameIdentifier is not null); output.WriteLine($"REAL_PS2_IDENTIFY sampled={results.Count} serials={serials} elapsed_ms={timer.Elapsed.TotalMilliseconds:F2} max_read_per_disc=1048576"); foreach (GameIdentificationResult result in results.Take(10)) output.WriteLine($"IDENTIFIED title={result.SuggestedTitle} serial={result.GameIdentifier ?? "(none)"} confidence={result.Confidence}"); Assert.True(results.Count >= 10);
    }
}
