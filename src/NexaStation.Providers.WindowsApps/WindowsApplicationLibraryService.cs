using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps;

public sealed class WindowsApplicationLibraryService(IStartMenuApplicationScanner startMenu,IAppsFolderScanner appsFolder,IPwaDetector pwas,IWindowsApplicationClassifier classifier,IWindowsApplicationPersistence persistence,IApplicationIconExtractor icons) : IWindowsApplicationLibraryService
{
    public async Task<WindowsApplicationScanResult> ScanAllAsync(StartMenuScanRequest request,bool includeAppsFolder,bool detectPwas,CancellationToken cancellationToken=default)
    {
        DateTime started=DateTime.UtcNow;WindowsApplicationScanResult start=await startMenu.ScanAsync(request,cancellationToken).ConfigureAwait(false);List<WindowsApplicationCandidate> candidates=[..start.Candidates];List<string>warnings=[..start.Warnings];List<string>errors=[..start.Errors];bool complete=start.CompletedSuccessfully;
        if(includeAppsFolder){WindowsApplicationScanResult apps=await appsFolder.ScanAsync(cancellationToken).ConfigureAwait(false);candidates.AddRange(apps.Candidates);warnings.AddRange(apps.Warnings);errors.AddRange(apps.Errors);complete&=apps.CompletedSuccessfully;}
        if(detectPwas)
        {
            foreach(WindowsApplicationCandidate candidate in candidates.ToArray()){PwaCandidate? pwa=pwas.Detect(candidate);if(pwa is null||pwa.DetectionConfidence<ApplicationDiscoveryConfidence.Medium)continue;candidates.Add(candidate with{ProviderId="windows-pwa",ProviderItemId=pwa.AppUserModelId??pwa.BrowserApplicationId??pwa.OriginalShortcutPath??candidate.ProviderItemId,SourceType=WindowsApplicationSourceType.ProgressiveWebApplication,LaunchMethod=pwa.OriginalShortcutPath is not null?WindowsLaunchMethod.ShellLink:pwa.AppUserModelId is not null?WindowsLaunchMethod.AppUserModelId:WindowsLaunchMethod.BrowserApplication,BrowserFamily=pwa.BrowserFamily,BrowserAppId=pwa.BrowserApplicationId,WebAppUrl=pwa.ApplicationUrl,DiscoveryConfidence=pwa.DetectionConfidence});}
        }
        WindowsApplicationCandidate[] distinct=candidates.DistinctBy(c=>c.ProviderId+"|"+c.ProviderItemId,StringComparer.OrdinalIgnoreCase).ToArray();
        return new("windows-applications",started,DateTime.UtcNow,distinct,warnings,errors,complete,start.WasCancelled);
    }
    public Task<WindowsApplicationImportPreview>CreatePreviewAsync(WindowsApplicationScanResult scan,CancellationToken cancellationToken=default)=>persistence.CreatePreviewAsync(scan.Candidates,classifier,cancellationToken);
    public async Task<WindowsApplicationImportResult>ImportAsync(IReadOnlyCollection<WindowsApplicationImportSelection> selections,CancellationToken cancellationToken=default)
    {
        List<WindowsApplicationImportSelection> enriched=[];
        foreach(WindowsApplicationImportSelection selection in selections)
        {
            if(selection.Action==WindowsImportAction.Skip){enriched.Add(selection);continue;}WindowsApplicationCandidate candidate=selection.Candidate.Candidate;ExtractedApplicationIcon? icon=null;try{icon=await icons.ExtractAsync(new(candidate.ProviderId+"|"+candidate.ProviderItemId,candidate.ShortcutPath,candidate.TargetPath,candidate.IconLocation,candidate.AppUserModelId,256),cancellationToken).ConfigureAwait(false);}catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException){ }
            enriched.Add(selection with{Candidate=selection.Candidate with{Candidate=icon is null?candidate:candidate with{IconCandidate=new(icon.CachePath,AssetType.Icon,100,icon.Width,icon.Height)}}});
        }
        return await persistence.ImportAsync(enriched,cancellationToken).ConfigureAwait(false);
    }
}
