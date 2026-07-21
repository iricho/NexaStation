namespace NexaStation.Compatibility.Rules;

public sealed record LaunchCompatibilityOverrides
{
    public string? LaunchTarget { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyList<string> AdapterApprovedArguments { get; init; } = [];
    public int? StartupTimeoutSeconds { get; init; }
    public int? FrontendHideDelayMilliseconds { get; init; }
    public bool? KeepFrontendVisible { get; init; }
    public bool? UseShellActivation { get; init; }
    public bool? DisableProcessTracking { get; init; }
    public bool? UseManualReturn { get; init; }
}

public sealed record TrackingCompatibilityOverrides
{
    public string? ExpectedExecutableName { get; init; }
    public string? ExpectedRelativeExecutablePath { get; init; }
    public IReadOnlyList<string> IgnoredProcessNames { get; init; } = [];
    public bool? WaitForAllMatchingProcesses { get; init; }
    public bool? WaitForProcessReplacement { get; init; }
    public int? DetectionTimeoutSeconds { get; init; }
    public int? ExitGracePeriodSeconds { get; init; }
    public bool? UseForegroundWindow { get; init; }
    public bool? UseLaunchedProcess { get; init; }
    public bool? UseActivatedPidHint { get; init; }
}

public sealed record WindowCompatibilityOverrides
{
    public int? FullscreenRestoreDelayMilliseconds { get; init; }
    public bool? RestoreMaximizedBorderless { get; init; }
    public bool? DoNotForceForeground { get; init; }
    public bool? RestoreOnlyAfterManualAction { get; init; }
    public string? PreferredMonitorStableId { get; init; }
    public bool? DisableMonitorSwitching { get; init; }
}

public sealed record InputCompatibilityOverrides
{
    public bool? DisableDesktopControllerMode { get; init; }
    public string? ControllerGlyphFamily { get; init; }
    public double? NavigationDeadzone { get; init; }
    public int? RepeatDelayMilliseconds { get; init; }
    public int? RepeatRateMilliseconds { get; init; }
    public bool? DisableAnalogNavigation { get; init; }
    public bool? UseDpadOnly { get; init; }
    public string? ConfirmBackConvention { get; init; }
    public bool? DisableBatteryPolling { get; init; }
}

public sealed record ArtworkCompatibilityOverrides
{
    public int? MaximumArtworkDimension { get; init; }
    public bool? PreferWideHero { get; init; }
    public bool? DisableUpscaling { get; init; }
}

public sealed record PerformanceCompatibilityOverrides
{
    public bool? DisableDynamicBackground { get; init; }
    public bool? PauseThumbnailWork { get; init; }
    public bool? ReduceArtworkResolution { get; init; }
    public bool? DisableBlurWhileRunning { get; init; }
    public int? ProviderRefreshDelaySeconds { get; init; }
}

public sealed record CompatibilityRuleSet
{
    public LaunchCompatibilityOverrides Launch { get; init; } = new();
    public TrackingCompatibilityOverrides Tracking { get; init; } = new();
    public WindowCompatibilityOverrides Window { get; init; } = new();
    public InputCompatibilityOverrides Input { get; init; } = new();
    public ArtworkCompatibilityOverrides Artwork { get; init; } = new();
    public PerformanceCompatibilityOverrides Performance { get; init; } = new();
}
