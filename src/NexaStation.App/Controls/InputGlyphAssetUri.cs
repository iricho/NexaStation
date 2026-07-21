using NexaStation.Input.Glyphs;

namespace NexaStation.App.Controls;

public static class InputGlyphAssetUri
{
    private static readonly Dictionary<string, string> Assets = new(StringComparer.Ordinal)
    {
        ["InputGlyph.PlayStation.Cross"] = "ms-appx:///Assets/InputGlyphs/PlayStation/ps-cross.png",
        ["InputGlyph.PlayStation.Circle"] = "ms-appx:///Assets/InputGlyphs/PlayStation/ps-circle.png",
        ["InputGlyph.PlayStation.Square"] = "ms-appx:///Assets/InputGlyphs/PlayStation/ps-square.png",
        ["InputGlyph.PlayStation.Triangle"] = "ms-appx:///Assets/InputGlyphs/PlayStation/ps-triangle.png",
        ["InputGlyph.PlayStation.DPad"] = "ms-appx:///Assets/InputGlyphs/PlayStation/ps-dpad.png",
    };

    public static string? Resolve(InputGlyph glyph) => Assets.GetValueOrDefault(glyph.AssetKey);
    public static bool IsRequiredPlayStationAsset(string assetKey) => Assets.ContainsKey(assetKey);
}
