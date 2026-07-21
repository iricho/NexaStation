using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Steam.Artwork;
using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.Launching;
using NexaStation.Providers.Steam.Validation;

namespace NexaStation.Providers.Steam;

public sealed class SteamLibraryProvider(
    ISteamInstallationLocator installationLocator,
    ISteamLibraryLocator libraryLocator,
    ISteamApplicationScanner scanner,
    ISteamArtworkLocator artworkLocator,
    IProviderArtworkCache artworkCache,
    ISteamLaunchTargetFactory launchTargets,
    ISettingsService settings) : ILibraryProvider, IDisposable
{
    private readonly SemaphoreSlim _scanGate=new(1,1);
    public string Id=>"steam";
    public string DisplayName=>"Steam";

    public async Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken=default)
    {
        SteamInstallationDetectionResult result=await installationLocator.DetectAsync(cancellationToken).ConfigureAwait(false);
        return new(Id,result.Detected,result.InstallationPath,result.Message,!result.Detected,result.Diagnostics);
    }

    public async Task<ProviderScanResult> ScanAsync(ProviderScanRequest request,CancellationToken cancellationToken=default)
    {
        DateTime started=DateTime.UtcNow;
        if(!await _scanGate.WaitAsync(0,cancellationToken).ConfigureAwait(false))return new(Id,started,DateTime.UtcNow,request.ConfiguredInstallationPath,[],[],[],["A Steam scan is already running."],false);
        try
        {
            string? root=request.ConfiguredInstallationPath??await settings.GetAsync<string?>(SteamInstallationLocator.ConfirmedPathSetting,null,cancellationToken).ConfigureAwait(false);
            if(string.IsNullOrWhiteSpace(root)||!SteamInstallationLocator.IsValid(SteamInstallationLocator.Normalize(root)))root=(await installationLocator.DetectAsync(cancellationToken).ConfigureAwait(false)).InstallationPath;
            if(string.IsNullOrWhiteSpace(root))return new(Id,started,DateTime.UtcNow,null,[],[],[],["Steam was not detected automatically."],false);
            root=SteamInstallationLocator.Normalize(root);
            IReadOnlyList<SteamLibraryLocation> allLibraries=await libraryLocator.FindLibrariesAsync(root,cancellationToken).ConfigureAwait(false);
            IReadOnlyCollection<string>? configuredEnabled=request.EnabledLibrarySourceIds;
            if(configuredEnabled is null){string[] saved=await settings.GetAsync("Providers.Steam.EnabledLibrarySourceIds",Array.Empty<string>(),cancellationToken).ConfigureAwait(false);configuredEnabled=saved.Length==0?null:saved;}
            HashSet<string>? enabled=configuredEnabled?.ToHashSet(StringComparer.OrdinalIgnoreCase);
            SteamLibraryLocation[] libraries=allLibraries.Where(library=>enabled is null||enabled.Contains(library.Id)).ToArray();
            SteamScanResult scan=await scanner.ScanAsync(libraries,cancellationToken).ConfigureAwait(false);
            List<DiscoveredLibraryItem> items=[];
            foreach(SteamDiscoveredApplication application in scan.Applications)
            {
                cancellationToken.ThrowIfCancellationRequested();string appId=application.Manifest.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                IReadOnlyList<SteamArtworkCandidate> located=await artworkLocator.FindAsync(root,appId,cancellationToken).ConfigureAwait(false);
                IReadOnlyList<ArtworkCandidate> artwork=await artworkCache.CacheAsync(Id,appId,located,cancellationToken).ConfigureAwait(false);
                Dictionary<string,string> data=new(application.Manifest.ProviderData,StringComparer.OrdinalIgnoreCase){["InstallDirectoryName"]=application.Manifest.InstallDirectoryName,["LibraryPath"]=application.Library.Path};
                items.Add(new(Id,appId,application.Manifest.Name,application.Manifest.Name,ContentType.Game,application.Manifest.InstallationPath,application.Manifest.ManifestPath,launchTargets.CreateRunUri(appId),application.Manifest.InstallationExists,application.Manifest.LastUpdatedUtc,data,artwork,application.Library.Id));
            }
            List<string> warnings=[..scan.Warnings,..allLibraries.Where(value=>value.DiagnosticMessage is not null).Select(value=>value.DiagnosticMessage!)];
            return new(Id,started,DateTime.UtcNow,root,allLibraries.Select(ToSource).ToArray(),items,warnings,scan.Errors,false);
        }
        catch(OperationCanceledException){return new(Id,started,DateTime.UtcNow,request.ConfiguredInstallationPath,[],[],[],[],true);}
        finally{_scanGate.Release();}
    }

    public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(SteamProviderValidator.Validate(source));}
    public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();ProviderValidationResult validation=SteamProviderValidator.Validate(source);if(!validation.IsValid)throw new InvalidOperationException(validation.Message);return Task.FromResult(new ResolvedLaunchTarget(launchTargets.Create(source.ProviderItemId,source.InstallationPath),source));}
    private static ProviderLibrarySource ToSource(SteamLibraryLocation location)=>new(location.Id,"steam",location.Path,Normalize(location.Path),true,location.Available,null,location.DiagnosticMessage);
    private static string Normalize(string path)=>Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).ToUpperInvariant();
    public void Dispose()=>_scanGate.Dispose();
}
