using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Providers.Steam.Manifests;
using Xunit;

namespace NexaStation.Providers.Steam.Tests;

public sealed class ScannerTests
{
    [Fact]
    public async Task DiscoversOneItemPerManifest()
    {
        using TempSteam steam=new();WriteManifest(steam.Root,4242,"Fictional One","One");WriteManifest(steam.Root,4343,"Fictional Two","Two");
        SteamScanResult result=await Scanner().ScanAsync([Location(steam.Root)]);
        Assert.Equal(2,result.Applications.Count);Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ContinuesAfterMalformedManifest()
    {
        using TempSteam steam=new();WriteManifest(steam.Root,4242,"Fictional One","One");File.WriteAllText(Path.Combine(steam.Root,"steamapps","appmanifest_bad.acf"),"\"AppState\" { \"appid\" \"bad\"");
        SteamScanResult result=await Scanner().ScanAsync([Location(steam.Root)]);
        Assert.Single(result.Applications);Assert.Single(result.Errors);
    }

    [Fact]
    public async Task PrefersInstalledDuplicate()
    {
        using TempSteam steam=new();string second=steam.CreateLibrary("second");WriteManifest(steam.Root,4242,"Fictional One","Missing");WriteManifest(second,4242,"Fictional One","Installed");Directory.CreateDirectory(Path.Combine(second,"steamapps","common","Installed"));
        SteamScanResult result=await Scanner().ScanAsync([Location(steam.Root),Location(second)]);
        Assert.Single(result.Applications);Assert.True(result.Applications[0].Manifest.InstallationExists);Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task ReportsInstalledDuplicateConflict()
    {
        using TempSteam steam=new();string second=steam.CreateLibrary("second");WriteManifest(steam.Root,4242,"Fictional One","One");WriteManifest(second,4242,"Fictional One","Two");Directory.CreateDirectory(Path.Combine(steam.Root,"steamapps","common","One"));Directory.CreateDirectory(Path.Combine(second,"steamapps","common","Two"));
        SteamScanResult result=await Scanner().ScanAsync([Location(steam.Root),Location(second)]);
        Assert.Single(result.Applications);Assert.Contains(result.Warnings,value=>value.Contains("multiple installed",StringComparison.Ordinal));
    }

    [Fact] public async Task SkipsUnavailableLibrary(){using TempSteam steam=new();SteamLibraryLocation unavailable=Location(steam.Root) with{Available=false};SteamScanResult result=await Scanner().ScanAsync([unavailable]);Assert.Empty(result.Applications);Assert.NotEmpty(result.Warnings);}
    [Fact] public async Task CancellationStopsScan(){using TempSteam steam=new();using CancellationTokenSource cancellation=new();cancellation.Cancel();await Assert.ThrowsAsync<OperationCanceledException>(()=>Scanner().ScanAsync([Location(steam.Root)],cancellation.Token));}

    private static SteamApplicationScanner Scanner()=>new(new SteamAppManifestParser(new KeyValuesParser()),2);
    private static SteamLibraryLocation Location(string root)=>new("id-"+Path.GetFileName(root),root,Path.Combine(root,"steamapps"),Path.Combine(root,"steamapps","common"),true,SteamLibrarySource.PrimaryInstallation);
    private static void WriteManifest(string root,long appId,string name,string directory){string content=$"\"AppState\"{{\"appid\"\"{appId}\"\"name\"\"{name}\"\"installdir\"\"{directory}\"\"buildid\"\"1\"}}";File.WriteAllText(Path.Combine(root,"steamapps",$"appmanifest_{appId}.acf"),content);}
}
