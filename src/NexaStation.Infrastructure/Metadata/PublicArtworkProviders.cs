using System.Net;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

/// <summary>Uses Valve's public Steam library artwork CDN with an exact local Steam App ID.</summary>
public sealed class SteamPublicArtworkProvider(IRemoteArtworkDownloader downloader) : IArtworkProvider
{
    private static readonly Uri BaseUri = new("https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/");
    public string Id => "steam-public";
    public string DisplayName => "Steam Public Artwork";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.ExternalIdSearch | ArtworkProviderCapabilities.CoverArtwork | ArtworkProviderCapabilities.WideArtwork | ArtworkProviderCapabilities.HeroArtwork;
    public Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(new ArtworkProviderStatus(true, true, Message: "No API key required. Uses exact Steam App IDs.")); }
    public Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? appId = request.Identity.SteamAppId;
        if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsAsciiDigit)) return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([]);
        ArtworkMatchCandidate candidate = new(Id, appId, request.Identity.OriginalTitle, request.Identity.PlatformId ?? "pc", request.Identity.Region, null, ExternalIds: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["steam"] = appId });
        return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([candidate]);
    }
    public Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<RemoteArtworkAsset> assets = [];
        foreach (ArtworkKind type in request.AssetTypes.Distinct())
        {
            (string File, int Width, int Height)? definition = type switch
            {
                ArtworkKind.CoverPortrait => ("library_600x900_2x.jpg", 600, 900),
                ArtworkKind.GridWide => ("library_header.jpg", 920, 430),
                ArtworkKind.HeroBackground => ("library_hero.jpg", 1920, 620),
                _ => null,
            };
            if (definition is not { } value) continue;
            Uri uri = new(BaseUri, $"{Uri.EscapeDataString(request.ProviderGameId)}/{value.File}");
            assets.Add(new(Id, request.ProviderGameId, value.File, type, uri, null, value.Width, value.Height, null, "image/jpeg", Attribution: new Dictionary<string, string> { ["source"] = "Steam" }));
        }
        return Task.FromResult<IReadOnlyList<RemoteArtworkAsset>>(assets);
    }
    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) => downloader.DownloadAsync(asset, request, cancellationToken);
}

/// <summary>Uses GOG's public catalogue with the exact product ID stored by the local GOG installation.</summary>
public sealed class GogPublicArtworkProvider(HttpClient http, IRemoteArtworkDownloader downloader) : IArtworkProvider
{
    private static readonly Uri ProductApi = new("https://api.gog.com/products/");
    private static readonly Uri CatalogueApi = new("https://catalog.gog.com/v1/catalog");
    public string Id => "gog-public";
    public string DisplayName => "GOG Public Artwork";
    public ArtworkProviderCapabilities Capabilities =>
        ArtworkProviderCapabilities.ExternalIdSearch
        | ArtworkProviderCapabilities.CoverArtwork
        | ArtworkProviderCapabilities.WideArtwork
        | ArtworkProviderCapabilities.HeroArtwork
        | ArtworkProviderCapabilities.LogoArtwork
        | ArtworkProviderCapabilities.IconArtwork;

    public Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ArtworkProviderStatus(true, true, Message: "No API key required. Uses exact GOG product IDs."));
    }

    public Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? productId = request.Identity.ProviderItemId;
        if (!string.Equals(request.Identity.ProviderId, "gog", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(productId)
            || !productId.All(char.IsAsciiDigit))
            return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([]);
        return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>(
            [new(Id, productId, request.Identity.OriginalTitle, "pc", request.Identity.Region, null,
                ExternalIds: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gog"] = productId })]);
    }

    public async Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderGameId) || !request.ProviderGameId.All(char.IsAsciiDigit)) return [];
        using HttpResponseMessage productResponse = await http.GetAsync(
            new Uri(ProductApi, $"{Uri.EscapeDataString(request.ProviderGameId)}?expand=images"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        if (productResponse.StatusCode == HttpStatusCode.NotFound) return [];
        productResponse.EnsureSuccessStatusCode();
        await using Stream productStream = await productResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument productDocument = await JsonDocument.ParseAsync(productStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement product = productDocument.RootElement;
        string title = Text(product, "title") ?? request.ProviderGameId;

        JsonElement? catalogueProduct = null;
        Uri catalogueUri = new($"{CatalogueApi}?limit=48&productType=in%3Agame&query={Uri.EscapeDataString(title)}");
        using HttpResponseMessage catalogueResponse = await http.GetAsync(catalogueUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (catalogueResponse.IsSuccessStatusCode)
        {
            await using Stream catalogueStream = await catalogueResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument catalogueDocument = await JsonDocument.ParseAsync(catalogueStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (catalogueDocument.RootElement.TryGetProperty("products", out JsonElement products) && products.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement value in products.EnumerateArray())
                {
                    if (string.Equals(Text(value, "id"), request.ProviderGameId, StringComparison.Ordinal))
                    {
                        catalogueProduct = value.Clone();
                        break;
                    }
                }
            }
        }

        List<RemoteArtworkAsset> assets = [];
        foreach (ArtworkKind type in request.AssetTypes.Distinct())
        {
            string? value = type switch
            {
                ArtworkKind.CoverPortrait => catalogueProduct is { } catalogue ? Text(catalogue, "coverVertical") : null,
                ArtworkKind.GridWide => catalogueProduct is { } catalogue ? Text(catalogue, "coverHorizontal") : null,
                ArtworkKind.HeroBackground => catalogueProduct is { } catalogue
                    ? Text(catalogue, "galaxyBackgroundImage") ?? Image(product, "background")
                    : Image(product, "background"),
                ArtworkKind.Logo => catalogueProduct is { } catalogue
                    ? Text(catalogue, "logo") ?? Image(product, "logo2x") ?? Image(product, "logo")
                    : Image(product, "logo2x") ?? Image(product, "logo"),
                ArtworkKind.Icon => Image(product, "icon") ?? Image(product, "sidebarIcon2x") ?? Image(product, "sidebarIcon"),
                _ => null,
            };
            if (!TryHttps(value, out Uri? uri)) continue;
            assets.Add(new(Id, request.ProviderGameId, type.ToString(), type, uri!, null, null, null, null,
                Mime(uri!), Attribution: new Dictionary<string, string> { ["source"] = "GOG" }));
        }
        return assets;
    }

    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) =>
        downloader.DownloadAsync(asset, request, cancellationToken);

    private static string? Image(JsonElement product, string name) =>
        product.TryGetProperty("images", out JsonElement images) ? Text(images, name) : null;
    private static string? Text(JsonElement value, string name) =>
        value.TryGetProperty(name, out JsonElement element) && element.ValueKind is JsonValueKind.String or JsonValueKind.Number ? element.ToString() : null;
    private static bool TryHttps(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string absolute = value.StartsWith("//", StringComparison.Ordinal) ? $"https:{value}" : value;
        return Uri.TryCreate(absolute, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
    private static string Mime(Uri uri) => Path.GetExtension(uri.AbsolutePath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
}

/// <summary>Uses the public PCSX2-compatible PS2 cover catalogue with an exact disc serial.</summary>
public sealed class Ps2CoverArtworkProvider(IRemoteArtworkDownloader downloader) : IArtworkProvider
{
    private static readonly Uri BaseUri = new("https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/");
    public string Id => "ps2-covers";
    public string DisplayName => "PS2 Community Covers";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.GameSerialSearch | ArtworkProviderCapabilities.CoverArtwork;
    public Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(new ArtworkProviderStatus(true, true, Message: "No API key required. Uses exact PS2 disc serials.")); }
    public Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? serial = CanonicalSerial(request.Identity.GameSerial);
        if (!string.Equals(request.Identity.PlatformId, "playstation-2", StringComparison.OrdinalIgnoreCase) || serial is null) return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([]);
        return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([new(Id, serial, request.Identity.OriginalTitle, "playstation-2", request.Identity.Region, null, GameSerial: serial)]);
    }
    public Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.AssetTypes.Contains(ArtworkKind.CoverPortrait) || CanonicalSerial(request.ProviderGameId) is not { } serial) return Task.FromResult<IReadOnlyList<RemoteArtworkAsset>>([]);
        Uri uri = new(BaseUri, $"{serial}.jpg");
        RemoteArtworkAsset asset = new(Id, serial, $"{serial}.jpg", ArtworkKind.CoverPortrait, uri, null, null, null, null, "image/jpeg", Attribution: new Dictionary<string, string> { ["source"] = "xlenore/ps2-covers" });
        return Task.FromResult<IReadOnlyList<RemoteArtworkAsset>>([asset]);
    }
    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) => downloader.DownloadAsync(asset, request, cancellationToken);
    private static string? CanonicalSerial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string compact = new(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return compact.Length == 9 && compact[..4].All(char.IsAsciiLetter) && compact[4..].All(char.IsAsciiDigit) ? $"{compact[..4]}-{compact[4..]}" : null;
    }
}
