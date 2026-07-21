using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Providers.Steam.Manifests;
using Xunit;

namespace NexaStation.Providers.Steam.Tests;

public sealed class DiscoveryAndManifestTests
{
    [Fact]
    public async Task IncludesPrimaryLibrary()
    {
        using TempSteam steam=new();SteamLibraryLocator locator=new(new KeyValuesParser());
        IReadOnlyList<SteamLibraryLocation> result=await locator.FindLibrariesAsync(steam.Root);
        Assert.Contains(result,value=>value.Source==SteamLibrarySource.PrimaryInstallation&&value.Available);
    }

    [Fact]
    public async Task ParsesModernLibraryShape()
    {
        using TempSteam steam=new();string secondary=steam.CreateLibrary("secondary");steam.WriteLibraryConfiguration(Fixture("libraryfolders-modern.vdf").Replace("PRIMARY_PATH",Vdf(steam.Root),StringComparison.Ordinal).Replace("SECONDARY_PATH",Vdf(secondary),StringComparison.Ordinal));
        IReadOnlyList<SteamLibraryLocation> result=await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root);
        Assert.Contains(result,value=>string.Equals(value.Path,secondary,StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParsesLegacyFlatLibraryShape()
    {
        using TempSteam steam=new();string secondary=steam.CreateLibrary("legacy");steam.WriteLibraryConfiguration(Fixture("libraryfolders-legacy.vdf").Replace("SECONDARY_PATH",Vdf(secondary),StringComparison.Ordinal));
        IReadOnlyList<SteamLibraryLocation> result=await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root);
        Assert.Equal(2,result.Count);
    }

    [Fact] public async Task DeduplicatesLibraryPaths(){using TempSteam steam=new();steam.WriteLibraryConfiguration($"\"libraryfolders\"{{\"0\"{{\"path\"\"{Vdf(steam.Root)}\"}}\"1\"{{\"path\"\"{Vdf(steam.Root)}\"}}}}");Assert.Single(await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root));}
    [Fact] public async Task KeepsUnavailableLibrary(){using TempSteam steam=new();string missing=Path.Combine(steam.Root,"missing-drive");steam.WriteLibraryConfiguration($"\"libraryfolders\"{{\"1\"{{\"path\"\"{Vdf(missing)}\"}}}}");Assert.Contains(await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root),value=>!value.Available);}
    [Fact] public async Task MalformedConfigurationKeepsPrimary(){using TempSteam steam=new();steam.WriteLibraryConfiguration("\"libraryfolders\" { \"1\"");IReadOnlyList<SteamLibraryLocation> result=await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root);Assert.Contains(result,value=>value.Source==SteamLibrarySource.PrimaryInstallation);}
    [Fact] public void ValidatesSteamStructure(){using TempSteam steam=new();Assert.True(SteamInstallationLocator.IsValid(steam.Root));File.Delete(Path.Combine(steam.Root,"steam.exe"));Assert.False(SteamInstallationLocator.IsValid(steam.Root));}

    [Fact] public void ParsesRequiredManifestFields(){using TempSteam steam=new();SteamAppManifestParseResult r=Parse(steam,Fixture("appmanifest-valid.acf"));Assert.Equal(4242,r.Manifest!.AppId);Assert.Equal("Fictional Starship",r.Manifest.Name);Assert.Equal("FictionalStarship",r.Manifest.InstallDirectoryName);}
    [Fact] public void ParsesBuildAndSize(){using TempSteam steam=new();SteamAppManifest m=Parse(steam,Fixture("appmanifest-valid.acf")).Manifest!;Assert.Equal("98765",m.BuildId);Assert.Equal(123456789,m.SizeOnDisk);Assert.NotNull(m.LastUpdatedUtc);}
    [Fact] public void RejectsNonNumericAppId(){using TempSteam steam=new();SteamAppManifestParseResult r=Parse(steam,Fixture("appmanifest-valid.acf").Replace("4242","oops",StringComparison.Ordinal));Assert.Null(r.Manifest);Assert.Contains(r.Diagnostics,value=>value.Reason.Contains("App ID",StringComparison.Ordinal));}
    [Fact] public void RejectsZeroAppId(){using TempSteam steam=new();Assert.Null(Parse(steam,Fixture("appmanifest-valid.acf").Replace("4242","0",StringComparison.Ordinal)).Manifest);}
    [Fact] public void RejectsEmptyName(){using TempSteam steam=new();Assert.Null(Parse(steam,Fixture("appmanifest-valid.acf").Replace("Fictional Starship",string.Empty,StringComparison.Ordinal)).Manifest);}
    [Fact] public void RejectsTraversal(){using TempSteam steam=new();Assert.Null(Parse(steam,Fixture("appmanifest-valid.acf").Replace("FictionalStarship","..\\escape",StringComparison.Ordinal)).Manifest);}
    [Fact] public void RejectsManifestOutsideSteamApps(){using TempSteam steam=new();SteamAppManifestParser parser=new(new KeyValuesParser());Assert.Null(parser.Parse(Path.Combine(steam.Root,"outside.acf"),Fixture("appmanifest-valid.acf")).Manifest);}
    [Fact] public void ConstructsCommonInstallationPath(){using TempSteam steam=new();SteamAppManifest m=Parse(steam,Fixture("appmanifest-valid.acf")).Manifest!;Assert.Equal(Path.Combine(steam.Root,"steamapps","common","FictionalStarship"),m.InstallationPath);}
    [Fact] public void MissingDirectoryDoesNotInvalidateManifest(){using TempSteam steam=new();SteamAppManifestParseResult r=Parse(steam,Fixture("appmanifest-valid.acf"));Assert.NotNull(r.Manifest);Assert.False(r.Manifest!.InstallationExists);}
    [Fact] public void ExistingDirectoryMarksInstalled(){using TempSteam steam=new();Directory.CreateDirectory(Path.Combine(steam.Root,"steamapps","common","FictionalStarship"));Assert.True(Parse(steam,Fixture("appmanifest-valid.acf")).Manifest!.InstallationExists);}
    [Fact] public void MalformedManifestReturnsDiagnostics(){using TempSteam steam=new();SteamAppManifestParseResult r=Parse(steam,Fixture("appmanifest-malformed.acf"));Assert.Null(r.Manifest);Assert.NotEmpty(r.Diagnostics);}

    private static SteamAppManifestParseResult Parse(TempSteam steam,string content)=>new SteamAppManifestParser(new KeyValuesParser()).Parse(Path.Combine(steam.Root,"steamapps","appmanifest_4242.acf"),content);
    private static string Fixture(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
    private static string Vdf(string path)=>path.Replace("\\","\\\\",StringComparison.Ordinal);
}

internal sealed class TempSteam : IDisposable
{
    public TempSteam(){Root=Path.Combine(Path.GetTempPath(),"nexastation-steam-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(Root,"steamapps","common"));File.WriteAllText(Path.Combine(Root,"steam.exe"),string.Empty);}
    public string Root{get;}
    public string CreateLibrary(string name){string path=Path.Combine(Root,name);Directory.CreateDirectory(Path.Combine(path,"steamapps","common"));return path;}
    public void WriteLibraryConfiguration(string content)=>File.WriteAllText(Path.Combine(Root,"steamapps","libraryfolders.vdf"),content);
    public void Dispose(){try{Directory.Delete(Root,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}
