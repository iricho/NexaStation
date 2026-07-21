using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation;

public sealed class EmulationLibraryProvider(IEmulationPersistence persistence, IEmulationLibraryService service) : ILibraryProvider
{
    public string Id => "emulation";
    public string DisplayName => "Emulation";

    public async Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EmulatorInstallation> installations = await persistence.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
        int available = installations.Count(value => value.Enabled && File.Exists(value.ExecutablePath));
        return new(Id, available > 0, null, available > 0 ? $"{available} emulator installation(s) available." : "No emulator installations are configured.", available == 0, []);
    }

    public async Task<ProviderScanResult> ScanAsync(ProviderScanRequest request, CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow; List<ProviderLibrarySource> sources=[];List<DiscoveredLibraryItem> items=[];List<string>warnings=[];List<string>errors=[];bool cancelled=false;
        foreach(GameSource source in await persistence.GetGameSourcesAsync(cancellationToken).ConfigureAwait(false))
        {
            if(!source.Enabled)continue;GameSourceScanResult scan=await service.ScanAsync(source,null,cancellationToken).ConfigureAwait(false);cancelled|=scan.WasCancelled;warnings.AddRange(scan.Warnings);errors.AddRange(scan.Errors);sources.Add(new(source.Id,Id,source.FolderPath,Path.GetFullPath(source.FolderPath).ToUpperInvariant(),source.Enabled,scan.CompletedSuccessfully));
            items.AddRange(scan.Games.Select(game=>new DiscoveredLibraryItem(Id,game.PlatformId+"|"+game.NormalizedGamePath,game.SuggestedTitle,game.SuggestedTitle,ContentType.Game,game.GamePath,null,null,true,game.FileModifiedAtUtc,new Dictionary<string,string>{{"platformId",game.PlatformId},{"extension",game.FileExtension}},game.ArtworkCandidates??[],source.Id)));
        }
        return new(Id,started,DateTime.UtcNow,null,sources,items,warnings,errors,cancelled);
    }

    public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default)=>Task.FromResult(new ProviderValidationResult(source.Installed&&File.Exists(source.InstallationPath),source.Installed?"The emulated game source is available.":"The emulated game source is missing.",[]));
    public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Emulated games are resolved through IEmulatorLaunchResolver using their persisted emulator references.");
}
