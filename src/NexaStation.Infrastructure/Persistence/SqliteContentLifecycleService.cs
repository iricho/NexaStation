using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteContentLifecycleService(ISqliteConnectionFactory factory) : IContentLifecycleService
{
    public async Task<ContentLifecycleStatus> GetStatusAsync(ContentLifecycleTarget target, CancellationToken cancellationToken = default)
    {
        Validate(target);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM ContentLifecycleStatuses WHERE TargetType=$type AND TargetId=$id;";
        command.Parameters.AddWithValue("$type", (int)target.TargetType);
        command.Parameters.AddWithValue("$id", target.TargetId);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null ? ContentLifecycleStatus.Unassigned : (ContentLifecycleStatus)Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<ContentLifecycleEntry>> QueryAsync(ContentLifecycleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        int maximum = Math.Clamp(query.MaximumResults, 1, 10_000);
        List<ContentLifecycleEntry> results = [];
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        List<string> filters = [];
        if (query.TargetType is not null)
        {
            filters.Add("TargetType=$type");
            command.Parameters.AddWithValue("$type", (int)query.TargetType.Value);
        }
        if (query.Status is not null && query.Status != ContentLifecycleStatus.Unassigned)
        {
            filters.Add("Status=$status");
            command.Parameters.AddWithValue("$status", (int)query.Status.Value);
        }
        else if (query.Status == ContentLifecycleStatus.Unassigned)
        {
            return results;
        }
        command.CommandText = $"SELECT TargetType,TargetId,Status,UpdatedAtUtc FROM ContentLifecycleStatuses{(filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters))} ORDER BY UpdatedAtUtc DESC LIMIT $maximum;";
        command.Parameters.AddWithValue("$maximum", maximum);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ContentLifecycleTarget target = new((ContentLifecycleTargetType)reader.GetInt32(0), reader.GetString(1));
            results.Add(new(target, (ContentLifecycleStatus)reader.GetInt32(2), DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return results;
    }

    public async Task SetStatusAsync(ContentLifecycleTarget target, ContentLifecycleStatus status, CancellationToken cancellationToken = default)
    {
        Validate(target);
        if (status == ContentLifecycleStatus.Unassigned)
        {
            await ClearStatusAsync(target, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ContentLifecycleStatuses(TargetType,TargetId,Status,UpdatedAtUtc) VALUES($type,$id,$status,$at) ON CONFLICT(TargetType,TargetId) DO UPDATE SET Status=excluded.Status,UpdatedAtUtc=excluded.UpdatedAtUtc;";
        command.Parameters.AddWithValue("$type", (int)target.TargetType);
        command.Parameters.AddWithValue("$id", target.TargetId);
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$at", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearStatusAsync(ContentLifecycleTarget target, CancellationToken cancellationToken = default)
    {
        Validate(target);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ContentLifecycleStatuses WHERE TargetType=$type AND TargetId=$id;";
        command.Parameters.AddWithValue("$type", (int)target.TargetType);
        command.Parameters.AddWithValue("$id", target.TargetId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ClearStaleTargetsAsync(ContentLifecycleTargetType targetType, IReadOnlyCollection<string> validTargetIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validTargetIds);
        await using SqliteConnection connection = await factory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TargetId FROM ContentLifecycleStatuses WHERE TargetType=$type;";
        command.Parameters.AddWithValue("$type", (int)targetType);
        HashSet<string> valid = new(validTargetIds, StringComparer.Ordinal);
        List<string> stale = [];
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                if (!valid.Contains(reader.GetString(0))) stale.Add(reader.GetString(0));
        int removed = 0;
        foreach (string id in stale)
        {
            await using SqliteCommand delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM ContentLifecycleStatuses WHERE TargetType=$type AND TargetId=$id;";
            delete.Parameters.AddWithValue("$type", (int)targetType);
            delete.Parameters.AddWithValue("$id", id);
            removed += await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return removed;
    }

    private static void Validate(ContentLifecycleTarget target)
    {
        if (!Enum.IsDefined(target.TargetType)) throw new ArgumentOutOfRangeException(nameof(target));
        if (string.IsNullOrWhiteSpace(target.TargetId)) throw new ArgumentException("A lifecycle target ID is required.", nameof(target));
    }
}
