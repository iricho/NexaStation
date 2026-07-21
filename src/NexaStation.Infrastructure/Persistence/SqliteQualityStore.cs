using System.Globalization;
using Microsoft.Data.Sqlite;
using NexaStation.Quality.CrashFingerprinting;
using NexaStation.Quality.Performance;
using NexaStation.Quality.Regression;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteQualityStore(ISqliteConnectionFactory factory) : ICrashFingerprintRepository, IPerformanceBaselineRepository, IRegressionCaseRepository
{
    public async Task<CrashFingerprintRecord?> GetAsync(string fingerprint, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Fingerprint,ExceptionType,NormalizedTopFrames,FirstSeenAtUtc,LastSeenAtUtc,OccurrenceCount,LastApplicationVersion,StartupStage,ResolvedVersion FROM CrashFingerprints WHERE Fingerprint=$fingerprint;";
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), ParseDate(reader.GetString(3)), ParseDate(reader.GetString(4)), reader.GetInt32(5), reader.GetString(6), reader.IsDBNull(7) ? null : (StartupStage?)reader.GetInt32(7), reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    public async Task UpsertAsync(CrashFingerprintRecord record, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO CrashFingerprints(Fingerprint,ExceptionType,NormalizedTopFrames,FirstSeenAtUtc,LastSeenAtUtc,OccurrenceCount,LastApplicationVersion,StartupStage,ResolvedVersion) VALUES($fingerprint,$type,$frames,$first,$last,$count,$version,$stage,$resolved) ON CONFLICT(Fingerprint) DO UPDATE SET LastSeenAtUtc=excluded.LastSeenAtUtc,OccurrenceCount=excluded.OccurrenceCount,LastApplicationVersion=excluded.LastApplicationVersion,StartupStage=excluded.StartupStage,ResolvedVersion=excluded.ResolvedVersion;";
        command.Parameters.AddWithValue("$fingerprint", record.Fingerprint); command.Parameters.AddWithValue("$type", record.ExceptionType); command.Parameters.AddWithValue("$frames", record.NormalizedTopFrames); command.Parameters.AddWithValue("$first", Format(record.FirstSeenAtUtc)); command.Parameters.AddWithValue("$last", Format(record.LastSeenAtUtc)); command.Parameters.AddWithValue("$count", record.OccurrenceCount); command.Parameters.AddWithValue("$version", record.LastApplicationVersion); command.Parameters.AddWithValue("$stage", record.StartupStage is null ? DBNull.Value : (int)record.StartupStage.Value); command.Parameters.AddWithValue("$resolved", record.ResolvedVersion is null ? DBNull.Value : record.ResolvedVersion);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkResolvedAsync(string fingerprint, string resolvedVersion, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE CrashFingerprints SET ResolvedVersion=$version WHERE Fingerprint=$fingerprint;"; command.Parameters.AddWithValue("$version", resolvedVersion); command.Parameters.AddWithValue("$fingerprint", fingerprint); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(PerformanceBaseline baseline, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT OR REPLACE INTO PerformanceBaselines(Id,Scenario,MachineProfile,ApplicationVersion,MetricName,MetricValue,Unit,RecordedAtUtc) VALUES($id,$scenario,$machine,$version,$metric,$value,$unit,$at);"; command.Parameters.AddWithValue("$id", baseline.Id); command.Parameters.AddWithValue("$scenario", baseline.Scenario); command.Parameters.AddWithValue("$machine", baseline.MachineProfile.ToString()); command.Parameters.AddWithValue("$version", baseline.ApplicationVersion); command.Parameters.AddWithValue("$metric", baseline.MetricName); command.Parameters.AddWithValue("$value", baseline.MetricValue); command.Parameters.AddWithValue("$unit", baseline.Unit); command.Parameters.AddWithValue("$at", Format(baseline.RecordedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PerformanceBaseline>> GetAsync(string scenario, MachinePerformanceProfile machineProfile, string metricName, CancellationToken cancellationToken = default)
    {
        List<PerformanceBaseline> values = []; await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,Scenario,MachineProfile,ApplicationVersion,MetricName,MetricValue,Unit,RecordedAtUtc FROM PerformanceBaselines WHERE Scenario=$scenario AND MachineProfile=$machine AND MetricName=$metric ORDER BY RecordedAtUtc;"; command.Parameters.AddWithValue("$scenario", scenario); command.Parameters.AddWithValue("$machine", machineProfile.ToString()); command.Parameters.AddWithValue("$metric", metricName); await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) values.Add(new(reader.GetString(0), reader.GetString(1), Enum.Parse<MachinePerformanceProfile>(reader.GetString(2)), reader.GetString(3), reader.GetString(4), reader.GetDouble(5), reader.GetString(6), ParseDate(reader.GetString(7)))); return values;
    }

    public async Task SaveAsync(RegressionCase regressionCase, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT OR REPLACE INTO RegressionCases(Id,Title,Subsystem,SanitizedFixturePath,ReproductionSteps,ExpectedResult,Status,IntroducedVersion,FixedVersion,UpdatedAtUtc) VALUES($id,$title,$subsystem,$fixture,$steps,$expected,$status,$introduced,$fixed,$updated);"; command.Parameters.AddWithValue("$id", regressionCase.Id); command.Parameters.AddWithValue("$title", regressionCase.Title); command.Parameters.AddWithValue("$subsystem", regressionCase.Subsystem); command.Parameters.AddWithValue("$fixture", regressionCase.SanitizedFixturePath); command.Parameters.AddWithValue("$steps", regressionCase.ReproductionSteps); command.Parameters.AddWithValue("$expected", regressionCase.ExpectedResult); command.Parameters.AddWithValue("$status", (int)regressionCase.Status); command.Parameters.AddWithValue("$introduced", regressionCase.IntroducedVersion); command.Parameters.AddWithValue("$fixed", regressionCase.FixedVersion is null ? DBNull.Value : regressionCase.FixedVersion); command.Parameters.AddWithValue("$updated", Format(regressionCase.UpdatedAtUtc)); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RegressionCase>> GetAsync(CancellationToken cancellationToken = default)
    {
        List<RegressionCase> values = []; await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT Id,Title,Subsystem,SanitizedFixturePath,ReproductionSteps,ExpectedResult,Status,IntroducedVersion,FixedVersion,UpdatedAtUtc FROM RegressionCases ORDER BY UpdatedAtUtc DESC;"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false); while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) values.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), (RegressionCaseStatus)reader.GetInt32(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), ParseDate(reader.GetString(9)))); return values;
    }

    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
