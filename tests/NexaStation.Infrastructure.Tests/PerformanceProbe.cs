using System.Diagnostics;
using Microsoft.Data.Sqlite;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class PerformanceProbe
{
    [Fact]
    public async Task MeasureSyntheticLibraryQueries()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create();
        await using SqliteConnection connection = await db.Factory.OpenAsync();
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await using SqliteCommand command = connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO LibraryItems(Id,Title,SortTitle,ContentType,AddedAtUtc,UpdatedAtUtc) VALUES($id,$title,$title,1,$at,$at);";
        command.Parameters.Add("$id",SqliteType.Text);command.Parameters.Add("$title",SqliteType.Text);command.Parameters.Add("$at",SqliteType.Text);
        for(int index=0;index<1000;index++){command.Parameters["$id"].Value=$"perf-{index:D4}";command.Parameters["$title"].Value=$"Synthetic {index:D4}";command.Parameters["$at"].Value=DateTime.UtcNow.AddSeconds(-index).ToString("O");await command.ExecuteNonQueryAsync();}
        await transaction.CommitAsync();
        List<string> measurements=[];
        foreach(int count in new[]{100,500,1000}){Stopwatch timer=Stopwatch.StartNew();IReadOnlyList<LibraryItem> items=await db.Library.QueryAsync(new LibraryQuery(MaximumResults:count));timer.Stop();Assert.Equal(count,items.Count);measurements.Add($"{count}={timer.Elapsed.TotalMilliseconds:F2}ms");}
        await File.WriteAllLinesAsync(Path.Combine(AppContext.BaseDirectory,"phase1-performance.txt"),measurements);
    }
}
