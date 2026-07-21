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
