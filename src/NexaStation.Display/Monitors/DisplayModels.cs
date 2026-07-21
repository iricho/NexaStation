namespace NexaStation.Display.Monitors;

public sealed record DisplayRectangle(int X, int Y, int Width, int Height);
public enum DisplayHdrStatus { Unknown, NotSupported, SupportedInactive, Active }
public enum DisplayConnectionType { Unknown, Internal, Hdmi, DisplayPort, Wireless, Virtual }

public sealed record DisplayDescriptor(
    string Id,
    string FriendlyName,
    DisplayRectangle Bounds,
    DisplayRectangle WorkArea,
    double Scale,
    (int Width, int Height) Resolution,
    bool IsPrimary,
    bool IsCurrent,
    bool IsAvailable,
    double? RefreshRate = null,
    DisplayHdrStatus HdrStatus = DisplayHdrStatus.Unknown,
    DisplayConnectionType ConnectionType = DisplayConnectionType.Unknown);

public sealed record DisplaySelectionResult(bool Succeeded, DisplayDescriptor? Display, bool UsedFallback, string? Message = null);

public interface IDisplaySelectionService
{
    Task<IReadOnlyList<DisplayDescriptor>> GetDisplaysAsync(CancellationToken cancellationToken = default);
    Task<DisplaySelectionResult> SelectAsync(string displayId, CancellationToken cancellationToken = default);
    Task<DisplayDescriptor?> GetPreferredAsync(CancellationToken cancellationToken = default);
}

public interface IDisplayPlatformAdapter
{
    Task<IReadOnlyList<DisplayDescriptor>> EnumerateAsync(CancellationToken cancellationToken = default);
    Task<bool> MoveCurrentWindowAsync(DisplayDescriptor display, CancellationToken cancellationToken = default);
}

public enum PreferredDisplayMode { CurrentDisplay, WindowsPrimary, SpecificDisplay }
public enum FullscreenMode { Windowed, BorderlessFullscreen }
public sealed record DisplayPreferences(
    PreferredDisplayMode PreferredMode = PreferredDisplayMode.CurrentDisplay,
    string? PreferredDisplayId = null,
    bool StartOnPreferredDisplay = false,
    FullscreenMode FullscreenMode = FullscreenMode.BorderlessFullscreen,
    double InterfaceScale = 1,
    bool ReduceEffectsOnBattery = false);

public interface IDisplayPreferenceStore
{
    Task<DisplayPreferences> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DisplayPreferences preferences, CancellationToken cancellationToken = default);
}
