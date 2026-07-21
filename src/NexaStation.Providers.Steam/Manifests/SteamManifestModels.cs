using NexaStation.Providers.Steam.KeyValues;

namespace NexaStation.Providers.Steam.Manifests;

public sealed record SteamAppManifest(
    long AppId,
    string Name,
    string InstallDirectoryName,
    string? BuildId,
    DateTime? LastUpdatedUtc,
    long? SizeOnDisk,
    long? StateFlags,
    string ManifestPath,
    string InstallationPath,
    bool InstallationExists,
    IReadOnlyDictionary<string, string> ProviderData);

public sealed record SteamAppManifestParseResult(
    SteamAppManifest? Manifest,
    IReadOnlyList<KeyValuesDiagnostic> Diagnostics)
{
    public bool IsValid => Manifest is not null && Diagnostics.Count == 0;
}

public interface ISteamAppManifestParser
{
    SteamAppManifestParseResult Parse(string manifestPath, string content);
}
