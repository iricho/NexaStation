using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Adapters;
using NexaStation.Providers.Emulation.Identification;
using NexaStation.Providers.Emulation.Launching;

namespace NexaStation.Providers.Emulation.Tests.Adapters;

public sealed class AdapterAndTemplateTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),"NexaStation.Emu.Adapter."+Guid.NewGuid().ToString("N"));
    private readonly EmulatorInstallation _installation;
    private readonly EmulatedGameSource _game;
    private readonly EmulatorPlatform _platform=new("custom","Custom","Custom",null,null,[".rom"],["custom"]);

    public AdapterAndTemplateTests(){Directory.CreateDirectory(_directory);string executable=Path.Combine(_directory,"Fake Emulator.exe");File.WriteAllBytes(executable,[0]);string game=Path.Combine(_directory,"Homebrew Game.rom");File.WriteAllBytes(game,[1,2,3]);DateTime now=DateTime.UtcNow;_installation=new(){Id="emu",AdapterId="custom",DisplayName="Fake",ExecutablePath=executable,InstallationDirectory=_directory,Enabled=true,ValidationStatus=EmulatorValidationStatus.ProbeFailed,AddedAtUtc=now,UpdatedAtUtc=now};_game=new(){Id="game",LibraryItemId="item",PlatformId="custom",GamePath=game,NormalizedGamePath=game.ToUpperInvariant(),FirstSeenAtUtc=now,LastSeenAtUtc=now};}

    [Fact] public void TemplateKeepsPathsWithSpacesAsSingleArguments(){IReadOnlyList<string> args=LaunchTemplateRenderer.RenderArguments("{EMULATOR_PATH} --game {GAME_PATH}",new(_installation,_platform,_game));Assert.Equal(["--game",_game.GamePath],args);}
    [Theory][InlineData("{EMULATOR_PATH} {UNKNOWN}")][InlineData("{EMULATOR_PATH} {GAME_PATH} && bad")][InlineData("{GAME_PATH}; bad")][InlineData("{GAME_PATH} | bad")]
    public void RejectsUnsafeTemplates(string template)=>Assert.Throws<ArgumentException>(()=>LaunchTemplateRenderer.Validate(template));
    [Fact] public void RejectsUnmatchedQuotes()=>Assert.Throws<ArgumentException>(()=>LaunchTemplateRenderer.Tokenize("\"broken"));

    [Theory]
    [InlineData("dolphin","--batch","--exec=")]
    [InlineData("pcsx2","-fullscreen","--")]
    [InlineData("ppsspp","--fullscreen","--pause-menu-exit")]
    public async Task BuiltInAdaptersGenerateExpectedArguments(string adapterId,string first,string second)
    {
        IEmulatorAdapter adapter=Create(adapterId);EmulatorInstallation installation=_installation with{AdapterId=adapterId};EmulatorPlatform platform=adapterId switch{"dolphin"=>_platform with{Id="gamecube"},"pcsx2"=>_platform with{Id="playstation-2"},_=>_platform with{Id="playstation-portable"}};EmulatorLaunchDefinition launch=await adapter.BuildLaunchDefinitionAsync(new(installation,platform,_game with{PlatformId=platform.Id}));Assert.Contains(first,launch.ArgumentList);Assert.Contains(launch.ArgumentList,value=>value.StartsWith(second,StringComparison.Ordinal));Assert.DoesNotContain("--escape-exit",launch.ArgumentList);
    }

    [Fact] public async Task RetroArchRequiresCore(){RetroArchAdapter adapter=(RetroArchAdapter)Create("retroarch");await Assert.ThrowsAsync<InvalidOperationException>(()=>adapter.BuildLaunchDefinitionAsync(new(_installation with{AdapterId="retroarch"},_platform with{Id="snes"},_game with{PlatformId="snes"})));}
    [Fact] public async Task RetroArchSeparatesCoreAndGameArguments(){string corePath=Path.Combine(_directory,"snes9x_libretro.dll");File.WriteAllBytes(corePath,[0]);RetroArchCore core=new("core","emu","Snes9x",corePath,Path.GetFileName(corePath),["snes"],false,true,DateTime.UtcNow);RetroArchAdapter adapter=(RetroArchAdapter)Create("retroarch");EmulatorLaunchDefinition launch=await adapter.BuildLaunchDefinitionAsync(new(_installation with{AdapterId="retroarch"},_platform with{Id="snes"},_game with{PlatformId="snes"},Core:core));Assert.Equal(["-L",corePath,_game.GamePath],launch.ArgumentList);}
    [Fact] public async Task Rpcs3RequiresConfirmedTemplate(){Rpcs3Adapter adapter=(Rpcs3Adapter)Create("rpcs3");await Assert.ThrowsAsync<InvalidOperationException>(()=>adapter.BuildLaunchDefinitionAsync(new(_installation with{AdapterId="rpcs3"},_platform with{Id="playstation-3-experimental"},_game with{PlatformId="playstation-3-experimental"})));}
    [Fact] public async Task CustomTemplateUsesArgumentList(){CustomEmulatorAdapter adapter=(CustomEmulatorAdapter)Create("custom");EmulatorLaunchDefinition launch=await adapter.BuildLaunchDefinitionAsync(new(_installation with{ArgumentsOverride="{EMULATOR_PATH} --boot {GAME_PATH}"},_platform,_game));Assert.Equal(["--boot",_game.GamePath],launch.ArgumentList);}

    private static IEmulatorAdapter Create(string id){IEmulatorCapabilityProbe probe=new NoProbe();IGameIdentifierReader reader=new FilenameGameIdentifier();return id switch{"dolphin"=>new DolphinAdapter(probe,reader),"pcsx2"=>new Pcsx2Adapter(probe,reader),"ppsspp"=>new PpssppAdapter(probe,reader),"retroarch"=>new RetroArchAdapter(probe,reader),"rpcs3"=>new Rpcs3Adapter(probe,reader),_=>new CustomEmulatorAdapter(probe,reader)};}
    public void Dispose(){try{Directory.Delete(_directory,true);}catch(IOException){}}
    private sealed class NoProbe:IEmulatorCapabilityProbe{public Task<EmulatorProbeResult> ProbeAsync(string executablePath,EmulatorProbeOptions options,CancellationToken cancellationToken=default)=>Task.FromResult(new EmulatorProbeResult(false,null,null,EmulatorCapability.None,[],["manual"]));}
}
