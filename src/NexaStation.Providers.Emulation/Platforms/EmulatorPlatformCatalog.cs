using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Platforms;

public static class EmulatorPlatformCatalog
{
    public static IReadOnlyList<EmulatorPlatform> BuiltIn { get; } =
    [
        P("gamecube", "GameCube", "GC", "Nintendo", 6, [".iso", ".gcm", ".ciso", ".gcz", ".wia", ".rvz", ".dol", ".elf"], ["dolphin"]),
        P("wii", "Wii", "Wii", "Nintendo", 7, [".iso", ".wbfs", ".ciso", ".gcz", ".wia", ".rvz", ".dol", ".elf"], ["dolphin"]),
        P("playstation-2", "PlayStation 2", "PS2", "Sony", 6, [".iso", ".bin", ".img", ".mdf", ".chd", ".elf"], ["pcsx2"]),
        P("playstation-portable", "PlayStation Portable", "PSP", "Sony", 7, [".iso", ".cso", ".chd", ".pbp", ".elf"], ["ppsspp"]),
        P("nes", "Nintendo Entertainment System", "NES", "Nintendo", 3, [".nes"], ["retroarch"]),
        P("snes", "Super Nintendo Entertainment System", "SNES", "Nintendo", 4, [".sfc", ".smc"], ["retroarch"]),
        P("game-boy", "Game Boy", "GB", "Nintendo", 4, [".gb"], ["retroarch"]),
        P("game-boy-color", "Game Boy Color", "GBC", "Nintendo", 5, [".gbc"], ["retroarch"]),
        P("game-boy-advance", "Game Boy Advance", "GBA", "Nintendo", 6, [".gba"], ["retroarch"]),
        P("nintendo-64", "Nintendo 64", "N64", "Nintendo", 5, [".n64", ".z64", ".v64"], ["retroarch"]),
        P("sega-genesis", "Sega Genesis / Mega Drive", "Genesis", "Sega", 4, [".gen", ".md"], ["retroarch"]),
        P("sega-master-system", "Sega Master System", "SMS", "Sega", 3, [".sms"], ["retroarch"]),
        P("playstation", "PlayStation", "PS1", "Sony", 5, [".bin", ".cue", ".chd", ".pbp"], ["retroarch"]),
        P("arcade-generic", "Arcade (Generic)", "Arcade", null, null, [], ["retroarch", "custom"]),
        P("playstation-3-experimental", "PlayStation 3 (Experimental)", "PS3", "Sony", 7, [".elf", ".self"], ["rpcs3"]),
        P("custom", "Custom Platform", "Custom", null, null, [], ["custom"]),
    ];

    public static EmulatorPlatform? Find(string id) => BuiltIn.FirstOrDefault(platform => string.Equals(platform.Id, id, StringComparison.OrdinalIgnoreCase));

    private static EmulatorPlatform P(string id, string name, string shortName, string? maker, int? generation, IReadOnlyList<string> extensions, IReadOnlyList<string> adapters) =>
        new(id, name, shortName, maker, generation, extensions, adapters);
}
