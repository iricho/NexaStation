using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.Categorization;

public sealed class WindowsApplicationClassifier(IPwaDetector pwas) : IWindowsApplicationClassifier
{
    private static readonly string[] DocumentationTerms = ["documentation", "release notes", "readme", "license", "help"];
    private static readonly string[] RelatedToolTerms = ["safe mode", "crash reporter", "configuration tool", "dedicated server", "benchmark", "repair", " updater", " update"];
    private static readonly (string Category, string[] Terms)[] Categories =
    [
        ("Media", ["multimedia","media","music","audio","video","vlc","spotify"]),
        ("Communication", ["communication","chat","discord","teams","slack","zoom"]),
        ("Browser", ["browser","edge","chrome","firefox","brave"]),
        ("Streaming", ["streaming","netflix","youtube","moonlight"]),
        ("Development", ["development","developer","visual studio","code","sdk","git"]),
        ("Creative", ["creative","design","photo","paint","blender","adobe"]),
        ("Files", ["files","explorer","file manager"]),
        ("Remote Access", ["remote","rdp","moonlight","parsec"]),
        ("Productivity", ["office","productivity","word","excel","notion"]),
        ("Utilities", ["utilities","tools","utility"]),
    ];

    public WindowsApplicationClassification Classify(WindowsApplicationCandidate candidate)
    {
        string evidence = string.Join(' ', new[] { candidate.DisplayName, candidate.Description, candidate.StartMenuFolder, candidate.CategoryHint, candidate.TargetPath, candidate.Publisher }.Where(value => !string.IsNullOrWhiteSpace(value))).ToLowerInvariant();
        string target = Path.GetFileName(candidate.TargetPath ?? string.Empty);
        bool uninstall = evidence.Contains("uninstall", StringComparison.Ordinal) || target.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || target.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase) && candidate.Arguments.Any(value => value.Equals("/x", StringComparison.OrdinalIgnoreCase));
        if (uninstall) return new(WindowsCandidateKind.PossibleUninstaller, "System", ApplicationDiscoveryConfidence.High, false, "Uninstall or package-removal evidence.");
        bool docs = DocumentationTerms.Any(evidence.Contains);
        if (docs) return new(WindowsCandidateKind.Documentation, "Uncategorized", ApplicationDiscoveryConfidence.Medium, false, "Documentation-oriented shortcut.");
        if (candidate.SourceType == WindowsApplicationSourceType.UriShortcut) return new(WindowsCandidateKind.WebShortcut, "Browser", ApplicationDiscoveryConfidence.High, false, "Website shortcut, not an installed PWA identity.");
        bool related = RelatedToolTerms.Any(evidence.Contains);
        if (related) return new(WindowsCandidateKind.RelatedTool, SuggestCategory(evidence), ApplicationDiscoveryConfidence.Medium, false, "Related or maintenance tool evidence.");
        PwaCandidate? pwa = pwas.Detect(candidate);
        string category = pwa is null ? SuggestCategory(evidence) : pwa.ApplicationUrl?.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase) == true ? "Streaming" : "Browser";
        ApplicationDiscoveryConfidence confidence = candidate.DiscoveryConfidence;
        bool valid = candidate.Installed && (candidate.LaunchMethod is WindowsLaunchMethod.AppUserModelId or WindowsLaunchMethod.ShellItem || candidate.ShortcutPath is not null || candidate.TargetPath is not null || candidate.ProtocolUri is not null);
        return new(valid ? WindowsCandidateKind.Application : WindowsCandidateKind.Unknown, category, confidence, valid && confidence >= ApplicationDiscoveryConfidence.Medium, valid ? "Launchable Windows-visible application." : "No safe launch identity was available.");
    }

    private static string SuggestCategory(string evidence)
    {
        foreach ((string category, string[] terms) in Categories) if (terms.Any(evidence.Contains)) return category;
        return "Uncategorized";
    }
}
