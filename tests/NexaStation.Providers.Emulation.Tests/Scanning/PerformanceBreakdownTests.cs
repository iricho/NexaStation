using System.Diagnostics;
using Microsoft.Data.Sqlite;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Identification;
using NexaStation.Providers.Emulation.Scanning;
using Xunit.Abstractions;

namespace NexaStation.Providers.Emulation.Tests.Scanning;

public sealed class PerformanceBreakdownTests(ITestOutputHelper output)
{
    [Theory][InlineData(100)][InlineData(1000)][InlineData(5000)][InlineData(20000)]
    public async Task MeasuresPipelineStages(int count)
    {
        string root=Path.Combine(Path.GetTempPath(),"NexaStation.Emu.Breakdown."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            for(int index=0;index<count;index++)File.WriteAllBytes(Path.Combine(root,$"Homebrew {index:D5} (USA).iso"),[]);
            Stopwatch timer=Stopwatch.StartNew();string[] enumerated=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToArray();timer.Stop();double enumeration=timer.Elapsed.TotalMilliseconds;
            timer.Restart();string[] filtered=enumerated.Where(path=>string.Equals(Path.GetExtension(path),".iso",StringComparison.OrdinalIgnoreCase)).ToArray();timer.Stop();double filtering=timer.Elapsed.TotalMilliseconds;
            FilenameGameIdentifier identifier=new();timer.Restart();List<GameIdentificationResult> identified=[];foreach(string path in filtered)identified.Add(await identifier.IdentifyAsync(new("playstation-2",path,".iso")));timer.Stop();double normalization=timer.Elapsed.TotalMilliseconds;
            HashSet<string> existing=filtered.Take(filtered.Length/2).Select(GameSourceScanner.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);timer.Restart();int matched=filtered.Count(path=>existing.Contains(GameSourceScanner.Normalize(path)));timer.Stop();double matching=timer.Elapsed.TotalMilliseconds;
            timer.Restart();DiscoveredGame[] preview=filtered.Select((path,index)=>{FileInfo info=new(path);return new DiscoveredGame("playstation-2",path,GameSourceScanner.Normalize(path),identified[index].SuggestedTitle,info.Name,info.Extension,info.Length,info.LastWriteTimeUtc,identified[index],existing.Contains(GameSourceScanner.Normalize(path))?EmulatedImportStatus.AlreadyImported:EmulatedImportStatus.New);}).ToArray();timer.Stop();double previewMs=timer.Elapsed.TotalMilliseconds;
            GameSource source=new(){Id="perf",Name="Performance",FolderPath=root,PlatformId="playstation-2",IncludeSubfolders=true,Enabled=true,CreatedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow};FirstProgress first=new();GameSourceScanner scanner=new(identifier);timer.Restart();GameSourceScanResult scan=await scanner.ScanAsync(source,first);timer.Stop();double noChange=timer.Elapsed.TotalMilliseconds;double firstResult=first.FirstAtMilliseconds??timer.Elapsed.TotalMilliseconds;
            string dbPath=Path.Combine(root,"perf.db");await using SqliteConnection connection=new($"Data Source={dbPath};Pooling=False");await connection.OpenAsync();await using(SqliteCommand create=connection.CreateCommand()){create.CommandText="CREATE TABLE Items(Path TEXT PRIMARY KEY,Size INTEGER NOT NULL);";await create.ExecuteNonQueryAsync();}await using(SqliteTransaction transaction=(SqliteTransaction)await connection.BeginTransactionAsync()){await using SqliteCommand insert=connection.CreateCommand();insert.Transaction=transaction;insert.CommandText="INSERT INTO Items VALUES($path,0);";SqliteParameter parameter=insert.Parameters.Add("$path",SqliteType.Text);foreach(string path in filtered){parameter.Value=path;await insert.ExecuteNonQueryAsync();}await transaction.CommitAsync();}timer.Restart();await using(SqliteCommand update=connection.CreateCommand()){update.CommandText="UPDATE Items SET Size=1;";await update.ExecuteNonQueryAsync();}timer.Stop();double database=timer.Elapsed.TotalMilliseconds;await connection.CloseAsync();
            output.WriteLine($"BREAKDOWN files={count} enumerate_ms={enumeration:F3} filter_ms={filtering:F3} normalize_ms={normalization:F3} existing_match_ms={matching:F3} preview_ms={previewMs:F3} no_change_ms={noChange:F3} db_update_ms={database:F3} time_to_first_progress_ms={firstResult:F3}");
            Assert.Equal(count,scan.Games.Count);Assert.Equal(count/2,matched);Assert.Equal(count,preview.Length);
        }
        finally{Directory.Delete(root,true);}
    }

    private sealed class FirstProgress:IProgress<GameSourceScanProgress>
    {
        private readonly Stopwatch _timer=Stopwatch.StartNew();public double? FirstAtMilliseconds{get;private set;}public void Report(GameSourceScanProgress value)=>FirstAtMilliseconds??=_timer.Elapsed.TotalMilliseconds;
    }
}
