using NexaStation.Customization.Models;

namespace NexaStation.Presentation.Dashboard;

public sealed record ThemeSurfacePalette(string Background, string Surface, string Elevated, string Text, string Muted, string Border)
{
    public static ThemeSurfacePalette Resolve(ThemeType theme, string darkBackground, string? schemeId = null) => theme switch
    {
        ThemeType.Light => new("#F0F3F8", "#FFFFFF", "#E5EAF2", "#142033", "#485A72", "#BCC8D8"),
        ThemeType.HighContrast => new("#000000", "#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF"),
        _ when schemeId is "black-gold" or "custom-black-gold" => new(darkBackground, "#17140F", "#292219", "#FFF6E3", "#C8BDA5", "#665434"),
        _ => new(darkBackground, "#0D2032", "#172C42", "#F2F6FD", "#ADBCD0", "#29465B"),
    };
}
