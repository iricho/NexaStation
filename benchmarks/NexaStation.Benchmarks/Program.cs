using System.Text.Json;
using NexaStation.Quality.Performance;
using NexaStation.Core;
using NexaStation.Domain;
using Microsoft.Data.Sqlite;
using NexaStation.Infrastructure.Persistence;
using System.Diagnostics;

int iterations = args.Contains("--full", StringComparer.OrdinalIgnoreCase) ? 25 : 7;
DeterministicGameMatchScorer scorer = new();
void Score(int count)
{
    for(int index=0;index<count;index++)
    {
        GameIdentity identity=new($"item-{index}",ContentType.Game,$"synthetic game {index}",$"Synthetic Game {index}","pc",Confidence:IdentityConfidence.PlatformAndTitle);
        _=scorer.Score(identity,new("synthetic",index.ToString(System.Globalization.CultureInfo.InvariantCulture),$"Synthetic Game {index}","pc",null,2020));
    }
}
List<BenchmarkMeasurement> measurements =
[
    DeterministicBenchmarkHarness.Measure("library-create-1000", iterations, () => _ = SyntheticDatasetFactory.Library(1000)),
    DeterministicBenchmarkHarness.Measure("library-create-5000", iterations, () => _ = SyntheticDatasetFactory.Library(5000)),
    DeterministicBenchmarkHarness.Measure("gallery-query-10000", iterations, () => _ = SyntheticDatasetFactory.Gallery(10000).Where(item => item.SourceId == "source-3").OrderByDescending(item => item.CapturedAtUtc).Take(200).ToArray()),
    DeterministicBenchmarkHarness.Measure("smart-collection-5000", iterations, () => _ = SyntheticDatasetFactory.Library(5000).Where(item => item.Favorite || item.PlaytimeSeconds > 25000).OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray()),
    DeterministicBenchmarkHarness.Measure("metadata-score-100", iterations, () => Score(100)),
    DeterministicBenchmarkHarness.Measure("metadata-score-1000", iterations, () => Score(1000)),
    DeterministicBenchmarkHarness.Measure("metadata-score-5000", iterations, () => Score(5000)),
];

string activityRoot=Path.Combine(Path.GetTempPath(),"NexaStation.ActivityBenchmark",Guid.NewGuid().ToString("N"));
SqliteConnectionFactory activityFactory=new(Path.Combine(activityRoot,"activity.db"));
await new DatabaseInitializer(activityFactory,new BenchmarkLogger()).InitializeAsync();
SqlitePhase12Store activityStore=new(activityFactory);
await SeedActivityAsync(activityFactory,0,1000);
double activity1000=await MeasureActivityQueryAsync(activityStore);
await SeedActivityAsync(activityFactory,1000,19000);
double activity20000=await MeasureActivityQueryAsync(activityStore);

Console.WriteLine(JsonSerializer.Serialize(new
{
    formatVersion = 1,
    syntheticDataOnly = true,
    advisory = true,
    machineProfile = "Unclassified local development machine",
    measurements,
    activityTimeline = new { eventCount1000Milliseconds=activity1000, eventCount20000Milliseconds=activity20000, pageSize=100 },
}, new JsonSerializerOptions { WriteIndented = true }));

SqliteConnection.ClearAllPools();
try{Directory.Delete(activityRoot,true);}catch(IOException){}

static async Task SeedActivityAsync(SqliteConnectionFactory factory,int start,int count)
{
    await using SqliteConnection connection=await factory.OpenAsync();
    await using SqliteTransaction transaction=(SqliteTransaction)await connection.BeginTransactionAsync();
    await using SqliteCommand command=connection.CreateCommand();
    command.Transaction=transaction;
    command.CommandText="INSERT INTO ActivityEvents(Id,EventType,TargetType,TargetId,SessionId,TitleSnapshot,OccurredAtUtc,Result,PrivacyLevel,CreatedAtUtc) VALUES($id,0,1,'synthetic',NULL,'Synthetic',$at,0,0,$at);";
    command.Parameters.Add("$id",SqliteType.Text);command.Parameters.Add("$at",SqliteType.Text);
    for(int index=start;index<start+count;index++){command.Parameters["$id"].Value=$"activity-{index:D5}";command.Parameters["$at"].Value=DateTime.UtcNow.AddSeconds(-index).ToString("O",System.Globalization.CultureInfo.InvariantCulture);await command.ExecuteNonQueryAsync();}
    await transaction.CommitAsync();
}

static async Task<double> MeasureActivityQueryAsync(SqlitePhase12Store store)
{
    _=await store.QueryAsync(new ActivityQuery(MaximumResults:100));
    Stopwatch timer=Stopwatch.StartNew();
    for(int index=0;index<7;index++)_=await store.QueryAsync(new ActivityQuery(Offset:index*100,MaximumResults:100));
    timer.Stop();return timer.Elapsed.TotalMilliseconds/7;
}

sealed class BenchmarkLogger:IAppLogger
{
    public void Information(string eventName,string message,IReadOnlyDictionary<string,object?>? properties=null){}
    public void LogError(string eventName,string message,Exception? exception=null,IReadOnlyDictionary<string,object?>? properties=null){}
}
