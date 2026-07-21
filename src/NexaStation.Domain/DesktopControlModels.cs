namespace NexaStation.Domain;

[Flags]
public enum DesktopControllerButtons { None = 0, A = 1, B = 2, X = 4, Y = 8, LeftShoulder = 16, RightShoulder = 32, Menu = 64, View = 128, LeftThumb = 256, RightThumb = 512, DPadUp = 1024, DPadDown = 2048, DPadLeft = 4096, DPadRight = 8192 }
public enum DesktopMouseButton { Left, Right, Middle }
public enum DesktopApprovedKey { Escape, Enter, Space, Tab, ShiftTab, Up, Down, Left, Right, Backspace, BrowserBack, BrowserForward, MediaPlayPause, VolumeUp, VolumeDown, Mute }
public enum DesktopControlAction { None, LeftClick, RightClick, MiddleClick, Scroll, Escape, Enter, Space, Tab, ShiftTab, BrowserBack, BrowserForward, MediaPlayPause, VolumeUp, VolumeDown, Mute, Toggle, EmergencyDisable, ReturnToNexaStation }
public enum DesktopControlStopReason { UserDisabled, EmergencyDisabled, ControllerDisconnected, ApplicationExited, FrontendRestored, ProfileDisabled, InjectionFailures, Shutdown, Cancelled }

public sealed record DesktopControlProfile
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; } = true;
    public double CursorSensitivity { get; init; } = 900;
    public double CursorAcceleration { get; init; } = 1.7;
    public double CursorDeadzone { get; init; } = 0.18;
    public double ScrollSensitivity { get; init; } = 480;
    public DesktopControlAction LeftClickAction { get; init; } = DesktopControlAction.LeftClick;
    public DesktopControlAction RightClickAction { get; init; } = DesktopControlAction.RightClick;
    public DesktopControlAction? MiddleClickAction { get; init; }
    public DesktopControlAction ScrollAction { get; init; } = DesktopControlAction.Scroll;
    public DesktopControlAction? BackAction { get; init; } = DesktopControlAction.Escape;
    public DesktopControlAction? ForwardAction { get; init; }
    public DesktopControlAction? EscapeAction { get; init; } = DesktopControlAction.Escape;
    public DesktopControlAction ToggleAction { get; init; } = DesktopControlAction.Toggle;
    public DesktopControlAction EmergencyDisableAction { get; init; } = DesktopControlAction.EmergencyDisable;
    public bool PauseOnPhysicalMouseInput { get; init; } = true;
    public bool UserManaged { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed record DesktopControlAssignment(string LibraryItemId, string DesktopControlProfileId, bool EnableOnLaunch);
public sealed record DesktopControllerReading(bool Connected, double LeftX, double LeftY, double RightX, double RightY, double LeftTrigger, double RightTrigger, DesktopControllerButtons Buttons, DateTime TimestampUtc);
public sealed record DesktopCursorMotion(double DeltaX, double DeltaY);
public sealed record DesktopInputInjectionResult(bool Succeeded, string Message, int? NativeError = null);
public sealed record DesktopControlSessionRequest(string LibraryItemId, int? TargetProcessId, bool IsGame, bool IsEmulator, bool IsElevatedTargetKnown = false);
public sealed record DesktopControlProfileResolution(DesktopControlProfile? Profile, bool EnableOnLaunch);
public sealed record DesktopControlSafetyResult(bool Allowed, string Message);
public sealed record ForegroundApplicationDetectionRequest(DateTime LaunchStartedAtUtc, int? ProcessIdHint, string? ExpectedExecutablePath, string? ExpectedAppUserModelId, string? ExpectedWindowClass, TimeSpan Timeout, TimeSpan PollInterval);
public sealed record ForegroundApplicationDetectionResult(bool Detected, int? ProcessId, string? ExecutablePath, string? AppUserModelId, string? WindowClass, string? WindowTitle, double Confidence, string Message);
