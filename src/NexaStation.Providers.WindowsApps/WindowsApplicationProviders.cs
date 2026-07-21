using System.Globalization;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.WindowsApps.Validation;

namespace NexaStation.Providers.WindowsApps;

public sealed class WindowsStartMenuProvider(IStartMenuApplicationScanner scanner) : WindowsProviderBase
{
    public override string Id => "windows-start-menu"; public override string DisplayName => "Windows Start Menu";
    protected override Task<WindowsApplicationScanResult> ScanWindowsAsync(CancellationToken cancellationToken) => scanner.ScanAsync(new(), cancellationToken);
}
public sealed class WindowsAppsFolderProvider(IAppsFolderScanner scanner) : WindowsProviderBase
{
    public override string Id => "windows-apps-folder"; public override string DisplayName => "Windows Apps";
    protected override Task<WindowsApplicationScanResult> ScanWindowsAsync(CancellationToken cancellationToken) => scanner.ScanAsync(cancellationToken);
}
public sealed class WindowsPwaProvider(IStartMenuApplicationScanner startMenu, IAppsFolderScanner appsFolder, IPwaDetector detector) : WindowsProviderBase
{
    public override string Id => "windows-pwa"; public override string DisplayName => "Installed Web Applications";
    protected override async Task<WindowsApplicationScanResult> ScanWindowsAsync(CancellationToken cancellationToken)
    {
        DateTime started=DateTime.UtcNow;WindowsApplicationScanResult start=await startMenu.ScanAsync(new(),cancellationToken).ConfigureAwait(false);WindowsApplicationScanResult apps=await appsFolder.ScanAsync(cancellationToken).ConfigureAwait(false);List<WindowsApplicationCandidate> result=[];
        foreach(WindowsApplicationCandidate candidate in start.Candidates.Concat(apps.Candidates)){PwaCandidate? pwa=detector.Detect(candidate);if(pwa is null||pwa.DetectionConfidence<ApplicationDiscoveryConfidence.Medium)continue;string identity=pwa.AppUserModelId??pwa.BrowserApplicationId??pwa.OriginalShortcutPath??candidate.ProviderItemId;result.Add(candidate with{ProviderId=Id,ProviderItemId=identity,SourceType=WindowsApplicationSourceType.ProgressiveWebApplication,LaunchMethod=pwa.OriginalShortcutPath is not null?WindowsLaunchMethod.ShellLink:pwa.AppUserModelId is not null?WindowsLaunchMethod.AppUserModelId:WindowsLaunchMethod.BrowserApplication,BrowserFamily=pwa.BrowserFamily,BrowserAppId=pwa.BrowserApplicationId,WebAppUrl=pwa.ApplicationUrl,DiscoveryConfidence=pwa.DetectionConfidence});}
        return new(Id,started,DateTime.UtcNow,result.DistinctBy(c=>c.ProviderItemId,StringComparer.OrdinalIgnoreCase).ToArray(),start.Warnings.Concat(apps.Warnings).ToArray(),start.Errors.Concat(apps.Errors).ToArray(),start.CompletedSuccessfully&&apps.CompletedSuccessfully,start.WasCancelled||apps.WasCancelled);
    }
}

public abstract class WindowsProviderBase : ILibraryProvider
{
    public abstract string Id { get; } public abstract string DisplayName { get; }
    public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderAvailabilityResult(Id,OperatingSystem.IsWindows(),null,OperatingSystem.IsWindows()?"Windows application discovery is available.":"Windows is required.",!OperatingSystem.IsWindows(),[]));
    public async Task<ProviderScanResult> ScanAsync(ProviderScanRequest request,CancellationToken cancellationToken=default){WindowsApplicationScanResult scan=await ScanWindowsAsync(cancellationToken).ConfigureAwait(false);return new(Id,scan.StartedAtUtc,scan.CompletedAtUtc,null,[],scan.Candidates.Select(ToDiscovered).ToArray(),scan.Warnings,scan.Errors,scan.WasCancelled);}
    public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(new ProviderValidationResult(source.Installed,"Windows application source validation is deferred to its structured launch target.",[]));}
    public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(new ResolvedLaunchTarget(ToTarget(source.ProviderData,source.ProviderId,source.LibraryItemId),source));}
    protected abstract Task<WindowsApplicationScanResult> ScanWindowsAsync(CancellationToken cancellationToken);

    internal static DiscoveredLibraryItem ToDiscovered(WindowsApplicationCandidate c){Dictionary<string,string> d=c.ProviderData.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);d["sourceType"]=((int)c.SourceType).ToString(CultureInfo.InvariantCulture);d["launchMethod"]=((int)c.LaunchMethod).ToString(CultureInfo.InvariantCulture);d["shortcutPath"]=c.ShortcutPath??string.Empty;d["targetPath"]=c.TargetPath??string.Empty;d["argumentsJson"]=System.Text.Json.JsonSerializer.Serialize(c.Arguments);d["workingDirectory"]=c.WorkingDirectory??string.Empty;d["aumid"]=c.AppUserModelId??string.Empty;d["shellParsingPath"]=c.ShellParsingPath??string.Empty;d["categoryHint"]=c.CategoryHint??string.Empty;d["packageFamilyName"]=c.PackageFamilyName??string.Empty;d["packageFullName"]=c.PackageFullName??string.Empty;d["applicationId"]=c.ApplicationId??string.Empty;d["confidence"]=((int)c.DiscoveryConfidence).ToString(CultureInfo.InvariantCulture);ContentType contentType=IsConservativeGame(c)?ContentType.Game:ContentType.Application;d["contentClassification"]=contentType.ToString();return new(c.ProviderId,c.ProviderItemId,c.DisplayName,c.DisplayName,contentType,c.TargetPath,c.ShortcutPath,c.ProtocolUri,c.Installed,null,d,c.IconCandidate is null?[]:[c.IconCandidate]);}
    private static bool IsConservativeGame(WindowsApplicationCandidate candidate)
    {
        string title=candidate.DisplayName.Trim();
        if(title.Equals("Xbox",StringComparison.OrdinalIgnoreCase)||title.Contains("Game Bar",StringComparison.OrdinalIgnoreCase)||title.Contains("Gaming Services",StringComparison.OrdinalIgnoreCase))return false;
        if(candidate.ProviderData.TryGetValue("isGame",out string? explicitGame)&&bool.TryParse(explicitGame,out bool parsed))return parsed;
        return candidate.CategoryHint is { } category&&(category.Equals("Game",StringComparison.OrdinalIgnoreCase)||category.Equals("Games",StringComparison.OrdinalIgnoreCase))
            || candidate.StartMenuFolder is { } folder&&(folder.Contains("Xbox Games",StringComparison.OrdinalIgnoreCase)||folder.Equals("Games",StringComparison.OrdinalIgnoreCase));
    }
    private static LaunchTarget ToTarget(IReadOnlyDictionary<string,string>d,string provider,string item){WindowsLaunchMethod method=Enum.TryParse(d.GetValueOrDefault("launchMethod"),out WindowsLaunchMethod parsed)?parsed:WindowsLaunchMethod.Executable;string? shortcut=Empty(d.GetValueOrDefault("shortcutPath"));string? target=Empty(d.GetValueOrDefault("targetPath"));string? aumid=Empty(d.GetValueOrDefault("aumid"));string? shell=Empty(d.GetValueOrDefault("shellParsingPath"));LaunchType type=method switch{WindowsLaunchMethod.AppUserModelId=>LaunchType.PackagedApplication,WindowsLaunchMethod.ShellItem=>LaunchType.ShellItem,WindowsLaunchMethod.BrowserApplication=>LaunchType.BrowserApplication,WindowsLaunchMethod.ShellLink=>LaunchType.WindowsShortcut,WindowsLaunchMethod.Uri=>LaunchType.Uri,_=>LaunchType.Executable};return new(type,target,Empty(d.GetValueOrDefault("workingDirectory"))){LibraryItemId=item,ProviderId=provider,ShortcutPath=shortcut,AppUserModelId=aumid,ShellParsingPath=shell,WindowsLaunchMethod=method,UserManaged=false};}
    private static string? Empty(string? value)=>string.IsNullOrWhiteSpace(value)?null:value;
}
