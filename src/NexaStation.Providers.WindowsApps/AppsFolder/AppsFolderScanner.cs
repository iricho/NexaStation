using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.AppsFolder;

public sealed class AppsFolderScanner(IAppsFolderApplicationReader reader) : IAppsFolderScanner
{
    public async Task<WindowsApplicationScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow; List<string> warnings = []; IReadOnlyList<AppsFolderApplication> entries;
        try { entries = await reader.ReadAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new("windows-apps-folder", started, DateTime.UtcNow, [], [], [], false, true); }
        catch (Exception exception) { return new("windows-apps-folder", started, DateTime.UtcNow, [], [], [exception.Message], false); }
        List<WindowsApplicationCandidate> candidates = [];
        foreach (AppsFolderApplication entry in entries)
        {
            string? id = !string.IsNullOrWhiteSpace(entry.AppUserModelId) ? entry.AppUserModelId : !string.IsNullOrWhiteSpace(entry.ShellParsingPath) ? entry.ShellParsingPath : entry.PackageFamilyName is not null && entry.ApplicationId is not null ? entry.PackageFamilyName + "!" + entry.ApplicationId : null;
            if (id is null) { warnings.Add($"Skipped non-launchable AppsFolder entry '{entry.DisplayName}'."); continue; }
            candidates.Add(new() { ProviderId="windows-apps-folder",ProviderItemId=id,DisplayName=entry.DisplayName,SourceType=WindowsApplicationSourceType.PackagedApplication,LaunchMethod=entry.AppUserModelId is null?WindowsLaunchMethod.ShellItem:WindowsLaunchMethod.AppUserModelId,AppUserModelId=entry.AppUserModelId,PackageFamilyName=entry.PackageFamilyName,PackageFullName=entry.PackageFullName,ApplicationId=entry.ApplicationId,IconLocation=entry.IconPath,Publisher=entry.Publisher,ShellParsingPath=entry.ShellParsingPath,Installed=entry.Installed,DiscoveryConfidence=entry.AppUserModelId is null?ApplicationDiscoveryConfidence.High:ApplicationDiscoveryConfidence.Verified });
        }
        return new("windows-apps-folder", started, DateTime.UtcNow, candidates, warnings, []);
    }
}
