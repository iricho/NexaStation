using System.Globalization;
using Microsoft.Data.Sqlite;
using NexaStation.QuickMenu.Models;
using NexaStation.QuickMenu.Services;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqliteSystemActionHistory(ISqliteConnectionFactory factory):ISystemActionHistory
{
    public async Task RecordAsync(SystemActionHistoryEntry entry,CancellationToken cancellationToken=default){await using SqliteConnection connection=await factory.OpenAsync(cancellationToken);await using SqliteCommand command=connection.CreateCommand();command.CommandText="INSERT INTO SystemActionHistory(Id,ActionType,RequestedAtUtc,CompletedAtUtc,Result,FailureMessage) VALUES($id,$action,$requested,$completed,$result,$failure) ON CONFLICT(Id) DO UPDATE SET CompletedAtUtc=excluded.CompletedAtUtc,Result=excluded.Result,FailureMessage=excluded.FailureMessage;";command.Parameters.AddWithValue("$id",entry.Id);command.Parameters.AddWithValue("$action",(int)entry.ActionType);command.Parameters.AddWithValue("$requested",entry.RequestedAtUtc.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture));command.Parameters.AddWithValue("$completed",entry.CompletedAtUtc is null?DBNull.Value:entry.CompletedAtUtc.Value.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture));command.Parameters.AddWithValue("$result",entry.Result);command.Parameters.AddWithValue("$failure",(object?)entry.FailureMessage??DBNull.Value);await command.ExecuteNonQueryAsync(cancellationToken);}
}
