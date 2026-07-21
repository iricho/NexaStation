using System.Text.RegularExpressions;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.Validation;

public static partial class WindowsApplicationValidator
{
    public static string? Validate(WindowsApplicationCandidate candidate)
    {
        if (candidate.LaunchMethod == WindowsLaunchMethod.AppUserModelId && !ValidAumid().IsMatch(candidate.AppUserModelId ?? string.Empty)) return "The AppUserModelId is invalid.";
        if (candidate.LaunchMethod == WindowsLaunchMethod.Executable && (candidate.TargetPath is null || !Path.IsPathFullyQualified(candidate.TargetPath) || !File.Exists(candidate.TargetPath))) return "The executable target is missing.";
        if (candidate.LaunchMethod == WindowsLaunchMethod.ShellLink && (candidate.ShortcutPath is null || !File.Exists(candidate.ShortcutPath))) return "The original shortcut is missing.";
        if (candidate.LaunchMethod == WindowsLaunchMethod.Uri && candidate.ProtocolUri is not { IsAbsoluteUri: true }) return "The URI target is invalid.";
        if (candidate.ProtocolUri is { Scheme: var scheme } && scheme is not "http" and not "https" and not "mailto" && candidate.DiscoveryConfidence < ApplicationDiscoveryConfidence.Verified) return "The unusual URI scheme requires confirmation.";
        return null;
    }

    [GeneratedRegex("^[^\\s!]{1,200}![^\\s!]{1,200}$", RegexOptions.CultureInvariant)] private static partial Regex ValidAumid();
}
