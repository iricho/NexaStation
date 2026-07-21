namespace NexaStation.EmulatorDiscovery.Definitions;

public sealed record EmulatorDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlySet<string> SupportedPlatforms { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownExecutableNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownProductNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownPublisherNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownPackageIdentifiers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownStartMenuPatterns { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownInstallDirectoryPatterns { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> VersionArguments { get; init; } = [];
    public IReadOnlyList<string> VersionOutputPatterns { get; init; } = [];
    public IReadOnlySet<string> RequiredCompanionFiles { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> OptionalCompanionFiles { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public required string AdapterId { get; init; }
    public string LaunchCapabilityProfile { get; init; } = "DirectGameLaunch";
    public string? IconSource { get; init; }
    public int DetectionPriority { get; init; }
    public bool Enabled { get; init; } = true;
}

public static class BuiltInEmulatorDefinitions
{
    public static IReadOnlyList<EmulatorDefinition> All { get; } =
    [
        Create("dolphin", "Dolphin", "dolphin", ["Dolphin.exe"], ["gamecube", "wii"]),
        Create("pcsx2", "PCSX2", "pcsx2", ["pcsx2-qt.exe", "pcsx2.exe"], ["playstation-2"]),
        Create("ppsspp", "PPSSPP", "ppsspp", ["PPSSPPWindows64.exe", "PPSSPPWindows.exe", "PPSSPP.exe"], ["playstation-portable"]),
        Create("retroarch", "RetroArch", "retroarch", ["retroarch.exe"], ["nes", "snes", "game-boy", "game-boy-color", "game-boy-advance", "nintendo-64", "sega-genesis", "playstation"]),
        Create("rpcs3", "RPCS3", "rpcs3", ["rpcs3.exe"], ["playstation-3-experimental"]),
    ];

    private static EmulatorDefinition Create(string id, string name, string adapter, string[] executables, string[] platforms) => new()
    {
        Id = id, DisplayName = name, AdapterId = adapter, KnownExecutableNames = executables.ToHashSet(StringComparer.OrdinalIgnoreCase), KnownProductNames = new HashSet<string>([name],StringComparer.OrdinalIgnoreCase), KnownStartMenuPatterns = new HashSet<string>([name],StringComparer.OrdinalIgnoreCase), KnownInstallDirectoryPatterns = new HashSet<string>([name],StringComparer.OrdinalIgnoreCase), SupportedPlatforms = platforms.ToHashSet(StringComparer.OrdinalIgnoreCase), VersionArguments = ["--version"], DetectionPriority = 100,
    };
}
