using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NexaStation.Core;
using NexaStation.Domain;
using Windows.System;

namespace NexaStation.Infrastructure.DiscoverApps;

public sealed partial class EmbeddedDiscoverAppCatalogue(ISettingsService settings) : IDiscoverAppCatalogue
{
    private IReadOnlyList<DiscoverAppDefinition>? _cached;
    public async Task<IReadOnlyList<DiscoverAppDefinition>> GetAsync(CancellationToken cancellationToken=default)
    {
        if(_cached is null)
        {
            await using Stream stream=typeof(EmbeddedDiscoverAppCatalogue).Assembly.GetManifestResourceStream("NexaStation.Infrastructure.DiscoverApps.discover-apps.v1.json")??throw new InvalidOperationException("The Discover Apps catalogue resource is missing.");
            _cached=await DiscoverCatalogueParser.ParseAsync(stream,cancellationToken);
        }
        IReadOnlyList<string> hidden=await settings.GetAsync("DiscoverApps.HiddenIds",Array.Empty<string>(),cancellationToken).ConfigureAwait(false);HashSet<string> hiddenIds=hidden.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _cached.Where(value=>value.Enabled&&!hiddenIds.Contains(value.Id)).ToArray();
    }
}

public static class DiscoverCatalogueParser
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter()}};
    public static async Task<IReadOnlyList<DiscoverAppDefinition>> ParseAsync(Stream stream,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(stream);DiscoverAppDefinition[] definitions=await JsonSerializer.DeserializeAsync<DiscoverAppDefinition[]>(stream,JsonOptions,cancellationToken).ConfigureAwait(false)??[];
        string[] duplicates=definitions.GroupBy(value=>value.Id,StringComparer.OrdinalIgnoreCase).Where(group=>group.Count()>1).Select(group=>group.Key).ToArray();if(duplicates.Length>0)throw new InvalidDataException("Duplicate Discover Apps IDs: "+string.Join(", ",duplicates));
        if(definitions.Any(value=>string.IsNullOrWhiteSpace(value.Id)||string.IsNullOrWhiteSpace(value.DisplayName)||string.IsNullOrWhiteSpace(value.Description)||string.IsNullOrWhiteSpace(value.Category)))throw new InvalidDataException("Every Discover Apps entry requires an ID, display name, description, and category.");
        return definitions.OrderBy(value=>value.SortOrder).ThenBy(value=>value.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}

public sealed class DiscoverAppStateService(IWindowsApplicationPersistence windowsApplications) : IDiscoverAppStateService
{
    public async Task<DiscoverAppState> ResolveAsync(DiscoverAppDefinition definition,CancellationToken cancellationToken=default)
    {
        IReadOnlyList<WindowsApplicationSource> sources=await windowsApplications.GetSourcesAsync(cancellationToken:cancellationToken).ConfigureAwait(false);
        return DiscoverAppStateMatcher.Resolve(definition,sources);
    }
}

public static class DiscoverAppStateMatcher
{
    public static DiscoverAppState Resolve(DiscoverAppDefinition definition,IReadOnlyList<WindowsApplicationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(definition);ArgumentNullException.ThrowIfNull(sources);
        WindowsApplicationSource? match=sources.FirstOrDefault(source=>source.Installed&&Known(definition.KnownAppUserModelIds,source.AppUserModelId));
        match??=sources.FirstOrDefault(source=>source.Installed&&Known(definition.KnownPackageFamilyNames,source.PackageFamilyName));
        match??=sources.FirstOrDefault(source=>source.Installed&&source.ProviderData.TryGetValue("catalogueId",out string? id)&&id.Equals(definition.Id,StringComparison.OrdinalIgnoreCase));
        match??=sources.FirstOrDefault(source=>source.Installed&&source.TargetPath is { } path&&File.Exists(path)&&Known(definition.KnownExecutableNames,Path.GetFileName(path)));
        match??=sources.FirstOrDefault(source=>source.Installed&&source.SourceType==WindowsApplicationSourceType.ProgressiveWebApplication&&(Known(definition.KnownPwaIdentities,source.BrowserAppId)||Known(definition.KnownPwaIdentities,source.WebAppUrl?.AbsoluteUri)));
        if(match is not null)return new(DiscoverAppInstallationState.Installed,match.LibraryItemId,match.AppUserModelId??match.PackageFamilyName??match.TargetPath??match.BrowserAppId,"Matched an exact persisted Windows application identity.");
        if(sources.Any(source=>!source.Installed&&(Known(definition.KnownAppUserModelIds,source.AppUserModelId)||Known(definition.KnownPackageFamilyNames,source.PackageFamilyName))))return new(DiscoverAppInstallationState.BrokenInstallation,Reason:"A previously linked exact identity is no longer installed.");
        return new(DiscoverAppInstallationState.NotInstalled,Reason:"No exact installed identity matched. Display names were not considered.");
    }
    private static bool Known(IReadOnlyList<string> known,string? value)=>!string.IsNullOrWhiteSpace(value)&&known.Contains(value,StringComparer.OrdinalIgnoreCase);
}

public sealed partial class StoreNavigationService : IStoreNavigationService
{
    public async Task<StoreNavigationResult> OpenProductAsync(string productId,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();if(!TryCreateProductUri(productId,out Uri? uri))return new(false,true,null,"The Microsoft Store Product ID is invalid.");
        return await LaunchAsync(uri!,cancellationToken).ConfigureAwait(false);
    }
    public async Task<StoreNavigationResult> SearchAsync(string query,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();if(string.IsNullOrWhiteSpace(query))return new(false,true,null,"A Store search query is required.");return await LaunchAsync(new($"ms-windows-store://search/?query={Uri.EscapeDataString(query.Trim())}"),cancellationToken).ConfigureAwait(false);}
    private static async Task<StoreNavigationResult> LaunchAsync(Uri uri,CancellationToken cancellationToken){try{bool launched=await Launcher.LaunchUriAsync(uri).AsTask(cancellationToken).ConfigureAwait(false);return new(launched,launched,uri,launched?"Microsoft Store opened.":"Microsoft Store is unavailable or disabled.");}catch(Exception exception)when(exception is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException){return new(false,false,uri,"Microsoft Store is unavailable or disabled.");}}
    public static bool TryCreateProductUri(string? productId,out Uri? uri){if(string.IsNullOrWhiteSpace(productId)||!ProductIdPattern().IsMatch(productId)){uri=null;return false;}uri=new($"ms-windows-store://pdp/?ProductId={productId.ToUpperInvariant()}");return true;}
    [GeneratedRegex("^[A-Za-z0-9]{10,20}$",RegexOptions.CultureInvariant)]private static partial Regex ProductIdPattern();
}

public sealed class ExternalUriNavigationService : IExternalUriNavigationService
{
    public async Task<ExternalUriNavigationResult> OpenAsync(Uri uri,CancellationToken cancellationToken=default){ArgumentNullException.ThrowIfNull(uri);if(!uri.IsAbsoluteUri||uri.Scheme is not ("https" or "http"))return new(false,uri,"Only an HTTP or HTTPS web destination can be opened.");try{bool launched=await Launcher.LaunchUriAsync(uri).AsTask(cancellationToken).ConfigureAwait(false);return new(launched,uri,launched?"The web app opened in the default browser.":"Windows could not open the web destination.");}catch(Exception exception)when(exception is UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException){return new(false,uri,"Windows could not open the web destination.");}}
}
