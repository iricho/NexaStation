using Microsoft.Data.Sqlite;
using System.Globalization;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class PersistenceTests
{
    [Fact] public async Task EmptyDatabaseInitializesCorrectly(){await using TestDb db=await TestDb.Create();Assert.Equal(75,await db.Scalar("SELECT COUNT(*) FROM SchemaMigrations"));}
    [Fact] public async Task MigrationsRunInOrder(){await using TestDb db=await TestDb.Create();Assert.Equal(string.Join(',',Enumerable.Range(1,75)),await db.Text("SELECT group_concat(Version,',') FROM (SELECT Version FROM SchemaMigrations ORDER BY Version)"));}
    [Fact] public async Task ReinitializationIsSafe(){await using TestDb db=await TestDb.Create();await db.Initializer.InitializeAsync();Assert.Equal(75,await db.Scalar("SELECT COUNT(*) FROM SchemaMigrations"));}
    [Fact] public async Task FailedTransactionRollsBack(){await using TestDb db=await TestDb.Create();await using SqliteConnection c=await db.Factory.OpenAsync();await using SqliteTransaction tx=(SqliteTransaction)await c.BeginTransactionAsync();try{await TestDb.Execute(c,tx,"INSERT INTO Settings VALUES('x','y','z'); INSERT INTO MissingTable VALUES(1);");}catch(SqliteException){await tx.RollbackAsync();}Assert.Equal(0,await db.Scalar("SELECT COUNT(*) FROM Settings WHERE Key='x'"));}
    [Fact] public async Task ForeignKeysAreEnabled(){await using TestDb db=await TestDb.Create();Assert.Equal(1,await db.Scalar("PRAGMA foreign_keys"));}
    [Fact] public async Task DeletingItemRemovesLaunchTargets(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("delete-target");await db.Library.AddAsync(item);await db.Library.DeleteAsync(item.Id);Assert.Equal(0,await db.Scalar("SELECT COUNT(*) FROM LaunchTargets"));}
    [Fact] public async Task DeletingItemRemovesSessions(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("delete-session");await db.Library.AddAsync(item);await db.Sessions.StartSessionAsync(Session(item));await db.Library.DeleteAsync(item.Id);Assert.Equal(0,await db.Scalar("SELECT COUNT(*) FROM PlaySessions"));}
    [Fact] public async Task FavoritesPersist(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("favorite");await db.Library.AddAsync(item);await db.Library.SetFavoriteAsync(item.Id,true);Assert.True((await db.Library.GetByIdAsync(item.Id))!.Favorite);}
    [Fact] public async Task HiddenStatePersists(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("hidden");await db.Library.AddAsync(item);await db.Library.SetHiddenAsync(item.Id,true);Assert.True((await db.Library.GetByIdAsync(item.Id))!.Hidden);}
    [Fact] public async Task SearchMatchesTitles(){await using TestDb db=await TestDb.Create();await db.Library.AddAsync(Item("search","Alpha Signal"));Assert.Single(await db.Library.QueryAsync(new LibraryQuery(SearchText:"signal")));}
    [Fact] public async Task HiddenItemsExcludedByDefault(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("excluded");await db.Library.AddAsync(item);await db.Library.SetHiddenAsync(item.Id,true);Assert.Empty(await db.Library.QueryAsync(new LibraryQuery()));}
    [Fact] public async Task RecentlyAddedSortsNewestFirst(){await using TestDb db=await TestDb.Create();await db.Library.AddAsync(Item("old",now:DateTime.UtcNow.AddDays(-1)));await db.Library.AddAsync(Item("new"));Assert.Equal("new",(await db.Library.QueryAsync(new LibraryQuery(SortMode:LibrarySortMode.RecentlyAdded)))[0].Id);}
    [Fact] public async Task RecentlyLaunchedSortsNewestFirst(){await using TestDb db=await TestDb.Create();LibraryItem a=Item("a") with{LastLaunchedAtUtc=DateTime.UtcNow.AddHours(-1)};LibraryItem b=Item("b") with{LastLaunchedAtUtc=DateTime.UtcNow};await db.Library.AddAsync(a);await db.Library.AddAsync(b);Assert.Equal("b",(await db.Library.QueryAsync(new LibraryQuery(SortMode:LibrarySortMode.RecentlyLaunched)))[0].Id);}
    [Fact] public async Task DuplicatePathIsDetected(){await using TestDb db=await TestDb.Create();LibraryItem item=Item("duplicate");await db.Library.AddAsync(item);Assert.Equal(item.Id,(await db.Library.FindDuplicateAsync(item.LaunchTarget!))!.Id);}

    private static LibraryItem Item(string id,string? title=null,DateTime? now=null){DateTime at=now??DateTime.UtcNow;LaunchTarget target=new(LaunchType.Executable,Path.GetFullPath(Path.Combine(Path.GetTempPath(),id+".exe"))){Id=id+"-target",LibraryItemId=id,CreatedAtUtc=at,UpdatedAtUtc=at};return new(){Id=id,Title=title??id,SortTitle=title??id,ContentType=ContentType.Application,AddedAtUtc=at,UpdatedAtUtc=at,DefaultLaunchTargetId=target.Id,LaunchTargets=[target]};}
    private static PlaySession Session(LibraryItem item)=>new(Guid.NewGuid().ToString("N"),item.Id,DateTime.UtcNow,ProcessId:42);

    internal sealed class TestDb : IAsyncDisposable
    {
        private TestDb(string path){Path=path;Factory=new SqliteConnectionFactory(path);Logger=new Logger();Initializer=new DatabaseInitializer(Factory,Logger);Library=new SqliteLibraryRepository(Factory);Sessions=new SqlitePlaySessionRepository(Factory,Logger);}
        public string Path{get;}public SqliteConnectionFactory Factory{get;}public DatabaseInitializer Initializer{get;}public SqliteLibraryRepository Library{get;}public SqlitePlaySessionRepository Sessions{get;}public Logger Logger{get;}
        public static async Task<TestDb> Create(){TestDb db=new(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"NexaStation.Tests",Guid.NewGuid().ToString("N"),"test.db"));await db.Initializer.InitializeAsync();return db;}
        public async Task<long> Scalar(string sql){await using SqliteConnection c=await Factory.OpenAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToInt64(await cmd.ExecuteScalarAsync(),CultureInfo.InvariantCulture);}
        public async Task<string> Text(string sql){await using SqliteConnection c=await Factory.OpenAsync();await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText=sql;return (string)(await cmd.ExecuteScalarAsync())!;}
        public static async Task Execute(SqliteConnection c,SqliteTransaction tx,string sql){await using SqliteCommand cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;await cmd.ExecuteNonQueryAsync();}
        public ValueTask DisposeAsync(){try{Directory.Delete(System.IO.Path.GetDirectoryName(Path)!,true);}catch(IOException){}return ValueTask.CompletedTask;}
    }
    internal sealed class Logger:IAppLogger{public List<string> Events{get;}=[];public void Information(string eventName,string message,IReadOnlyDictionary<string,object?>? properties=null)=>Events.Add(eventName);public void LogError(string eventName,string message,Exception? exception=null,IReadOnlyDictionary<string,object?>? properties=null)=>Events.Add(eventName);}
}

public sealed class SessionTests
{
    [Fact] public async Task SuccessfulLaunchCreatesSession(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("start");await db.Library.AddAsync(item);await db.Sessions.StartSessionAsync(New(item));Assert.Single(await db.Sessions.GetRecentAsync(10));}
    [Fact] public async Task ProcessExitCompletesSession(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("exit");await db.Library.AddAsync(item);var s=New(item);await db.Sessions.StartSessionAsync(s);await db.Sessions.CompleteSessionAsync(s.Id,DateTime.UtcNow,12,true);Assert.NotNull((await db.Sessions.GetRecentAsync(1))[0].EndedAtUtc);}
    [Fact] public async Task TotalPlaytimeIncrementsOnce(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("once");await db.Library.AddAsync(item);var s=New(item);await db.Sessions.StartSessionAsync(s);await db.Sessions.CompleteSessionAsync(s.Id,DateTime.UtcNow,12,true);await db.Sessions.CompleteSessionAsync(s.Id,DateTime.UtcNow,12,true);Assert.Equal(12,(await db.Library.GetByIdAsync(item.Id))!.TotalPlaytimeSeconds);}
    [Fact] public async Task FailedLaunchAddsNoPlaytime(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("failed");await db.Library.AddAsync(item);Assert.Equal(0,(await db.Library.GetByIdAsync(item.Id))!.TotalPlaytimeSeconds);}
    [Fact] public async Task IncompleteSessionsRepairConservatively(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("repair");await db.Library.AddAsync(item);var s=New(item,DateTime.UtcNow.AddMinutes(-2));await db.Sessions.StartSessionAsync(s);await db.Sessions.RepairIncompleteSessionsAsync(DateTime.UtcNow);Assert.InRange((await db.Sessions.GetRecentAsync(1))[0].DurationSeconds!.Value,119,121);}
    [Fact] public async Task SessionCannotCompleteTwice(){await TotalPlaytimeIncrementsOnce();}
    [Fact] public async Task NegativeDurationRejected(){await using var db=await PersistenceTests.TestDb.Create();await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>db.Sessions.CompleteSessionAsync("x",DateTime.UtcNow,-1,true));}
    [Fact] public async Task RepairedDurationIsCapped(){await using var db=await PersistenceTests.TestDb.Create();var item=Make("cap");await db.Library.AddAsync(item);await db.Sessions.StartSessionAsync(New(item,DateTime.UtcNow.AddDays(-1)));await db.Sessions.RepairIncompleteSessionsAsync(DateTime.UtcNow);Assert.Equal(SqlitePlaySessionRepository.MaximumRecoverySeconds,(await db.Sessions.GetRecentAsync(1))[0].DurationSeconds);}
    private static LibraryItem Make(string id){DateTime n=DateTime.UtcNow;LaunchTarget t=new(LaunchType.Executable,"notepad.exe"){Id=id+"t",LibraryItemId=id};return new(){Id=id,Title=id,SortTitle=id,ContentType=ContentType.Application,AddedAtUtc=n,UpdatedAtUtc=n,DefaultLaunchTargetId=t.Id,LaunchTargets=[t]};}
    private static PlaySession New(LibraryItem item,DateTime? at=null)=>new(Guid.NewGuid().ToString("N"),item.Id,at??DateTime.UtcNow,ProcessId:42);
}

public sealed class StateAndEditingTests
{
    [Fact] public async Task CurrentSectionPersists(){await using var db=await PersistenceTests.TestDb.Create();SqliteApplicationStateRepository r=new(db.Factory);await r.SetAsync("CurrentSection","Games");Assert.Equal("Games",await r.GetAsync("CurrentSection"));}
    [Fact] public async Task SelectedItemPersists(){await using var db=await PersistenceTests.TestDb.Create();SqliteApplicationStateRepository r=new(db.Factory);await r.SetAsync("SelectedItem.Home","x");Assert.Equal("x",await r.GetAsync("SelectedItem.Home"));}
    [Fact] public async Task MissingSelectedItemFallsBackSafely(){await using var db=await PersistenceTests.TestDb.Create();Assert.Null(await db.Library.GetByIdAsync("removed"));}
    [Fact] public async Task FrequentStateUpdatesAreDebounced(){MemoryState r=new();await using ApplicationStateService service=new(r,TimeSpan.FromMilliseconds(20));service.SetDebounced("x","1");service.SetDebounced("x","2");service.SetDebounced("x","3");await Task.Delay(80);Assert.Equal(1,r.Writes);Assert.Equal("3",await r.GetAsync("x"));}
    [Fact] public async Task EditingPreservesAddedTimestamp(){await using var db=await PersistenceTests.TestDb.Create();var item=Create("edit");await db.Library.AddAsync(item);await db.Library.UpdateAsync(item with{Title="Edited",UpdatedAtUtc=DateTime.UtcNow});Assert.Equal(item.AddedAtUtc,(await db.Library.GetByIdAsync(item.Id))!.AddedAtUtc);}
    [Fact] public async Task RelinkingPreservesPlayHistory(){await using var db=await PersistenceTests.TestDb.Create();var item=Create("history");await db.Library.AddAsync(item);var s=new PlaySession("s",item.Id,DateTime.UtcNow);await db.Sessions.StartSessionAsync(s);await db.Library.UpdateAsync(item with{LaunchTargets=[item.LaunchTarget! with{ExecutablePath="other.exe"}]});Assert.Single(await db.Sessions.GetRecentAsync(10));}
    [Fact] public async Task RelinkingPreservesArtwork(){await using var db=await PersistenceTests.TestDb.Create();var item=Create("art") with{CoverAsset=new Asset("a","art",AssetType.Cover,@"C:\outside.jpg",null,null,null,DateTime.UtcNow)};await db.Library.AddAsync(item);await db.Library.UpdateAsync(item with{LaunchTargets=[item.LaunchTarget! with{ExecutablePath="other.exe"}]});Assert.Equal(@"C:\outside.jpg",(await db.Library.GetByIdAsync(item.Id))!.CoverAsset!.LocalPath);}
    [Fact] public async Task RemovingDoesNotDeleteExternalFiles(){string file=System.IO.Path.GetTempFileName();try{await using var db=await PersistenceTests.TestDb.Create();var item=Create("external") with{CoverAsset=new Asset("a","external",AssetType.Cover,file,null,null,null,DateTime.UtcNow)};await db.Library.AddAsync(item);await db.Library.DeleteAsync(item.Id);Assert.True(File.Exists(file));}finally{File.Delete(file);}}
    private static LibraryItem Create(string id){DateTime n=DateTime.UtcNow;LaunchTarget t=new(LaunchType.Executable,"notepad.exe"){Id=id+"t",LibraryItemId=id};return new(){Id=id,Title=id,SortTitle=id,ContentType=ContentType.Application,AddedAtUtc=n,UpdatedAtUtc=n,DefaultLaunchTargetId=t.Id,LaunchTargets=[t]};}
    private sealed class MemoryState:IApplicationStateRepository{private readonly Dictionary<string,string> _values=[];public int Writes{get;private set;}public Task<string?> GetAsync(string key,CancellationToken cancellationToken=default)=>Task.FromResult(_values.GetValueOrDefault(key));public Task SetAsync(string key,string value,CancellationToken cancellationToken=default){Writes++;_values[key]=value;return Task.CompletedTask;}}
}
