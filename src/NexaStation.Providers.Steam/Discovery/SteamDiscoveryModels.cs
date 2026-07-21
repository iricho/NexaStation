namespace NexaStation.Providers.Steam.Discovery;

public sealed record SteamInstallationDetectionResult(
    bool Detected,
    string? InstallationPath,
    string Message,
    IReadOnlyList<string> Diagnostics);

public enum SteamLibrarySource { PrimaryInstallation, LibraryConfiguration, UserConfigured }

public sealed record SteamLibraryLocation(
    string Id,
    string Path,
    string SteamAppsPath,
    string CommonPath,
    bool Available,
    SteamLibrarySource Source,
    string? DiagnosticMessage = null);

public interface ISteamInstallationLocator
{
    Task<SteamInstallationDetectionResult> DetectAsync(CancellationToken cancellationToken = default);
}

public interface ISteamLibraryLocator
{
    Task<IReadOnlyList<SteamLibraryLocation>> FindLibrariesAsync(string steamRoot, CancellationToken cancellationToken = default);
}
