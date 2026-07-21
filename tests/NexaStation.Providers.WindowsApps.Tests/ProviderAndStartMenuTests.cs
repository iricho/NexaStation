using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.WindowsApps.StartMenu;
using Xunit;

namespace NexaStation.Providers.WindowsApps.Tests;

public sealed class ProviderAndStartMenuTests
{
    [Fact]
    public async Task StartMenuScannerPreservesArgumentBoundariesAndCategoryHint()
    {
        using TempFolder folder=new();string category=Path.Combine(folder.Path,"Utilities");Directory.CreateDirectory(category);string shortcut=Path.Combine(category,"Tool.lnk");await File.WriteAllTextAsync(shortcut,string.Empty);
        StartMenuApplicationScanner scanner=new(new FakeFolders(folder.Path),new FakeLinks(path=>new(true,path,"Tool","C:\\Tools\\tool.exe",["--name","two words"],"--name \"two words\"","C:\\Tools")));
        WindowsApplicationCandidate item=Assert.Single((await scanner.ScanAsync(new())).Candidates);
        Assert.Equal(["--name","two words"],item.Arguments);Assert.Equal("Utilities",item.CategoryHint);Assert.Equal(WindowsLaunchMethod.ShellLink,item.LaunchMethod);
    }

    [Fact]
    public async Task StartMenuScannerReportsUnreadableEntryWithoutFailingBatch()
    {
        using TempFolder folder=new();string shortcut=Path.Combine(folder.Path,"Broken.lnk");await File.WriteAllTextAsync(shortcut,string.Empty);StartMenuApplicationScanner scanner=new(new FakeFolders(folder.Path),new FakeLinks(path=>new(false,path,Error:"bad link")));
        WindowsApplicationScanResult result=await scanner.ScanAsync(new());Assert.Empty(result.Candidates);Assert.Single(result.Warnings);Assert.True(result.CompletedSuccessfully);
    }

    [Fact]
    public async Task StartMenuScannerIgnoresNonLinkFiles()
    {
        using TempFolder folder=new();await File.WriteAllTextAsync(Path.Combine(folder.Path,"note.txt"),"ignored");StartMenuApplicationScanner scanner=new(new FakeFolders(folder.Path),new FakeLinks(_=>throw new InvalidOperationException()));Assert.Empty((await scanner.ScanAsync(new())).Candidates);
    }

    [Fact]
    public async Task StartMenuScannerRespectsSourceSelection()
    {
        RecordingFolders folders=new();await new StartMenuApplicationScanner(folders,new FakeLinks(_=>throw new InvalidOperationException())).ScanAsync(new(false,true));Assert.False(folders.Current);Assert.True(folders.Common);
    }

    [Fact]
    public async Task ProviderProjectsStructuredCandidateIntoGenericContract()
    {
        WindowsApplicationCandidate candidate=new(){ProviderId="windows-start-menu",ProviderItemId="entry",DisplayName="Tool",LaunchMethod=WindowsLaunchMethod.ShellLink,ShortcutPath="C:\\Menu\\Tool.lnk",TargetPath="C:\\Tools\\tool.exe",Arguments=["--safe"],DiscoveryConfidence=ApplicationDiscoveryConfidence.High};WindowsStartMenuProvider provider=new(new StaticScanner(candidate));ProviderScanResult scan=await provider.ScanAsync(new());DiscoveredLibraryItem item=Assert.Single(scan.DiscoveredItems);Assert.Equal("Tool",item.Title);Assert.Equal("0",item.ProviderData["launchMethod"]);Assert.Equal("C:\\Menu\\Tool.lnk",item.ProviderData["shortcutPath"]);
    }

    [Fact]
    public async Task XboxGamesFolderCandidateIsConservativelyImportedAsGame()
    {
        WindowsApplicationCandidate candidate=new(){ProviderId="windows-start-menu",ProviderItemId="game",DisplayName="Example Adventure",StartMenuFolder="Xbox Games",CategoryHint="Games",PackageFamilyName="Publisher.Example_abc",PackageFullName="Publisher.Example_1.0_x64_abc",ApplicationId="Game",LaunchMethod=WindowsLaunchMethod.AppUserModelId,AppUserModelId="Publisher.Example_abc!Game",DiscoveryConfidence=ApplicationDiscoveryConfidence.Verified};
        DiscoveredLibraryItem item=Assert.Single((await new WindowsStartMenuProvider(new StaticScanner(candidate)).ScanAsync(new())).DiscoveredItems);
        Assert.Equal(ContentType.Game,item.ContentType);Assert.Equal("Publisher.Example_abc",item.ProviderData["packageFamilyName"]);Assert.Equal("Game",item.ProviderData["applicationId"]);
    }

    [Theory]
    [InlineData("Xbox")]
    [InlineData("Xbox Game Bar")]
    [InlineData("Gaming Services")]
    public async Task XboxShellUtilitiesRemainApplications(string title)
    {
        WindowsApplicationCandidate candidate=new(){ProviderId="windows-start-menu",ProviderItemId=title,DisplayName=title,StartMenuFolder="Xbox Games",CategoryHint="Games",LaunchMethod=WindowsLaunchMethod.AppUserModelId,AppUserModelId="Microsoft.Xbox!App",DiscoveryConfidence=ApplicationDiscoveryConfidence.Verified};
        DiscoveredLibraryItem item=Assert.Single((await new WindowsStartMenuProvider(new StaticScanner(candidate)).ScanAsync(new())).DiscoveredItems);Assert.Equal(ContentType.Application,item.ContentType);
    }

    [Fact]
    public async Task ProviderAvailabilityReflectsWindowsPlatform()
    {
        WindowsStartMenuProvider provider=new(new StaticScanner());ProviderAvailabilityResult result=await provider.DetectAsync();Assert.Equal(OperatingSystem.IsWindows(),result.Detected);
    }

    [Fact]
    public async Task SyntheticFiveHundredEntryScanCompletesWithinBudget()
    {
        using TempFolder folder=new();for(int i=0;i<500;i++)await File.WriteAllTextAsync(Path.Combine(folder.Path,$"App{i:D3}.lnk"),string.Empty);StartMenuApplicationScanner scanner=new(new FakeFolders(folder.Path),new FakeLinks(path=>new(true,path,Path.GetFileNameWithoutExtension(path),"C:\\Windows\\notepad.exe")));Stopwatch timer=Stopwatch.StartNew();WindowsApplicationScanResult result=await scanner.ScanAsync(new());timer.Stop();Assert.Equal(500,result.Candidates.Count);Assert.True(timer.Elapsed<TimeSpan.FromSeconds(5),$"Scan took {timer.Elapsed}.");
    }

    private sealed class FakeFolders(string root):IStartMenuKnownFolderResolver{public IReadOnlyList<(string ProviderSourceId,string Path)>Resolve(bool currentUser,bool common)=>[("test",root)];}
    private sealed class RecordingFolders:IStartMenuKnownFolderResolver{public bool Current{get;private set;}public bool Common{get;private set;}public IReadOnlyList<(string ProviderSourceId,string Path)>Resolve(bool currentUser,bool common){Current=currentUser;Common=common;return[];}}
    private sealed class FakeLinks(Func<string,ShellLinkReadResult> read):IShellLinkReader{public Task<ShellLinkReadResult>ReadAsync(string shortcutPath,CancellationToken cancellationToken=default)=>Task.FromResult(read(shortcutPath));}
    private sealed class StaticScanner(params WindowsApplicationCandidate[] items):IStartMenuApplicationScanner{public Task<WindowsApplicationScanResult>ScanAsync(StartMenuScanRequest request,CancellationToken cancellationToken=default)=>Task.FromResult(new WindowsApplicationScanResult("windows-start-menu",DateTime.UtcNow,DateTime.UtcNow,items,[],[]));}
    private sealed class TempFolder:IDisposable{public TempFolder(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"nexa-windows-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}public string Path{get;}public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);}}
}
