using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.ProgressiveWebApps;

public sealed class PwaDetector : IPwaDetector
{
    public PwaCandidate? Detect(WindowsApplicationCandidate candidate)
    {
        string? executable = candidate.TargetPath; string family = BrowserFamily(executable, candidate.AppUserModelId);
        string? appId = FindValue(candidate.Arguments, "--app-id=");
        string? rawUrl = FindValue(candidate.Arguments, "--app=") ?? FindValue(candidate.Arguments, "--app-url=");
        Uri? url = rawUrl is not null && Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? parsed) && parsed.Scheme is "http" or "https" ? parsed : null;
        bool packaged = candidate.SourceType == WindowsApplicationSourceType.PackagedApplication && (candidate.AppUserModelId?.Contains("PWA", StringComparison.OrdinalIgnoreCase) == true || candidate.DisplayName.Contains("web", StringComparison.OrdinalIgnoreCase));
        bool browser = family is not "Unknown";
        if (!packaged && !browser && appId is null && url is null) return null;
        ApplicationDiscoveryConfidence confidence = packaged ? ApplicationDiscoveryConfidence.High : browser && appId is not null ? ApplicationDiscoveryConfidence.High : browser && url is not null ? ApplicationDiscoveryConfidence.Medium : ApplicationDiscoveryConfidence.Low;
        return new(packaged ? "Packaged PWA" : family, executable, appId, url, FindValue(candidate.Arguments, "--profile-directory="), candidate.ShortcutPath, candidate.AppUserModelId, confidence);
    }

    private static string? FindValue(IReadOnlyList<string> arguments, string prefix) => arguments.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..].Trim('"');
    private static string BrowserFamily(string? executable, string? aumid)
    {
        string name = Path.GetFileName(executable ?? string.Empty);
        if (name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase) || aumid?.Contains("MSEdge", StringComparison.OrdinalIgnoreCase) == true) return "Microsoft Edge";
        if (name.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";
        if (name.Equals("brave.exe", StringComparison.OrdinalIgnoreCase)) return "Brave";
        if (name.Contains("chromium", StringComparison.OrdinalIgnoreCase)) return "Chromium";
        return "Unknown";
    }
}
