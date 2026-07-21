using System.Globalization;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Persistence;

public sealed class SqlitePlaySessionRepository(ISqliteConnectionFactory factory, IAppLogger logger) : IPlaySessionRepository
{
    public const long MaximumRecoverySeconds = 4 * 60 * 60;

    public async Task StartSessionAsync(PlaySession session, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteTransaction tx=(SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqliteCommand cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO PlaySessions (Id,LibraryItemId,StartedAtUtc,EndedAtUtc,DurationSeconds,ProcessId,ExitDetectedNormally,FailureMessage,ProviderId,ProviderItemId,ProcessNames,DetectionConfidence,SessionKind,TrackingMode,ForegroundProcessId,ForegroundWindowIdentity) VALUES($id,$item,$start,NULL,NULL,$pid,0,NULL,$provider,$providerItem,$names,$confidence,$kind,$tracking,$foreground,$window); UPDATE LibraryItems SET LastLaunchedAtUtc=$start,UpdatedAtUtc=$start WHERE Id=$item;";cmd.Parameters.AddWithValue("$id",session.Id);cmd.Parameters.AddWithValue("$item",session.LibraryItemId);cmd.Parameters.AddWithValue("$start",session.StartedAtUtc.ToUniversalTime().ToString("O"));cmd.Parameters.AddWithValue("$pid",session.ProcessId is null?DBNull.Value:session.ProcessId);cmd.Parameters.AddWithValue("$provider",session.ProviderId is null?DBNull.Value:session.ProviderId);cmd.Parameters.AddWithValue("$providerItem",session.ProviderItemId is null?DBNull.Value:session.ProviderItemId);cmd.Parameters.AddWithValue("$names",session.ProcessNames is null?DBNull.Value:session.ProcessNames);cmd.Parameters.AddWithValue("$confidence",session.DetectionConfidence is null?DBNull.Value:session.DetectionConfidence);cmd.Parameters.AddWithValue("$kind",(int)session.SessionKind);cmd.Parameters.AddWithValue("$tracking",(int)session.TrackingMode);cmd.Parameters.AddWithValue("$foreground",session.ForegroundProcessId is null?DBNull.Value:session.ForegroundProcessId);cmd.Parameters.AddWithValue("$window",session.ForegroundWindowIdentity is null?DBNull.Value:session.ForegroundWindowIdentity);await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteSessionAsync(string sessionId, DateTime endedAtUtc, long durationSeconds, bool exitDetectedNormally, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteTransaction tx=(SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string? itemId;await using(SqliteCommand find=c.CreateCommand()){find.Transaction=tx;find.CommandText="SELECT LibraryItemId FROM PlaySessions WHERE Id=$id AND EndedAtUtc IS NULL;";find.Parameters.AddWithValue("$id",sessionId);itemId=await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;}
        if(itemId is null){await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);return;}
        await using(SqliteCommand cmd=c.CreateCommand()){cmd.Transaction=tx;cmd.CommandText="UPDATE PlaySessions SET EndedAtUtc=$end,DurationSeconds=$duration,ExitDetectedNormally=$normal WHERE Id=$id AND EndedAtUtc IS NULL; UPDATE LibraryItems SET TotalPlaytimeSeconds=TotalPlaytimeSeconds+$duration,UpdatedAtUtc=$end WHERE Id=$item;";cmd.Parameters.AddWithValue("$end",endedAtUtc.ToUniversalTime().ToString("O"));cmd.Parameters.AddWithValue("$duration",durationSeconds);cmd.Parameters.AddWithValue("$normal",exitDetectedNormally?1:0);cmd.Parameters.AddWithValue("$id",sessionId);cmd.Parameters.AddWithValue("$item",itemId);await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);}await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task FailSessionAsync(string sessionId,string failureMessage,CancellationToken cancellationToken=default){await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText="UPDATE PlaySessions SET FailureMessage=$message WHERE Id=$id;";cmd.Parameters.AddWithValue("$message",failureMessage);cmd.Parameters.AddWithValue("$id",sessionId);await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);}

    public async Task<IReadOnlyList<PlaySession>> GetRecentAsync(int maximumResults,CancellationToken cancellationToken=default){await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText="SELECT * FROM PlaySessions ORDER BY StartedAtUtc DESC LIMIT $max;";cmd.Parameters.AddWithValue("$max",Math.Clamp(maximumResults,1,500));List<PlaySession> result=[];await using SqliteDataReader r=await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);while(await r.ReadAsync(cancellationToken).ConfigureAwait(false))result.Add(new(r.GetString(r.GetOrdinal("Id")),r.GetString(r.GetOrdinal("LibraryItemId")),Parse(r.GetString(r.GetOrdinal("StartedAtUtc"))),TextDate(r,"EndedAtUtc"),Int64(r,"DurationSeconds"),Int32(r,"ProcessId"),r.GetInt32(r.GetOrdinal("ExitDetectedNormally"))!=0,Text(r,"FailureMessage"),Text(r,"ProviderId"),Text(r,"ProviderItemId"),Text(r,"ProcessNames"),Double(r,"DetectionConfidence"),(ActivitySessionKind)r.GetInt32(r.GetOrdinal("SessionKind")),(ApplicationTrackingMode)r.GetInt32(r.GetOrdinal("TrackingMode")),Int32(r,"ForegroundProcessId"),Text(r,"ForegroundWindowIdentity")));return result;}

    public async Task RepairIncompleteSessionsAsync(DateTime applicationStartedAtUtc,CancellationToken cancellationToken=default)
    {
        IReadOnlyList<PlaySession> open=(await GetRecentAsync(500,cancellationToken).ConfigureAwait(false)).Where(s=>s.EndedAtUtc is null).ToArray();
        foreach(PlaySession session in open){long duration=Math.Clamp((long)(applicationStartedAtUtc.ToUniversalTime()-session.StartedAtUtc).TotalSeconds,0,MaximumRecoverySeconds);await CompleteSessionAsync(session.Id,applicationStartedAtUtc,duration,false,cancellationToken).ConfigureAwait(false);await FailSessionAsync(session.Id,"Interrupted when NexaStation was not running.",cancellationToken).ConfigureAwait(false);logger.Information("PlaySessionRepaired",$"Repaired interrupted session {session.Id} with {duration} seconds.");}
    }
    private static DateTime Parse(string value)=>DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
    private static string? Text(SqliteDataReader reader,string name){int i=reader.GetOrdinal(name);return reader.IsDBNull(i)?null:reader.GetString(i);}
    private static DateTime? TextDate(SqliteDataReader reader,string name){string? value=Text(reader,name);return value is null?null:Parse(value);}
    private static long? Int64(SqliteDataReader reader,string name){int i=reader.GetOrdinal(name);return reader.IsDBNull(i)?null:reader.GetInt64(i);}
    private static int? Int32(SqliteDataReader reader,string name){int i=reader.GetOrdinal(name);return reader.IsDBNull(i)?null:reader.GetInt32(i);}
    private static double? Double(SqliteDataReader reader,string name){int i=reader.GetOrdinal(name);return reader.IsDBNull(i)?null:reader.GetDouble(i);}
}
