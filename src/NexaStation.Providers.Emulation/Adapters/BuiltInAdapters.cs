using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Adapters;

public sealed class DolphinAdapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "dolphin";
    public override string DisplayName => "Dolphin";
    public override IReadOnlyCollection<string> KnownExecutableNames => ["Dolphin.exe"];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["gamecube", "wii"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} --batch --exec={GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch | EmulatorCapability.BatchMode | EmulatorCapability.CustomUserDirectory | EmulatorCapability.DiscImageLaunch | EmulatorCapability.ExitAfterGame;
}

public sealed class Pcsx2Adapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "pcsx2";
    public override string DisplayName => "PCSX2";
    public override IReadOnlyCollection<string> KnownExecutableNames => ["pcsx2-qt.exe", "pcsx2.exe"];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["playstation-2"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} -fullscreen -batch -- {GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch | EmulatorCapability.BatchMode | EmulatorCapability.FullscreenArgument | EmulatorCapability.CustomUserDirectory | EmulatorCapability.DiscImageLaunch | EmulatorCapability.ExitAfterGame;
}

public sealed class PpssppAdapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "ppsspp";
    public override string DisplayName => "PPSSPP";
    public override IReadOnlyCollection<string> KnownExecutableNames => ["PPSSPPWindows64.exe", "PPSSPPWindows.exe", "PPSSPP.exe"];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["playstation-portable"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} --fullscreen --pause-menu-exit {GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch | EmulatorCapability.FullscreenArgument | EmulatorCapability.DiscImageLaunch;
}

public sealed class RetroArchAdapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "retroarch";
    public override string DisplayName => "RetroArch";
    public override IReadOnlyCollection<string> KnownExecutableNames => ["retroarch.exe"];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["nes", "snes", "game-boy", "game-boy-color", "game-boy-advance", "nintendo-64", "sega-genesis", "sega-master-system", "playstation", "arcade-generic"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} -L {CORE_PATH} {GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch | EmulatorCapability.CoreSelection | EmulatorCapability.CustomConfiguration;

    public override Task<EmulatorLaunchDefinition> BuildLaunchDefinitionAsync(EmulatorLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Core is null || !request.Core.Available || !File.Exists(request.Core.CorePath)) throw new InvalidOperationException("RetroArch requires an available core for this platform.");
        return base.BuildLaunchDefinitionAsync(request, cancellationToken);
    }
}

public sealed class Rpcs3Adapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "rpcs3";
    public override string DisplayName => "RPCS3 — Experimental";
    public override IReadOnlyCollection<string> KnownExecutableNames => ["rpcs3.exe"];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["playstation-3-experimental"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} {GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch | EmulatorCapability.FolderGameLaunch | EmulatorCapability.TitleIdLaunch;
    protected override IReadOnlyList<string> ProbeArguments => ["--help", "--version"];
    protected override bool RequiresConfirmedTemplate => true;
}

public sealed class CustomEmulatorAdapter(IEmulatorCapabilityProbe probe, IGameIdentifierReader identifier) : EmulatorAdapterBase(probe, identifier)
{
    public override string Id => "custom";
    public override string DisplayName => "Custom Emulator";
    public override IReadOnlyCollection<string> KnownExecutableNames => [];
    public override IReadOnlyCollection<string> SupportedPlatformIds => ["custom"];
    protected override string DefaultTemplate => "{EMULATOR_PATH} {GAME_PATH}";
    protected override EmulatorCapability DefaultCapabilities => EmulatorCapability.DirectGameLaunch;
    protected override IReadOnlyList<string> ProbeArguments => [];
}
