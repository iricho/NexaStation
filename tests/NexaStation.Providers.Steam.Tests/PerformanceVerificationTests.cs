using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.Artwork;
using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Providers.Steam.Manifests;
using Xunit;
using Xunit.Abstractions;

namespace NexaStation.Providers.Steam.Tests;

public sealed class PerformanceVerificationTests(ITestOutputHelper output)
{
    [Theory] [InlineData(100)] [InlineData(500)] [InlineData(1000)] [InlineData(5000)]
    public async Task MeasuresSyntheticLibrary(int count)
    {
        using TempSteam steam=new();
        for(int i=1;i<=count;i++)File.WriteAllText(Path.Combine(steam.Root,"steamapps",$"appmanifest_{i}.acf"),$"\"AppState\"{{\"appid\"\"{i}\"\"name\"\"Fictional Game {i}\"\"installdir\"\"Game{i}\"\"buildid\"\"1\"}}");
        Stopwatch timer=Stopwatch.StartNew();IReadOnlyList<SteamLibraryLocation> libraries=await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(steam.Root);timer.Stop();double configurationMs=timer.Elapsed.TotalMilliseconds;
        timer.Restart();SteamScanResult scan=await new SteamApplicationScanner(new SteamAppManifestParser(new KeyValuesParser()),8).ScanAsync(libraries);timer.Stop();double enumerationAndParsingMs=timer.Elapsed.TotalMilliseconds;
        Dictionary<long,SteamDiscoveredApplication> existing=scan.Applications.ToDictionary(value=>value.Manifest.AppId);timer.Restart();int matches=scan.Applications.Count(value=>existing.ContainsKey(value.Manifest.AppId));timer.Stop();double matchingMs=timer.Elapsed.TotalMilliseconds;
        timer.Restart();int unchanged=scan.Applications.Count(value=>existing[value.Manifest.AppId].Manifest.BuildId==value.Manifest.BuildId);timer.Stop();double incrementalMs=timer.Elapsed.TotalMilliseconds;
        ProviderScanResult providerScan=new("steam",DateTime.UtcNow,DateTime.UtcNow,steam.Root,[],scan.Applications.Select(value=>new DiscoveredLibraryItem("steam",value.Manifest.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture),value.Manifest.Name,value.Manifest.Name,ContentType.Game,value.Manifest.InstallationPath,value.Manifest.ManifestPath,new Uri($"steam://run/{value.Manifest.AppId}"),value.Manifest.InstallationExists,value.Manifest.LastUpdatedUtc,value.Manifest.ProviderData,[])).ToArray(),[],[]);
        ProviderImportService imports=new(new ProviderRegistry([new FakeProvider(providerScan)]),new EmptyPersistence());timer.Restart();ImportPreview preview=await imports.CreatePreviewAsync("steam",providerScan);timer.Stop();double previewMs=timer.Elapsed.TotalMilliseconds;
        string artworkRoot=Path.Combine(steam.Root,"appcache");Directory.CreateDirectory(artworkRoot);timer.Restart();_ = await new SteamArtworkLocator().FindAsync(steam.Root,"1");timer.Stop();double artworkLookupMs=timer.Elapsed.TotalMilliseconds;
        output.WriteLine($"PERF manifests={count} config_ms={configurationMs:F3} enumerate_parse_ms={enumerationAndParsingMs:F3} match_ms={matchingMs:F3} incremental_ms={incrementalMs:F3} preview_ms={previewMs:F3} artwork_lookup_ms={artworkLookupMs:F3} matches={matches} unchanged={unchanged} preview={preview.Candidates.Count}");
        Assert.Equal(count,scan.Applications.Count);Assert.Equal(count,matches);Assert.Equal(count,unchanged);Assert.Equal(count,preview.Candidates.Count);
    }

    private sealed class FakeProvider(ProviderScanResult result):ILibraryProvider{public string Id=>"steam";public string DisplayName=>"Steam";public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken=default)=>throw new NotSupportedException();public Task<ProviderScanResult> ScanAsync(ProviderScanRequest request,CancellationToken cancellationToken=default)=>Task.FromResult(result);public Task<ProviderValidationResult> ValidateAsync(ProviderSource source,CancellationToken cancellationToken=default)=>throw new NotSupportedException();public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source,CancellationToken cancellationToken=default)=>throw new NotSupportedException();}
    private sealed class EmptyPersistence:IProviderPersistence
    {
        public Task<ProviderSource?> FindSourceAsync(string providerId,string providerItemId,CancellationToken cancellationToken=default)=>Task.FromResult<ProviderSource?>(null);
        public Task<LibraryItem?> FindPossibleDuplicateAsync(DiscoveredLibraryItem item,CancellationToken cancellationToken=default)=>Task.FromResult<LibraryItem?>(null);
        public Task<ProviderImportResult> ImportAsync(string providerId,IReadOnlyCollection<ImportSelection> selections,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<ProviderRefreshResult> ApplyRefreshAsync(ProviderScanResult scanResult,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<IReadOnlyList<ProviderSource>> GetSourcesAsync(string providerId,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<ProviderSource>>([]);
        public Task AddMetadataOverrideAsync(string libraryItemId,string fieldName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task RemoveMetadataOverrideAsync(string libraryItemId,string fieldName,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    }
}
