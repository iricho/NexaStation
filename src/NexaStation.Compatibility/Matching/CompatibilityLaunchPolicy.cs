using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Rules;
using NexaStation.Domain;

namespace NexaStation.Compatibility.Matching;

public static class CompatibilityLaunchPolicy
{
    public static CompatibilityTarget CreateTarget(LibraryItem item, LaunchTarget target) => new(
        TargetType(item, target),
        target.ProviderId,
        ProviderItemId(target),
        PlatformId: target.EmulatedPlatformId,
        EmulatorAdapterId: target.EmulatorAdapterId,
        ExecutableIdentity: ExecutableIdentity(target),
        AppUserModelId: target.AppUserModelId,
        ApplicationVersion: target.EmulatorVersion,
        WindowsVersion: Environment.OSVersion.Version.ToString());

    public static LaunchTarget Apply(LaunchTarget target, CompatibilityRuleSet rules)
    {
        LaunchCompatibilityOverrides launch = rules.Launch;
        TrackingCompatibilityOverrides tracking = rules.Tracking;
        string? executable = string.IsNullOrWhiteSpace(launch.LaunchTarget) ? target.ExecutablePath : launch.LaunchTarget;
        IReadOnlyList<string> arguments = target.ArgumentList.Concat(launch.AdapterApprovedArguments).ToArray();
        ApplicationTrackingMode trackingMode = launch.UseManualReturn == true ? ApplicationTrackingMode.ManualReturn
            : launch.DisableProcessTracking == true ? ApplicationTrackingMode.None
            : tracking.UseForegroundWindow == true ? ApplicationTrackingMode.ForegroundWindow
            : tracking.UseActivatedPidHint == true ? ApplicationTrackingMode.ActivatedProcessHint
            : tracking.UseLaunchedProcess == true ? ApplicationTrackingMode.LaunchedProcess
            : target.TrackingMode;
        return target with
        {
            ExecutablePath = executable,
            WorkingDirectory = launch.WorkingDirectory ?? target.WorkingDirectory,
            HideFrontendWhileRunning = launch.KeepFrontendVisible == true ? false : target.HideFrontendWhileRunning,
            ExpectedProcessName = tracking.ExpectedExecutableName
                ?? (string.IsNullOrWhiteSpace(tracking.ExpectedRelativeExecutablePath) ? target.ExpectedProcessName : Path.GetFileNameWithoutExtension(tracking.ExpectedRelativeExecutablePath)),
            ExpectedExecutablePath = tracking.ExpectedRelativeExecutablePath ?? target.ExpectedExecutablePath,
            DetectionTimeoutSeconds = tracking.DetectionTimeoutSeconds ?? launch.StartupTimeoutSeconds ?? target.DetectionTimeoutSeconds,
            ExitGracePeriodSeconds = tracking.ExitGracePeriodSeconds ?? target.ExitGracePeriodSeconds,
            TrackingMode = trackingMode,
            FrontendRestoreDelayMilliseconds = rules.Window.FullscreenRestoreDelayMilliseconds ?? target.FrontendRestoreDelayMilliseconds,
            DoNotForceFrontendForeground = rules.Window.DoNotForceForeground ?? target.DoNotForceFrontendForeground,
            RestoreFrontendManually = rules.Window.RestoreOnlyAfterManualAction ?? target.RestoreFrontendManually,
            ArgumentList = arguments,
        };
    }

    private static CompatibilityTargetType TargetType(LibraryItem item, LaunchTarget target) => target.ProviderId?.ToLowerInvariant() switch
    {
        "steam" => CompatibilityTargetType.SteamGame,
        "emulation" => CompatibilityTargetType.EmulatedGame,
        _ when target.LaunchType == LaunchType.PackagedApplication => CompatibilityTargetType.PackagedApplication,
        _ when target.LaunchType == LaunchType.BrowserApplication => CompatibilityTargetType.Pwa,
        _ when item.ContentType == ContentType.Game => CompatibilityTargetType.StandaloneGame,
        _ when item.ContentType == ContentType.Application => CompatibilityTargetType.DesktopApplication,
        _ => CompatibilityTargetType.Unknown,
    };

    private static string? ProviderItemId(LaunchTarget target)
    {
        if (target.ProviderId?.Equals("steam", StringComparison.OrdinalIgnoreCase) == true) return target.Uri?.AbsolutePath.Trim('/');
        return target.EmulatedGameSourceId ?? target.WindowsApplicationSourceId;
    }

    private static string? ExecutableIdentity(LaunchTarget target)
    {
        string? value = target.ExpectedExecutablePath ?? target.ExecutablePath;
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFileName(value).ToLowerInvariant();
    }
}
