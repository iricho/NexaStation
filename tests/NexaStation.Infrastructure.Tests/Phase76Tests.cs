using System.Text;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Artwork;
using NexaStation.Infrastructure.DiscoverApps;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class DiscoverCatalogueTests
{
    [Fact]public async Task EmbeddedCatalogueValidates(){await using Stream stream=typeof(EmbeddedDiscoverAppCatalogue).Assembly.GetManifestResourceStream("NexaStation.Infrastructure.DiscoverApps.discover-apps.v1.json")!;IReadOnlyList<DiscoverAppDefinition> entries=await DiscoverCatalogueParser.ParseAsync(stream);Assert.NotEmpty(entries);Assert.All(entries,value=>Assert.NotEmpty(value.SupportedRegions));Assert.All(entries.Where(value=>value.Category is "Streaming" or "Music"),value=>Assert.False(string.IsNullOrWhiteSpace(value.IconAssetPath)));Assert.Contains(entries,value=>value.Id=="disney-plus"&&value.StoreProductId=="9NXQXXLFST89");Assert.Contains(entries,value=>value.Id=="prime-video"&&value.StoreProductId=="9P6RC76MSMMJ");}
    [Fact]public async Task UnknownFieldsFailSafely(){await using MemoryStream stream=Json("[{\"id\":\"x\",\"displayName\":\"X\",\"description\":\"X\",\"category\":\"Utilities\",\"unknown\":true}]");await Assert.ThrowsAsync<JsonException>(()=>DiscoverCatalogueParser.ParseAsync(stream));}
    [Fact]public async Task DuplicateIdsAreRejected(){await using MemoryStream stream=Json("[{\"id\":\"x\",\"displayName\":\"X\",\"description\":\"X\",\"category\":\"Utilities\"},{\"id\":\"X\",\"displayName\":\"Y\",\"description\":\"Y\",\"category\":\"Utilities\"}]");await Assert.ThrowsAsync<InvalidDataException>(()=>DiscoverCatalogueParser.ParseAsync(stream));}
    [Fact]public async Task HiddenCatalogueEntryStaysHidden(){EmbeddedDiscoverAppCatalogue catalogue=new(new HiddenSettings("netflix"));Assert.DoesNotContain(await catalogue.GetAsync(),value=>value.Id=="netflix");}
    [Fact]public void StoreProductUriIsExact(){Assert.True(StoreNavigationService.TryCreateProductUri("9NZVDKPMR9RD",out Uri? uri));Assert.Equal("ms-windows-store://pdp/?ProductId=9NZVDKPMR9RD",uri!.AbsoluteUri);}
    [Theory][InlineData("")][InlineData("abc")][InlineData("not valid id")][InlineData("123456789012345678901")]public void InvalidStoreProductIdIsRejected(string value){Assert.False(StoreNavigationService.TryCreateProductUri(value,out Uri? uri));Assert.Null(uri);}
    private static MemoryStream Json(string json)=>new(Encoding.UTF8.GetBytes(json));
    private sealed class HiddenSettings(params string[] hidden):ISettingsService{public Task<T> GetAsync<T>(string key,T defaultValue,CancellationToken cancellationToken=default)=>Task.FromResult(key=="DiscoverApps.HiddenIds"?(T)(object)hidden:defaultValue);public Task SetAsync<T>(string key,T value,CancellationToken cancellationToken=default)=>Task.CompletedTask;}
}

public sealed class DiscoverStateMatcherTests
{
    [Fact]public void AumidDetectsInstalledState(){DiscoverAppDefinition definition=Definition()with{KnownAppUserModelIds=["Family!App"]};DiscoverAppState result=DiscoverAppStateMatcher.Resolve(definition,[Source()with{AppUserModelId="family!app"}]);Assert.True(result.IsInstalled);Assert.Equal("item",result.LibraryItemId);}
    [Fact]public void PackageFamilyDetectsInstalledState(){DiscoverAppDefinition definition=Definition()with{KnownPackageFamilyNames=["Example_123"]};Assert.True(DiscoverAppStateMatcher.Resolve(definition,[Source()with{PackageFamilyName="example_123"}]).IsInstalled);}
    [Fact]public void DisplayNameAloneNeverProvesInstallation(){DiscoverAppDefinition definition=Definition();WindowsApplicationSource source=Source()with{ProviderData=new Dictionary<string,string>{{"displayName",definition.DisplayName}}};Assert.Equal(DiscoverAppInstallationState.NotInstalled,DiscoverAppStateMatcher.Resolve(definition,[source]).State);}
    [Fact]public void ExactCatalogueLinkDoesNotNeedDisplayName(){DiscoverAppDefinition definition=Definition();WindowsApplicationSource source=Source()with{ProviderData=new Dictionary<string,string>{{"catalogueId",definition.Id}}};Assert.True(DiscoverAppStateMatcher.Resolve(definition,[source]).IsInstalled);}
    private static DiscoverAppDefinition Definition()=>new(){Id="example",DisplayName="Example",Description="Example app",Category="Utilities",AppKind=DiscoverAppKind.Mixed};
    private static WindowsApplicationSource Source()=>new(){Id="source",LibraryItemId="item",ProviderId="windows-appsfolder",ProviderItemId="identity",SourceType=WindowsApplicationSourceType.PackagedApplication,Installed=true,FirstSeenAtUtc=DateTime.UtcNow,LastSeenAtUtc=DateTime.UtcNow};
}

public sealed class UnifiedArtworkResolverTests
{
    private static readonly byte[] Png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]public async Task UserOverrideWins(){await using Context c=await Context.Create();string image=c.File("selected.png",Png);LibraryItem item=Create(c,"override.exe")with{CoverAsset=new Asset("cover","",AssetType.Cover,image,1,1,Png.Length,DateTime.UtcNow)};item=item with{CoverAsset=item.CoverAsset! with{LibraryItemId=item.Id}};await c.Library.AddAsync(item);await c.Persistence.AddMetadataOverrideAsync(item.Id,"Cover");ArtworkResolutionResult result=await c.Resolver.ResolveAsync(Request(item));Assert.Equal(ArtworkSourceType.UserOverride,result.SourceType);Assert.Equal(image,result.AssetPath);}
    [Fact]public async Task ManualSidecarCoverResolves(){await using Context c=await Context.Create();LibraryItem item=Create(c,"manual.exe");await c.Library.AddAsync(item);string sidecar=c.File("manual-cover.jpg",Png);ArtworkResolutionResult result=await c.Resolver.ResolveAsync(Request(item));Assert.Equal(ArtworkSourceType.Sidecar,result.SourceType);Assert.Equal(sidecar,result.AssetPath);}
    [Fact]public async Task CorruptArtworkFallsBack(){await using Context c=await Context.Create();string image=c.File("broken.png",[1,2,3]);LibraryItem item=Create(c,"broken.exe")with{CoverAsset=new Asset("cover","",AssetType.Cover,image,null,null,3,DateTime.UtcNow)};item=item with{CoverAsset=item.CoverAsset!with{LibraryItemId=item.Id}};await c.Library.AddAsync(item);ArtworkResolutionResult result=await c.Resolver.ResolveAsync(Request(item));Assert.True(result.IsFallback);Assert.NotNull(result.FailureReason);}
    [Fact]public async Task SameFallbackRequestIsStable(){await using Context c=await Context.Create();LibraryItem item=Create(c,"empty.exe");await c.Library.AddAsync(item);ArtworkRequest request=Request(item);ArtworkResolutionResult[] results=await Task.WhenAll(c.Resolver.ResolveAsync(request),c.Resolver.ResolveAsync(request));Assert.Equal(results[0].CacheKey,results[1].CacheKey);Assert.True(results[0].IsFallback);}
    [Fact]public async Task PrivateContentIsExcluded(){await using Context c=await Context.Create();ArtworkResolutionResult result=await c.Resolver.ResolveAsync(new("private",ContentType.Media,"gallery",AssetType.Cover,200,120,IsPrivateContent:true));Assert.True(result.IsFallback);Assert.Contains("Private",result.FailureReason);}
    [Fact]public async Task InvisibleRequestCanCancel(){await using Context c=await Context.Create();using CancellationTokenSource cancellation=new();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>c.Resolver.ResolveAsync(new("missing",ContentType.Game,null,AssetType.Cover,200,120),cancellation.Token));}

    private static ArtworkRequest Request(LibraryItem item)=>new(item.Id,item.ContentType,null,AssetType.Cover,300,420);
    private static LibraryItem Create(Context context,string executable){string path=context.File(executable,[]);return LibraryItem.Create(Path.GetFileNameWithoutExtension(executable),ContentType.Game,new LaunchTarget(LaunchType.Executable,path));}
    private sealed class Context:IAsyncDisposable
    {
        private Context(string root,SqliteConnectionFactory factory,SqliteLibraryRepository library,SqliteProviderPersistence persistence){Root=root;Factory=factory;Library=library;Persistence=persistence;Resolver=new(factory,library);}public string Root{get;}public SqliteConnectionFactory Factory{get;}public SqliteLibraryRepository Library{get;}public SqliteProviderPersistence Persistence{get;}public UnifiedArtworkResolver Resolver{get;}
        public static async Task<Context>Create(){string root=Path.Combine(Path.GetTempPath(),"NexaStationArtworkTests",Guid.NewGuid().ToString("N"));SqliteConnectionFactory factory=new(Path.Combine(root,"test.db"));await new DatabaseInitializer(factory,new Logger()).InitializeAsync();SqliteLibraryRepository library=new(factory);return new(root,factory,library,new(factory,library));}
        public string File(string name,byte[] bytes){string path=Path.Combine(Root,name);Directory.CreateDirectory(Root);System.IO.File.WriteAllBytes(path,bytes);return path;}
        public ValueTask DisposeAsync(){try{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(Root,true);}catch(IOException){}return ValueTask.CompletedTask;}
    }
    private sealed class Logger:IAppLogger{public void Information(string eventName,string message,IReadOnlyDictionary<string,object?>? properties=null){}public void LogError(string eventName,string message,Exception? exception=null,IReadOnlyDictionary<string,object?>? properties=null){}}
}
