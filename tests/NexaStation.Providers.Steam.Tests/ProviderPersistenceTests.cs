using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Providers.Steam.Tests;

public sealed class ProviderPersistenceTests
{
    [Fact] public async Task MigrationsIncludeLaterProviderVersions(){await using Context c=await Context.CreateAsync();await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="SELECT MAX(Version) FROM SchemaMigrations;";Assert.Equal(74,Convert.ToInt32(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture));}
    [Fact] public async Task SteamProviderIsSeeded(){await using Context c=await Context.CreateAsync();await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="SELECT DisplayName FROM Providers WHERE Id='steam';";Assert.Equal("Steam",await command.ExecuteScalarAsync());}
    [Fact] public async Task ImportsNewSteamItem(){await using Context c=await Context.CreateAsync();ProviderImportResult result=await c.Persistence.ImportAsync("steam",[new(Item(),ImportSelectionAction.Import)]);Assert.Equal(1,result.Summary.Added);Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));}
    [Fact] public async Task ImportCreatesProviderSource(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);ProviderSource source=Assert.Single(await c.Persistence.GetSourcesAsync("steam"));Assert.Equal("4242",source.ProviderItemId);Assert.True(source.Installed);}
    [Fact] public async Task ImportCreatesSteamLaunchTarget(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);LibraryItem item=Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));Assert.Equal("steam://run/4242",item.LaunchTarget!.Uri!.AbsoluteUri.TrimEnd('/'));Assert.Equal("steam",item.LaunchTarget.ProviderId);}
    [Fact] public async Task ReimportDoesNotDuplicate(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);ProviderImportResult second=await c.Persistence.ImportAsync("steam",[new(Item())]);Assert.Equal(1,second.Summary.Unchanged);Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));}
    [Fact] public async Task LinksToManualItemAndPreservesBothTargets(){await using Context c=await Context.CreateAsync();LibraryItem manual=LibraryItem.Create("Fictional Starship",ContentType.Game,new LaunchTarget(LaunchType.Executable,Environment.ProcessPath));await c.Library.AddAsync(manual);await c.Persistence.ImportAsync("steam",[new(Item(),ImportSelectionAction.Link,manual.Id)]);LibraryItem linked=(await c.Library.GetByIdAsync(manual.Id))!;Assert.Equal(2,linked.LaunchTargets.Count);Assert.Contains(linked.LaunchTargets,value=>value.ProviderId=="steam");}
    [Fact] public async Task RefreshPreservesFavoritePlaytimeAndTitleOverride(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);LibraryItem current=Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));await c.Library.UpdateAsync(current with{Title="My Custom Name",Favorite=true,TotalPlaytimeSeconds=999});await c.Persistence.AddMetadataOverrideAsync(current.Id,"Title");await c.Persistence.ApplyRefreshAsync(Scan(Item() with{Title="Provider Rename",SortTitle="Provider Rename",ProviderData=new Dictionary<string,string>{{"buildid","2"}}}));LibraryItem refreshed=(await c.Library.GetByIdAsync(current.Id))!;Assert.Equal("My Custom Name",refreshed.Title);Assert.True(refreshed.Favorite);Assert.Equal(999,refreshed.TotalPlaytimeSeconds);}
    [Fact] public async Task RefreshUpdatesNonOverriddenTitle(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);await c.Persistence.ApplyRefreshAsync(Scan(Item() with{Title="Provider Rename",SortTitle="Provider Rename",ProviderData=new Dictionary<string,string>{{"buildid","2"}}}));Assert.Equal("Provider Rename",Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true))).Title);}
    [Fact] public async Task RefreshPreservesUserArtwork(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);LibraryItem item=Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));string artwork=Path.Combine(c.Root,"custom.png");File.WriteAllBytes(artwork,[1,2,3]);Asset custom=new(Guid.NewGuid().ToString("N"),item.Id,AssetType.Cover,artwork,null,null,3,DateTime.UtcNow);await c.Library.UpdateAsync(item with{CoverAsset=custom});await c.Persistence.AddMetadataOverrideAsync(item.Id,"Cover");await c.Persistence.ApplyRefreshAsync(Scan(Item() with{ProviderData=new Dictionary<string,string>{{"buildid","2"}}}));Assert.Equal(artwork,(await c.Library.GetByIdAsync(item.Id))!.CoverAsset!.LocalPath);}
    [Fact] public async Task RefreshAddsNewProviderArtworkOnce(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);string artwork=Path.Combine(c.Root,"provider-cover.png");File.WriteAllBytes(artwork,[1,2,3]);DiscoveredLibraryItem refreshed=Item()with{ArtworkCandidates=[new(artwork,AssetType.Cover,100,600,900)]};await c.Persistence.ApplyRefreshAsync(Scan(refreshed));await c.Persistence.ApplyRefreshAsync(Scan(refreshed));LibraryItem item=Assert.Single(await c.Library.QueryAsync(new LibraryQuery(IncludeHidden:true)));Assert.Equal(artwork,item.CoverAsset!.LocalPath);await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="SELECT COUNT(*) FROM Assets WHERE LibraryItemId=$item AND AssetType=$type;";command.Parameters.AddWithValue("$item",item.Id);command.Parameters.AddWithValue("$type",(int)AssetType.Cover);Assert.Equal(1,Convert.ToInt32(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture));}
    [Fact] public async Task SuccessfulConnectedScanMarksDisappearedMissing(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);ProviderRefreshResult result=await c.Persistence.ApplyRefreshAsync(Scan());Assert.Equal(1,result.Summary.Missing);Assert.False(Assert.Single(await c.Persistence.GetSourcesAsync("steam")).Installed);}
    [Fact] public async Task DisconnectedLibraryDoesNotMarkMissing(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);ProviderScanResult scan=Scan() with{LibrarySources=[Library(false)]};ProviderRefreshResult result=await c.Persistence.ApplyRefreshAsync(scan);Assert.Equal(0,result.Summary.Missing);Assert.True(Assert.Single(await c.Persistence.GetSourcesAsync("steam")).Installed);}
    [Fact] public async Task PreviewRecognizesAlreadyImported(){await using Context c=await Context.CreateAsync();await c.Persistence.ImportAsync("steam",[new(Item())]);ProviderImportService service=new(new ProviderRegistry([new FakeProvider()]),c.Persistence);ImportPreview preview=await service.CreatePreviewAsync("steam",Scan(Item()));Assert.Equal(ImportCandidateStatus.AlreadyImported,Assert.Single(preview.Candidates).Status);}
    [Fact] public async Task PreviewFindsPossibleTitleDuplicate(){await using Context c=await Context.CreateAsync();await c.Library.AddAsync(LibraryItem.Create("Fictional Starship",ContentType.Game,new LaunchTarget(LaunchType.Executable,Environment.ProcessPath)));ProviderImportService service=new(new ProviderRegistry([new FakeProvider()]),c.Persistence);ImportPreview preview=await service.CreatePreviewAsync("steam",Scan(Item()));Assert.Equal(ImportCandidateStatus.PossibleDuplicate,Assert.Single(preview.Candidates).Status);}

    [Fact] public void LibraryIdentityIsStableAcrossCaseAndTrailingSeparators()
    {
        string root=SteamRoot;
        Assert.Equal(ProviderLibraryIdentity.CreateId("steam",root),ProviderLibraryIdentity.CreateId("STEAM",root.ToUpperInvariant()+Path.DirectorySeparatorChar));
    }

    [Fact] public async Task MultipleSteamLibrariesCreateDistinctRows()
    {
        await using Context c=await Context.CreateAsync();
        ProviderLibrarySource first=Library(SteamRoot),second=Library(Path.Combine(c.Root,"OtherSteamLibrary"));
        await c.Persistence.ApplyRefreshAsync(Scan([first,second]));
        Assert.Equal(2,await SourceRowCountAsync(c));
        Assert.NotEqual(first.Id,second.Id);
    }

    [Fact] public async Task RepeatedRefreshIsIdempotent()
    {
        await using Context c=await Context.CreateAsync();
        await c.Persistence.ApplyRefreshAsync(Scan(Item()));
        int first=await SourceRowCountAsync(c);
        await c.Persistence.ApplyRefreshAsync(Scan(Item()));
        Assert.Equal(first,await SourceRowCountAsync(c));
        Assert.Equal(1,first);
    }

    [Fact] public async Task ChangedLibraryPathCreatesNewStableIdentity()
    {
        await using Context c=await Context.CreateAsync();
        ProviderLibrarySource original=Library(SteamRoot),moved=Library(Path.Combine(c.Root,"MovedSteamLibrary"));
        await c.Persistence.ApplyRefreshAsync(Scan([original]));
        await c.Persistence.ApplyRefreshAsync(Scan([moved]));
        Assert.Equal(2,await SourceRowCountAsync(c));
    }

    [Fact] public async Task ExistingSourceIdIsPreservedAndUsedByImportedItem()
    {
        await using Context c=await Context.CreateAsync();const string existingId="legacy-user-library-id";
        await InsertLibrarySourceAsync(c,existingId,SteamRoot);
        await c.Persistence.ImportAsync("steam",[new(Item())]);
        ProviderSource source=Assert.Single(await c.Persistence.GetSourcesAsync("steam"));
        Assert.Equal(existingId,source.ProviderLibrarySourceId);
        Assert.Equal(1,await SourceRowCountAsync(c));
    }

    [Fact] public async Task LegacySteamAppsPathIsRepairedWithoutDeletingImports()
    {
        await using Context c=await Context.CreateAsync();
        await InsertLibrarySourceAsync(c,LibraryId,Path.Combine(SteamRoot,"steamapps"));
        await c.Persistence.ApplyRefreshAsync(Scan(Item()));
        await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="SELECT Id,Path FROM ProviderLibrarySources;";
        await using SqliteDataReader reader=await command.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal(LibraryId,reader.GetString(0));Assert.Equal(SteamRoot,reader.GetString(1),ignoreCase:true);Assert.False(await reader.ReadAsync());
    }

    [Fact] public async Task ConcurrentRefreshRequestsAreSerialized()
    {
        await using Context c=await Context.CreateAsync();CountingProvider provider=new();ProviderImportService service=new(new ProviderRegistry([provider]),c.Persistence);
        await Task.WhenAll(service.RefreshAsync("steam"),service.RefreshAsync("steam"),service.RefreshAsync("steam"));
        Assert.Equal(1,provider.MaximumConcurrency);Assert.Equal(3,provider.CallCount);Assert.Equal(1,await SourceRowCountAsync(c));
    }

    private static string SteamRoot=>Path.Combine(Path.GetTempPath(),"NexaStationSteamLibrary");
    private static string LibraryId=>ProviderLibraryIdentity.CreateId("steam",SteamRoot);
    private static DiscoveredLibraryItem Item()=>new("steam","4242","Fictional Starship","Fictional Starship",ContentType.Game,Path.Combine(SteamRoot,"steamapps","common","FictionalStarship"),Path.Combine(SteamRoot,"steamapps","appmanifest_4242.acf"),new Uri("steam://run/4242"),true,DateTime.UtcNow,new Dictionary<string,string>{{"buildid","1"}},[],LibraryId);
    private static ProviderLibrarySource Library(bool available=true)=>Library(SteamRoot,available);
    private static ProviderLibrarySource Library(string root,bool available=true)=>new(ProviderLibraryIdentity.CreateId("steam",root),"steam",root,ProviderLibraryIdentity.NormalizePath(root),true,available);
    private static ProviderScanResult Scan(params DiscoveredLibraryItem[] items)=>Scan([Library()],items);
    private static ProviderScanResult Scan(IReadOnlyList<ProviderLibrarySource> libraries,params DiscoveredLibraryItem[] items)=>new("steam",DateTime.UtcNow.AddSeconds(-1),DateTime.UtcNow,null,libraries,items,[],[]);
    private static async Task<int> SourceRowCountAsync(Context c){await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="SELECT COUNT(*) FROM ProviderLibrarySources;";return Convert.ToInt32(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture);}
    private static async Task InsertLibrarySourceAsync(Context c,string id,string path){await using SqliteConnection db=await c.Factory.OpenAsync();await using SqliteCommand command=db.CreateCommand();command.CommandText="INSERT INTO ProviderLibrarySources(Id,ProviderId,Path,NormalizedPath,Enabled,FirstSeenAtUtc,LastSeenAtUtc) VALUES($id,'steam',$path,$normalized,1,$at,$at);";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$path",path);command.Parameters.AddWithValue("$normalized",ProviderLibraryIdentity.NormalizePath(path));command.Parameters.AddWithValue("$at",DateTime.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync();}

    private sealed class FakeProvider : ILibraryProvider
    {
        public string Id=>"steam";public string DisplayName=>"Steam";
        public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderAvailabilityResult("steam",true,null,"ok",false,[]));
        public Task<ProviderScanResult> ScanAsync(ProviderScanRequest request,CancellationToken cancellationToken=default)=>Task.FromResult(Scan());
        public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderValidationResult(true,"ok",[]));
        public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }

    private sealed class CountingProvider : ILibraryProvider
    {
        private int _active;private int _callCount;public int MaximumConcurrency{get;private set;}public int CallCount=>_callCount;public string Id=>"steam";public string DisplayName=>"Steam";
        public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderAvailabilityResult("steam",true,null,"ok",false,[]));
        public async Task<ProviderScanResult> ScanAsync(ProviderScanRequest request,CancellationToken cancellationToken=default){int active=Interlocked.Increment(ref _active);MaximumConcurrency=Math.Max(MaximumConcurrency,active);Interlocked.Increment(ref _callCount);try{await Task.Delay(25,cancellationToken);return Scan();}finally{Interlocked.Decrement(ref _active);}}
        public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderValidationResult(true,"ok",[]));
        public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }

    private sealed class Context : IAsyncDisposable
    {
        private Context(string root,SqliteConnectionFactory factory,SqliteLibraryRepository library,SqliteProviderPersistence persistence){Root=root;Factory=factory;Library=library;Persistence=persistence;}
        public string Root{get;}public SqliteConnectionFactory Factory{get;}public SqliteLibraryRepository Library{get;}public SqliteProviderPersistence Persistence{get;}
        public static async Task<Context> CreateAsync(){string root=Path.Combine(Path.GetTempPath(),"nexastation-provider-tests",Guid.NewGuid().ToString("N"));SqliteConnectionFactory factory=new(Path.Combine(root,"test.db"));FakeLogger logger=new();await new DatabaseInitializer(factory,logger).InitializeAsync();SqliteLibraryRepository library=new(factory);return new(root,factory,library,new SqliteProviderPersistence(factory,library));}
        public ValueTask DisposeAsync(){try{SqliteConnection.ClearAllPools();Directory.Delete(Root,true);}catch(IOException){}catch(UnauthorizedAccessException){}return ValueTask.CompletedTask;}
    }
    private sealed class FakeLogger:IAppLogger{public void Information(string eventName,string message,IReadOnlyDictionary<string,object?>? properties=null){}public void LogError(string eventName,string message,Exception? exception=null,IReadOnlyDictionary<string,object?>? properties=null){}}
}
