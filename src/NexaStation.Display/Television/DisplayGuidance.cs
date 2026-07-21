namespace NexaStation.Display.Television;

public sealed record DisplayCalibrationGuidance(string Title, string Description, bool ChangesWindowsSettings = false);

public static class DisplayGuidance
{
    public static IReadOnlyList<DisplayCalibrationGuidance> Screens { get; } =
    [
        new("Safe Area", "Adjust the frame until every edge is fully visible on your screen."),
        new("Text Readability", "Check small text, card titles, subtitles, navigation labels, and input hints from your normal viewing distance."),
        new("Contrast", "Compare dark and light NexaStation surfaces. Windows color settings are not changed."),
        new("Artwork Brightness", "Adjust only the NexaStation background overlay darkness."),
        new("Focus Visibility", "Compare focused and unfocused cards without relying on color alone."),
        new("Motion", "Preview navigation and enable Reduced Motion if preferred."),
        new("HDR Guidance", "NexaStation uses SDR-safe interface colors and does not enable HDR or change Windows color calibration."),
    ];
}
