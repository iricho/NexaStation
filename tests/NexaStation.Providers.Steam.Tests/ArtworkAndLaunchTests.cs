using System.Buffers.Binary;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Steam.Artwork;
using NexaStation.Providers.Steam.Launching;
using Xunit;

namespace NexaStation.Providers.Steam.Tests;

public sealed class ArtworkAndLaunchTests
{
    [Fact] public async Task FindsAppIdArtwork(){using TempSteam steam=new();string file=WritePng(steam,"4242_cover",600,900);IReadOnlyList<SteamArtworkCandidate> result=await new SteamArtworkLocator().FindAsync(steam.Root,"4242");Assert.Contains(result,value=>value.Path==file);}
    [Fact] public async Task ClassifiesVerticalAsCover(){using TempSteam steam=new();WritePng(steam,"4242_grid",600,900);Assert.Equal(SteamArtworkType.Cover,(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"))[0].Type);}
    [Fact] public async Task ClassifiesWideAsBackground(){using TempSteam steam=new();WritePng(steam,"4242_grid",1600,900);Assert.Equal(SteamArtworkType.Background,(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"))[0].Type);}
    [Fact] public async Task FilenameClassificationHasPriority(){using TempSteam steam=new();WritePng(steam,"4242_logo",1600,900);Assert.Equal(SteamArtworkType.Logo,(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"))[0].Type);}
    [Fact] public async Task CorruptArtworkIsIgnored(){using TempSteam steam=new();string root=Path.Combine(steam.Root,"appcache","librarycache");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"4242_cover.png"),"not an image");Assert.Empty(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"));}
    [Fact] public async Task CopiesIntoProviderCache(){using TempSteam steam=new();string file=WritePng(steam,"4242_cover",600,900);string cache=Path.Combine(steam.Root,"cache");IReadOnlyList<ArtworkCandidate> result=await new ProviderArtworkCache(cache).CacheAsync("steam","4242",[new(file,SteamArtworkType.Cover,600,900,100)]);Assert.True(File.Exists(result[0].Path));Assert.StartsWith(Path.GetFullPath(cache),result[0].Path,StringComparison.OrdinalIgnoreCase);}
    [Fact] public async Task CacheCopyIsDeterministic(){using TempSteam steam=new();string file=WritePng(steam,"4242_cover",600,900);ProviderArtworkCache cache=new(Path.Combine(steam.Root,"cache"));string first=(await cache.CacheAsync("steam","4242",[new(file,SteamArtworkType.Cover,600,900,100)]))[0].Path;string second=(await cache.CacheAsync("steam","4242",[new(file,SteamArtworkType.Cover,600,900,100)]))[0].Path;Assert.Equal(first,second);}
    [Fact] public async Task UnknownSquareThumbnailIsCachedAsIcon(){using TempSteam steam=new();string file=WritePng(steam,"4242",32,32);ArtworkCandidate cached=Assert.Single(await new ProviderArtworkCache(Path.Combine(steam.Root,"cache")).CacheAsync("steam","4242",[new(file,SteamArtworkType.Unknown,32,32,1)]));Assert.Equal(AssetType.Icon,cached.SuggestedType);}
    [Fact] public async Task FindsNestedLibraryCacheLayoutWithoutRepeatedAppId(){using TempSteam steam=new();string root=Path.Combine(steam.Root,"appcache","librarycache","4242");Directory.CreateDirectory(root);string file=WritePng(root,"library_600x900",600,900);Assert.Contains(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"),value=>value.Path==file);}
    [Fact] public async Task FindsUserGridArtwork(){using TempSteam steam=new();string root=Path.Combine(steam.Root,"userdata","1000","config","grid");Directory.CreateDirectory(root);string file=WritePng(root,"4242p",600,900);Assert.Contains(await new SteamArtworkLocator().FindAsync(steam.Root,"4242"),value=>value.Path==file);}
    [Fact] public async Task HigherResolutionWideArtworkWinsBackgroundSelection(){using TempSteam steam=new();WritePng(steam,"4242_background_small",480,270);string large=WritePng(steam,"4242_background_large",1920,1080);SteamArtworkCandidate selected=(await new SteamArtworkLocator().FindAsync(steam.Root,"4242")).First(value=>value.Type==SteamArtworkType.Background);Assert.Equal(large,selected.Path);}

    [Fact] public void CreatesValidSteamUri(){Assert.Equal("steam://run/4242",new SteamLaunchTargetFactory().CreateRunUri("4242").AbsoluteUri.TrimEnd('/'));}
    [Theory] [InlineData("0")] [InlineData("-1")] [InlineData("abc")] [InlineData("")] public void RejectsInvalidAppId(string value){Assert.Throws<ArgumentException>(()=>new SteamLaunchTargetFactory().CreateRunUri(value));}
    [Fact] public async Task DetectsNewProcessInsideInstallation(){string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"fictional-game"));FakeSnapshots snapshots=new([[new(10,"steam",null,DateTime.UtcNow.AddMinutes(-1))],[new(10,"steam",null,DateTime.UtcNow.AddMinutes(-1)),new(20,"fictional",Path.Combine(root,"fictional.exe"),DateTime.UtcNow)]]);LaunchedApplicationDetectionResult result=await Detect(snapshots,root);Assert.True(result.Detected);Assert.Equal(20,result.Processes[0].ProcessId);}
    [Fact] public async Task IgnoresSteamProcesses(){FakeSnapshots snapshots=new([[new(10,"steam",null,DateTime.UtcNow.AddMinutes(-1))],[new(11,"steamwebhelper",null,DateTime.UtcNow)]]);Assert.False((await Detect(snapshots,null)).Detected);}
    [Fact] public async Task TracksMultipleCandidates(){string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"fictional-multi"));FakeSnapshots snapshots=new([[],[new(20,"game",Path.Combine(root,"game.exe"),DateTime.UtcNow),new(21,"helper",Path.Combine(root,"helper.exe"),DateTime.UtcNow)]]);Assert.Equal(2,(await Detect(snapshots,root)).Processes.Count);}
    [Fact] public async Task RespectsExpectedProcessRule(){FakeSnapshots snapshots=new([[],[new(30,"specialgame",null,DateTime.UtcNow)]]);LaunchedApplicationDetectionResult result=await Detect(snapshots,null,["specialgame"]);Assert.True(result.Detected);Assert.True(result.Confidence>0.9);}
    [Fact] public async Task PersistsHighConfidenceRule(){FakeSettings settings=new();FakeSnapshots snapshots=new([[],[new(30,"specialgame",null,DateTime.UtcNow)]]);SteamLaunchedApplicationDetector detector=new(snapshots,settings);await detector.DetectAsync(Request(null,["specialgame"]));Assert.Equal("specialgame",await settings.GetAsync<string?>("Providers.Steam.ProcessRules.4242",null));}
    [Fact] public async Task DetectionTimeoutIsReadable(){FakeSnapshots snapshots=new([[],[]]);LaunchedApplicationDetectionResult result=await Detect(snapshots,null);Assert.False(result.Detected);Assert.Contains("No new",result.Message,StringComparison.Ordinal);}

    private static Task<LaunchedApplicationDetectionResult> Detect(FakeSnapshots snapshots,string? root,IReadOnlyCollection<string>? expected=null)=>new SteamLaunchedApplicationDetector(snapshots).DetectAsync(Request(root,expected??[]));
    private static LaunchedApplicationDetectionRequest Request(string? root,IReadOnlyCollection<string> expected)=>new(new SteamLaunchTargetFactory().Create("4242",root),DateTime.UtcNow.AddMilliseconds(-10),root,expected,["steam","steamwebhelper"],TimeSpan.FromMilliseconds(30),TimeSpan.FromMilliseconds(1),TimeSpan.Zero);
    private static string WritePng(TempSteam steam,string name,int width,int height)=>WritePng(Path.Combine(steam.Root,"appcache","librarycache"),name,width,height);
    private static string WritePng(string root,string name,int width,int height){Directory.CreateDirectory(root);byte[] bytes=new byte[24];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(bytes,0);BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16,4),width);BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20,4),height);string path=Path.Combine(root,name+".png");File.WriteAllBytes(path,bytes);return path;}

    private sealed class FakeSnapshots(IReadOnlyList<IReadOnlyList<ProcessSnapshot>> captures) : IProcessSnapshotProvider
    {
        private int _index;public IReadOnlyList<ProcessSnapshot> Capture()=>captures[Math.Min(_index++,captures.Count-1)];public RunningProcess? Attach(int processId)=>new(processId,Task.FromResult(0));
    }
    private sealed class FakeSettings : ISettingsService
    {
        private readonly Dictionary<string,object?> _values=[];
        public Task<T> GetAsync<T>(string key,T defaultValue,CancellationToken cancellationToken=default)=>Task.FromResult(_values.TryGetValue(key,out object? value)&&(value is T typed||value is null)?(T?)value??defaultValue:defaultValue);
        public Task SetAsync<T>(string key,T value,CancellationToken cancellationToken=default){_values[key]=value;return Task.CompletedTask;}
    }
}
