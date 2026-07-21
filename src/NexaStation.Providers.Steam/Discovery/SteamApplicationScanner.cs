using System.Collections.Concurrent;
using NexaStation.Providers.Steam.Manifests;

namespace NexaStation.Providers.Steam.Discovery;

public sealed record SteamDiscoveredApplication(SteamAppManifest Manifest, SteamLibraryLocation Library);
public sealed record SteamScanResult(IReadOnlyList<SteamDiscoveredApplication> Applications,IReadOnlyList<string> Warnings,IReadOnlyList<string> Errors,TimeSpan Duration);

public interface ISteamApplicationScanner
{
    Task<SteamScanResult> ScanAsync(IReadOnlyCollection<SteamLibraryLocation> libraries,CancellationToken cancellationToken=default);
}

public sealed class SteamApplicationScanner(ISteamAppManifestParser parser,int maximumConcurrency=8) : ISteamApplicationScanner
{
    private readonly int _maximumConcurrency=Math.Clamp(maximumConcurrency,1,32);

    public async Task<SteamScanResult> ScanAsync(IReadOnlyCollection<SteamLibraryLocation> libraries,CancellationToken cancellationToken=default)
    {
        long started=Environment.TickCount64;ConcurrentBag<SteamDiscoveredApplication> discovered=[];ConcurrentBag<string> warnings=[];ConcurrentBag<string> errors=[];List<(SteamLibraryLocation Library,string Manifest)> files=[];
        foreach(SteamLibraryLocation library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(!library.Available){warnings.Add($"Library '{library.Path}' is unavailable and was skipped.");continue;}
            try{files.AddRange(Directory.EnumerateFiles(library.SteamAppsPath,"appmanifest_*.acf",SearchOption.TopDirectoryOnly).Select(path=>(library,path)));}
            catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){errors.Add($"Could not enumerate manifests in '{library.Path}': {exception.Message}");}
        }
        await Parallel.ForEachAsync(files,new ParallelOptions{CancellationToken=cancellationToken,MaxDegreeOfParallelism=_maximumConcurrency},async (entry,token)=>
        {
            try
            {
                string content=await File.ReadAllTextAsync(entry.Manifest,token).ConfigureAwait(false);
                SteamAppManifestParseResult result=parser.Parse(entry.Manifest,content);
                if(result.Manifest is not null)discovered.Add(new(result.Manifest,entry.Library));
                else errors.Add($"{Path.GetFileName(entry.Manifest)}: {string.Join("; ",result.Diagnostics.Select(value=>$"line {value.Line}, column {value.Column}: {value.Reason}"))}");
            }
            catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){errors.Add($"{Path.GetFileName(entry.Manifest)} could not be read: {exception.Message}");}
        }).ConfigureAwait(false);

        List<SteamDiscoveredApplication> unique=[];
        foreach(IGrouping<long,SteamDiscoveredApplication> group in discovered.GroupBy(value=>value.Manifest.AppId))
        {
            SteamDiscoveredApplication[] options=group.ToArray();
            SteamDiscoveredApplication selected=options.OrderByDescending(value=>value.Manifest.InstallationExists).ThenBy(value=>value.Manifest.ManifestPath,StringComparer.OrdinalIgnoreCase).First();
            unique.Add(selected);
            if(options.Length>1)warnings.Add(options.Count(value=>value.Manifest.InstallationExists)>1?$"App {group.Key} has multiple installed manifests; '{selected.Manifest.ManifestPath}' was selected deterministically.":$"App {group.Key} appeared in multiple libraries; the available installation was preferred.");
        }
        return new(unique.OrderBy(value=>value.Manifest.Name,StringComparer.OrdinalIgnoreCase).ToArray(),warnings.Order().ToArray(),errors.Order().ToArray(),TimeSpan.FromMilliseconds(Environment.TickCount64-started));
    }
}
