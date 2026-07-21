using NexaStation.Domain;

namespace NexaStation.Core;

public static class LaunchTargetValidator
{
    public static string? Validate(LaunchTarget? target)
    {
        if (target is null) return "This item does not have a launch target.";
        if (!target.IsEnabled) return "This provider installation is currently unavailable.";
        if (target.LaunchType is LaunchType.Executable && (string.IsNullOrWhiteSpace(target.ExecutablePath) || (Path.IsPathFullyQualified(target.ExecutablePath) && !File.Exists(target.ExecutablePath))))
            return "This installation could not be located.";
        if (target.LaunchType is LaunchType.WindowsShortcut && (string.IsNullOrWhiteSpace(target.ShortcutPath) || !File.Exists(target.ShortcutPath)))
            return "This installation could not be located.";
        if (target.LaunchType is LaunchType.WebApplication && (target.Uri is null || (target.Uri.Scheme != Uri.UriSchemeHttp && target.Uri.Scheme != Uri.UriSchemeHttps)))
            return "Web applications require an http or https address.";
        if (target.LaunchType is LaunchType.Uri && (target.Uri is null || !target.Uri.IsAbsoluteUri))
            return "The launch URI is invalid.";
        if (target.LaunchType is LaunchType.SystemCommand && string.IsNullOrWhiteSpace(target.ExecutablePath))
            return "The system command is missing.";
        if (target.LaunchType is LaunchType.Emulator && string.IsNullOrWhiteSpace(target.EmulatedGameSourceId))
            return "This emulator launch target is incomplete.";
        if (target.LaunchType is LaunchType.PackagedApplication && string.IsNullOrWhiteSpace(target.AppUserModelId))
            return "This packaged application does not have a valid application identity.";
        if (target.LaunchType is LaunchType.ShellItem && string.IsNullOrWhiteSpace(target.ShellParsingPath))
            return "This Windows shell application target is incomplete.";
        if (target.LaunchType is LaunchType.BrowserApplication && string.IsNullOrWhiteSpace(target.ShortcutPath) && string.IsNullOrWhiteSpace(target.AppUserModelId))
            return "This installed web application no longer has a safe Windows launch identity.";
        return null;
    }
}
