using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteCompatibilityStore(ISqliteConnectionFactory factory) : ICompatibilityProfileRepository, ICompatibilityReportRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CompatibilityProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        List<CompatibilityProfile> profiles = [];
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT RuleJson FROM CompatibilityProfiles ORDER BY Source, UpdatedAtUtc DESC;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            CompatibilityProfile? profile = JsonSerializer.Deserialize<CompatibilityProfile>(reader.GetString(0), JsonOptions);
            if (profile is not null) profiles.Add(profile);
        }
        return profiles;
    }

    public async Task SaveAsync(CompatibilityProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CompatibilityProfiles(Id,TargetType,ProviderId,ProviderItemId,PlatformId,EmulatorAdapterId,ExecutableIdentity,ApplicationVersionConstraint,WindowsVersionConstraint,ProfileVersion,RuleJson,Source,Enabled,UserModified,CreatedAtUtc,UpdatedAtUtc)
            VALUES($id,$targetType,$providerId,$providerItemId,$platformId,$adapterId,$executable,$appVersion,$windowsVersion,$profileVersion,$json,$source,$enabled,$userModified,$created,$updated)
            ON CONFLICT(Id) DO UPDATE SET TargetType=excluded.TargetType,ProviderId=excluded.ProviderId,ProviderItemId=excluded.ProviderItemId,PlatformId=excluded.PlatformId,EmulatorAdapterId=excluded.EmulatorAdapterId,ExecutableIdentity=excluded.ExecutableIdentity,ApplicationVersionConstraint=excluded.ApplicationVersionConstraint,WindowsVersionConstraint=excluded.WindowsVersionConstraint,ProfileVersion=excluded.ProfileVersion,RuleJson=excluded.RuleJson,Source=excluded.Source,Enabled=excluded.Enabled,UserModified=excluded.UserModified,UpdatedAtUtc=excluded.UpdatedAtUtc;
            """;
        AddProfileParameters(command, profile);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceImportedFeedAsync(IReadOnlyList<CompatibilityProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM CompatibilityProfiles WHERE Source=$source AND UserModified=0;";
            delete.Parameters.AddWithValue("$source", (int)CompatibilityProfileSource.ImportedSignedFeed);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (CompatibilityProfile profile in profiles)
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO CompatibilityProfiles(Id,TargetType,ProviderId,ProviderItemId,PlatformId,EmulatorAdapterId,ExecutableIdentity,ApplicationVersionConstraint,WindowsVersionConstraint,ProfileVersion,RuleJson,Source,Enabled,UserModified,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$targetType,$providerId,$providerItemId,$platformId,$adapterId,$executable,$appVersion,$windowsVersion,$profileVersion,$json,$source,$enabled,$userModified,$created,$updated);";
            AddProfileParameters(insert, profile);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteImportedFeedAsync(CancellationToken cancellationToken = default) => await ReplaceImportedFeedAsync([], cancellationToken).ConfigureAwait(false);

    public async Task DeleteUserProfilesAsync(IReadOnlyCollection<string> profileIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileIds);
        if (profileIds.Count == 0) return;
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (string id in profileIds.Distinct(StringComparer.Ordinal))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM CompatibilityProfiles WHERE Id=$id AND Source=$source;";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$source", (int)CompatibilityProfileSource.User);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task ICompatibilityReportRepository.SaveAsync(CompatibilityReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CompatibilityReports(Id,TargetType,TargetIdentity,DisplayName,ProviderId,NexaStationVersion,WindowsVersion,Architecture,LaunchResult,TrackingResult,RestoreResult,InputResult,PerformanceResult,SanitizedNotes,DiagnosticCorrelationId,CreatedAtUtc)
            VALUES($id,$targetType,$identity,$displayName,$providerId,$version,$windows,$architecture,$launch,$tracking,$restore,$input,$performance,$notes,$correlation,$created)
            ON CONFLICT(Id) DO UPDATE SET LaunchResult=excluded.LaunchResult,TrackingResult=excluded.TrackingResult,RestoreResult=excluded.RestoreResult,InputResult=excluded.InputResult,PerformanceResult=excluded.PerformanceResult,SanitizedNotes=excluded.SanitizedNotes,DiagnosticCorrelationId=excluded.DiagnosticCorrelationId;
            """;
        command.Parameters.AddWithValue("$id", report.Id);
        command.Parameters.AddWithValue("$targetType", (int)report.TargetType);
        AddNullable(command, "$identity", report.TargetIdentity);
        AddNullable(command, "$displayName", report.DisplayName);
        AddNullable(command, "$providerId", report.ProviderId);
        command.Parameters.AddWithValue("$version", report.NexaStationVersion);
        AddNullable(command, "$windows", report.WindowsVersion);
        AddNullable(command, "$architecture", report.Architecture);
        command.Parameters.AddWithValue("$launch", (int)report.LaunchResult);
        command.Parameters.AddWithValue("$tracking", (int)report.TrackingResult);
        command.Parameters.AddWithValue("$restore", (int)report.RestoreResult);
        command.Parameters.AddWithValue("$input", (int)report.InputResult);
        command.Parameters.AddWithValue("$performance", (int)report.PerformanceResult);
        AddNullable(command, "$notes", report.UserNotes);
        AddNullable(command, "$correlation", report.DiagnosticCorrelationId);
        command.Parameters.AddWithValue("$created", report.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CompatibilityReport>> GetRecentAsync(int maximumResults, CancellationToken cancellationToken = default)
    {
        List<CompatibilityReport> reports = [];
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id,TargetType,TargetIdentity,DisplayName,ProviderId,NexaStationVersion,WindowsVersion,Architecture,LaunchResult,TrackingResult,RestoreResult,InputResult,PerformanceResult,SanitizedNotes,DiagnosticCorrelationId,CreatedAtUtc FROM CompatibilityReports ORDER BY CreatedAtUtc DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(maximumResults, 0, 1000));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            reports.Add(new CompatibilityReport
            {
                Id = reader.GetString(0), TargetType = (CompatibilityTargetType)reader.GetInt32(1), TargetIdentity = GetNullable(reader, 2), DisplayName = GetNullable(reader, 3), ProviderId = GetNullable(reader, 4), NexaStationVersion = reader.GetString(5), WindowsVersion = GetNullable(reader, 6), Architecture = GetNullable(reader, 7), LaunchResult = (CompatibilityResult)reader.GetInt32(8), TrackingResult = (CompatibilityResult)reader.GetInt32(9), RestoreResult = (CompatibilityResult)reader.GetInt32(10), InputResult = (CompatibilityResult)reader.GetInt32(11), PerformanceResult = (CompatibilityResult)reader.GetInt32(12), UserNotes = GetNullable(reader, 13), DiagnosticCorrelationId = GetNullable(reader, 14), CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(15), CultureInfo.InvariantCulture), PrivacyLevel = CompatibilityPrivacyLevel.Standard,
            });
        }
        return reports;
    }

    private static void AddProfileParameters(SqliteCommand command, CompatibilityProfile profile)
    {
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$targetType", (int)profile.TargetMatcher.TargetType);
        AddNullable(command, "$providerId", profile.TargetMatcher.ProviderId);
        AddNullable(command, "$providerItemId", profile.TargetMatcher.ProviderItemId);
        AddNullable(command, "$platformId", profile.TargetMatcher.PlatformId);
        AddNullable(command, "$adapterId", profile.TargetMatcher.EmulatorAdapterId);
        AddNullable(command, "$executable", profile.TargetMatcher.ExecutableIdentity);
        AddNullable(command, "$appVersion", profile.VersionConstraints.Application);
        AddNullable(command, "$windowsVersion", profile.VersionConstraints.Windows);
        command.Parameters.AddWithValue("$profileVersion", profile.ProfileVersion);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(profile, JsonOptions));
        command.Parameters.AddWithValue("$source", (int)profile.Source);
        command.Parameters.AddWithValue("$enabled", profile.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$userModified", profile.UserModified ? 1 : 0);
        command.Parameters.AddWithValue("$created", profile.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", profile.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void AddNullable(SqliteCommand command, string name, string? value) => command.Parameters.AddWithValue(name, value is null ? DBNull.Value : value);
    private static string? GetNullable(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
