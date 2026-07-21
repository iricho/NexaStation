using System.Globalization;
using NexaStation.Providers.Steam.KeyValues;

namespace NexaStation.Providers.Steam.Manifests;

public sealed class SteamAppManifestParser(IKeyValuesParser parser) : ISteamAppManifestParser
{
    public SteamAppManifestParseResult Parse(string manifestPath, string content)
    {
        string fullManifestPath;
        try { fullManifestPath = Path.GetFullPath(manifestPath); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return Invalid(manifestPath, $"Manifest path is invalid: {exception.Message}"); }

        KeyValuesDocument document = parser.Parse(content);
        List<KeyValuesDiagnostic> diagnostics = document.Diagnostics.Select(value => value with { File = fullManifestPath }).ToList();
        KeyValuesNode? appState = document.Root.Find("AppState") ?? document.Root.Children.FirstOrDefault(node => node.IsObject);
        if (appState is null) { diagnostics.Add(new(fullManifestPath,1,1,"AppState object was not found.")); return new(null,diagnostics); }

        if (!long.TryParse(appState.ValueOf("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out long appId) || appId <= 0)
            diagnostics.Add(new(fullManifestPath,1,1,"App ID must be a positive numeric value."));
        string name = appState.ValueOf("name")?.Trim() ?? string.Empty;
        if (name.Length == 0) diagnostics.Add(new(fullManifestPath,1,1,"Application name is required."));
        string installDirectory = appState.ValueOf("installdir")?.Trim() ?? string.Empty;
        if (!SafeDirectoryName(installDirectory)) diagnostics.Add(new(fullManifestPath,1,1,"Install directory is empty, rooted, or contains path traversal."));

        string? steamAppsPath = Directory.GetParent(fullManifestPath)?.FullName;
        if (steamAppsPath is null || !string.Equals(Path.GetFileName(steamAppsPath), "steamapps", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new(fullManifestPath,1,1,"Manifest must be directly inside a steamapps directory."));
        string installationPath = steamAppsPath is null ? string.Empty : Path.GetFullPath(Path.Combine(steamAppsPath,"common",installDirectory));
        if (steamAppsPath is not null && !IsInside(Path.Combine(steamAppsPath,"common"), installationPath))
            diagnostics.Add(new(fullManifestPath,1,1,"Constructed installation path escapes steamapps/common."));

        if (diagnostics.Count > 0) return new(null, diagnostics);

        Dictionary<string,string> data = new(StringComparer.OrdinalIgnoreCase);
        foreach (string field in new[]{"buildid","LastUpdated","SizeOnDisk","StateFlags","BytesToDownload","BytesDownloaded"})
            if (appState.ValueOf(field) is string value) data[field]=value;
        DateTime? updated = long.TryParse(appState.ValueOf("LastUpdated"),NumberStyles.None,CultureInfo.InvariantCulture,out long unix) && unix >= 0
            ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime : null;
        long? size = long.TryParse(appState.ValueOf("SizeOnDisk"),NumberStyles.None,CultureInfo.InvariantCulture,out long bytes) && bytes >= 0 ? bytes : null;
        long? state = long.TryParse(appState.ValueOf("StateFlags"),NumberStyles.None,CultureInfo.InvariantCulture,out long flags) ? flags : null;
        return new(new(appId,name,installDirectory,appState.ValueOf("buildid"),updated,size,state,fullManifestPath,installationPath,Directory.Exists(installationPath),data),diagnostics);
    }

    private static SteamAppManifestParseResult Invalid(string file,string reason)=>new(null,[new(file,1,1,reason)]);
    private static bool SafeDirectoryName(string value)=>value.Length>0&&!Path.IsPathRooted(value)&&!value.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(segment=>segment is ".." or ".")&&value.IndexOfAny(Path.GetInvalidPathChars())<0;
    private static bool IsInside(string root,string candidate){string normalizedRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;return candidate.StartsWith(normalizedRoot,StringComparison.OrdinalIgnoreCase);}
}
