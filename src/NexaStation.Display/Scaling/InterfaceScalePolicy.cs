namespace NexaStation.Display.Scaling;

public sealed record InterfaceScaleDecision(bool Valid, double Scale, string? Error = null);

public sealed class InterfaceScalePolicy
{
    public static IReadOnlyList<double> Presets { get; } = [.8, .9, 1, 1.1, 1.25, 1.5];
    public static InterfaceScaleDecision Validate(double scale, int viewportWidth, int viewportHeight)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale is < .8 or > 1.5) return new(false, 1, "Interface scale must be between 80% and 150%.");
        if (viewportWidth / scale < 640 || viewportHeight / scale < 480) return new(false, 1, "That scale would make Settings unreachable at the current resolution.");
        return new(true, Math.Round(scale, 2));
    }
    public static double Recommended(double windowsScale, int width, int height) => Math.Clamp(width >= 3840 && height >= 2160 ? Math.Max(1.1, windowsScale) : windowsScale, .8, 1.5);
}
