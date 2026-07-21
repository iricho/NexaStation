namespace NexaStation.Display.Calibration;

public sealed record SafeAreaInsets(int Left, int Top, int Right, int Bottom)
{
    public static SafeAreaInsets Zero { get; } = new(0, 0, 0, 0);
}

public sealed record DisplayCalibration(
    string DisplayId,
    SafeAreaInsets SafeArea,
    int CalibrationWidth,
    int CalibrationHeight,
    double CalibrationScale,
    double ArtworkOverlayDarkness,
    DateTimeOffset UpdatedAtUtc);

public interface IDisplayCalibrationStore
{
    Task<DisplayCalibration?> GetAsync(string displayId, CancellationToken cancellationToken = default);
    Task SaveAsync(DisplayCalibration calibration, CancellationToken cancellationToken = default);
}

public sealed class SafeAreaCalibrationService(IDisplayCalibrationStore store, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DisplayCalibration> SaveAsync(string displayId, SafeAreaInsets insets, int width, int height, double scale, double overlayDarkness, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayId)) throw new ArgumentException("A stable display identifier is required.", nameof(displayId));
        if (width < 640 || height < 480) throw new ArgumentOutOfRangeException(nameof(width), "Calibration requires a display surface of at least 640×480.");
        if (scale is < .8 or > 1.5) throw new ArgumentOutOfRangeException(nameof(scale));
        int maxHorizontal = (int)Math.Floor(width * .15); int maxVertical = (int)Math.Floor(height * .15);
        SafeAreaInsets bounded = new(Math.Clamp(insets.Left, 0, maxHorizontal), Math.Clamp(insets.Top, 0, maxVertical), Math.Clamp(insets.Right, 0, maxHorizontal), Math.Clamp(insets.Bottom, 0, maxVertical));
        DisplayCalibration calibration = new(displayId, bounded, width, height, scale, Math.Clamp(overlayDarkness, 0, .85), _timeProvider.GetUtcNow());
        await store.SaveAsync(calibration, cancellationToken).ConfigureAwait(false); return calibration;
    }

    public static SafeAreaInsets ScaleForResolution(DisplayCalibration calibration, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        if (width <= 0 || height <= 0) return SafeAreaInsets.Zero;
        double horizontal = width / (double)calibration.CalibrationWidth; double vertical = height / (double)calibration.CalibrationHeight;
        return new((int)Math.Round(calibration.SafeArea.Left * horizontal), (int)Math.Round(calibration.SafeArea.Top * vertical), (int)Math.Round(calibration.SafeArea.Right * horizontal), (int)Math.Round(calibration.SafeArea.Bottom * vertical));
    }
}

public sealed class InMemoryDisplayCalibrationStore : IDisplayCalibrationStore
{
    private readonly Dictionary<string, DisplayCalibration> _values = new(StringComparer.Ordinal);
    public Task<DisplayCalibration?> GetAsync(string displayId, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _values.TryGetValue(displayId, out DisplayCalibration? value); return Task.FromResult(value); }
    public Task SaveAsync(DisplayCalibration calibration, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _values[calibration.DisplayId] = calibration; return Task.CompletedTask; }
}
