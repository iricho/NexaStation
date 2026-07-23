using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

public sealed class ScreenScraperArtworkProvider(IRemoteArtworkDownloader downloader) : IArtworkProvider
{
    public string Id => "screenscraper";
    public string DisplayName => "ScreenScraper";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.TitleSearch | ArtworkProviderCapabilities.PlatformSearch | ArtworkProviderCapabilities.GameSerialSearch | ArtworkProviderCapabilities.HashSearch | ArtworkProviderCapabilities.RegionFiltering | ArtworkProviderCapabilities.CoverArtwork | ArtworkProviderCapabilities.ScreenshotArtwork | ArtworkProviderCapabilities.LogoArtwork | ArtworkProviderCapabilities.Metadata | ArtworkProviderCapabilities.UserAuthentication | ArtworkProviderCapabilities.QuotaReporting;
    public Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(new ArtworkProviderStatus(false, false, "credential_strategy_unavailable", "ScreenScraper requires an approved developer credential strategy and is disabled in public desktop builds.")); }
    public Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([]); }
    public Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<RemoteArtworkAsset>>([]); }
    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) => downloader.DownloadAsync(asset, request, cancellationToken);
}

public sealed class SteamGridDbArtworkProvider(HttpClient http, IProviderCredentialStore credentials, IProviderRateLimitCoordinator limits, IRemoteArtworkDownloader downloader) : IArtworkProvider, IArtworkProviderConnectionProbe
{
    private static readonly Uri BaseUri = new("https://www.steamgriddb.com/api/v2/");
    public string Id => "steamgriddb";
    public string DisplayName => "SteamGridDB";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.TitleSearch | ArtworkProviderCapabilities.PlatformSearch | ArtworkProviderCapabilities.ExternalIdSearch | ArtworkProviderCapabilities.CoverArtwork | ArtworkProviderCapabilities.WideArtwork | ArtworkProviderCapabilities.HeroArtwork | ArtworkProviderCapabilities.LogoArtwork | ArtworkProviderCapabilities.IconArtwork | ArtworkProviderCapabilities.UserAuthentication;

    public async Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ProviderCredential? credential = await credentials.GetAsync(Id, cancellationToken).ConfigureAwait(false);
        return credential is null ? new(false, false, "credential_missing", "Add your free SteamGridDB API key to enable this provider.") : new(true, true);
    }

    public async Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        (string Platform, string Id, string IdentityKey)? external = ExternalIdentity(request.Identity);
        if(external is { } exactIdentity)
        {
            using JsonDocument? exact=await GetAsync($"games/{exactIdentity.Platform}/{Uri.EscapeDataString(exactIdentity.Id)}",cancellationToken).ConfigureAwait(false);
            if(exact is not null&&exact.RootElement.TryGetProperty("data",out JsonElement game)&&game.ValueKind==JsonValueKind.Object&&game.TryGetProperty("id",out JsonElement gameId))
            {
                string name=game.TryGetProperty("name",out JsonElement gameName)?gameName.GetString()??request.Identity.OriginalTitle:request.Identity.OriginalTitle;
                return [new(Id,gameId.ToString(),name,request.Identity.PlatformId??"pc",request.Identity.Region,null,
                    ExternalIds:new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{exactIdentity.IdentityKey,exactIdentity.Id}})];
            }
        }
        string title = request.SearchTitle ?? request.Identity.OriginalTitle;
        using JsonDocument? document = await GetAsync($"search/autocomplete/{Uri.EscapeDataString(title)}", cancellationToken).ConfigureAwait(false);
        if (document is null || !document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array) return [];
        List<ArtworkMatchCandidate> result = [];
        foreach (JsonElement item in data.EnumerateArray().Take(Math.Clamp(request.Limit, 1, 50)))
        {
            if (!item.TryGetProperty("id", out JsonElement id) || !item.TryGetProperty("name", out JsonElement name)) continue;
            result.Add(new(Id, id.ToString(), name.GetString() ?? title, request.PlatformId ?? request.Identity.PlatformId, request.Region ?? request.Identity.Region, null));
        }
        return result;
    }

    public async Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        List<RemoteArtworkAsset> result = [];
        foreach (ArtworkKind type in request.AssetTypes.Distinct())
        {
            string? endpoint = type switch { ArtworkKind.CoverPortrait or ArtworkKind.GridWide => "grids", ArtworkKind.HeroBackground => "heroes", ArtworkKind.Logo => "logos", ArtworkKind.Icon => "icons", _ => null };
            if (endpoint is null) continue;
            using JsonDocument? document = await GetAsync($"{endpoint}/game/{Uri.EscapeDataString(request.ProviderGameId)}?types=static&limit=50", cancellationToken).ConfigureAwait(false);
            if (document is null || !document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array) continue;
            foreach (JsonElement item in data.EnumerateArray())
            {
                if (!TryHttps(item, "url", out Uri? url) || !item.TryGetProperty("id", out JsonElement id)) continue;
                int? width = Number(item, "width"); int? height = Number(item, "height");
                ArtworkKind mapped = endpoint == "grids" && width is not null && height is not null && width > height ? ArtworkKind.GridWide : type;
                TryHttps(item, "thumb", out Uri? thumbnail);
                Dictionary<string, string> attribution = [];
                if (item.TryGetProperty("author", out JsonElement author) && author.TryGetProperty("name", out JsonElement authorName) && authorName.GetString() is { } value) attribution["author"] = value;
                result.Add(new(Id, request.ProviderGameId, id.ToString(), mapped, url!, thumbnail, width, height, null, Mime(item), Attribution: attribution));
            }
        }
        return result;
    }

    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) => downloader.DownloadAsync(asset, request, cancellationToken);

    public async Task<ArtworkProviderStatus> TestConnectionAsync(CancellationToken cancellationToken=default)
    {
        ProviderCredential? credential=await credentials.GetAsync(Id,cancellationToken).ConfigureAwait(false);if(credential is null)return new(false,false,"credential_missing","Add your free SteamGridDB API key to enable this provider.");
        await using RateLimitLease lease=await limits.AcquireAsync(Id,cancellationToken).ConfigureAwait(false);using HttpRequestMessage message=new(HttpMethod.Get,new Uri(BaseUri,"games/id/1"));message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",credential.Secret);
        try{using HttpResponseMessage response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false);await limits.ReportResponseAsync(Id,new((int)response.StatusCode,Retry(response),AuthenticationFailed:response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),cancellationToken).ConfigureAwait(false);if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)return new(false,false,"authentication_failed","The SteamGridDB API key was rejected.");if((int)response.StatusCode==429)return new(false,true,"rate_limited","SteamGridDB temporarily limited requests.",QuotaResetsAtUtc:Retry(response));return new(true,true);}
        catch(HttpRequestException){return new(false,true,"offline","SteamGridDB could not be reached.");}
    }

    private async Task<JsonDocument?> GetAsync(string relative, CancellationToken cancellationToken)
    {
        ProviderCredential? credential = await credentials.GetAsync(Id, cancellationToken).ConfigureAwait(false);
        if (credential is null) return null;
        await using RateLimitLease lease = await limits.AcquireAsync(Id, cancellationToken).ConfigureAwait(false);
        using HttpRequestMessage message = new(HttpMethod.Get, new Uri(BaseUri, relative));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Secret);
        using HttpResponseMessage response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await limits.ReportResponseAsync(Id, new((int)response.StatusCode, Retry(response), AuthenticationFailed: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden), cancellationToken).ConfigureAwait(false);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new ArtworkProviderOperationException("authentication_failed","The SteamGridDB API key was rejected.");
        if((int)response.StatusCode==429)throw new ArtworkProviderOperationException("rate_limited","SteamGridDB temporarily limited requests.",Retry(response));
        if (!response.IsSuccessStatusCode) throw new ArtworkProviderOperationException("provider_error",$"SteamGridDB returned HTTP {(int)response.StatusCode}.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? Retry(HttpResponseMessage response) => response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is { } delay ? DateTimeOffset.UtcNow + delay : null);
    private static (string Platform, string Id, string IdentityKey)? ExternalIdentity(GameIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.SteamAppId)) return ("steam", identity.SteamAppId, "steam");
        if (string.Equals(identity.ProviderId, "epic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(identity.ProviderItemId)) return ("egs", identity.ProviderItemId, "epic");
        return null;
    }
    private static bool TryHttps(JsonElement value, string property, out Uri? uri) { uri = null; return value.TryGetProperty(property, out JsonElement element) && Uri.TryCreate(element.GetString(), UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps; }
    private static int? Number(JsonElement value, string property) => value.TryGetProperty(property, out JsonElement element) && element.TryGetInt32(out int result) ? result : null;
    private static string? Mime(JsonElement value) => value.TryGetProperty("mime", out JsonElement element) ? element.GetString() : null;
}

public sealed class TheGamesDbProvider(HttpClient http, IProviderCredentialStore credentials, IProviderRateLimitCoordinator limits, IRemoteArtworkDownloader downloader) : IArtworkProvider, IGameMetadataProvider, IArtworkProviderConnectionProbe
{
    private static readonly Uri BaseUri = new("https://api.thegamesdb.net/");
    public string Id => "thegamesdb";
    public string DisplayName => "TheGamesDB";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.TitleSearch | ArtworkProviderCapabilities.PlatformSearch | ArtworkProviderCapabilities.ExternalIdSearch | ArtworkProviderCapabilities.GameSerialSearch | ArtworkProviderCapabilities.HashSearch | ArtworkProviderCapabilities.CoverArtwork | ArtworkProviderCapabilities.WideArtwork | ArtworkProviderCapabilities.HeroArtwork | ArtworkProviderCapabilities.ScreenshotArtwork | ArtworkProviderCapabilities.Metadata | ArtworkProviderCapabilities.UserAuthentication | ArtworkProviderCapabilities.QuotaReporting;
    public async Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default) => await credentials.GetAsync(Id, cancellationToken).ConfigureAwait(false) is null ? new(false, false, "credential_missing", "Add your free TheGamesDB API key to enable this provider.") : new(true, true);

    public async Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        bool serialSearch=!string.IsNullOrWhiteSpace(request.Identity.GameSerial);string endpoint=serialSearch?"v1/Games/ByGameUniqueID":"v1.1/Games/ByGameName";Dictionary<string,string?> query=serialSearch?new(){["uid"]=request.Identity.GameSerial}:new(){["name"]=request.SearchTitle??request.Identity.OriginalTitle,["filter[platform]"]=ProviderPlatform(request.PlatformId??request.Identity.PlatformId)};
        using JsonDocument? document = await GetAsync(endpoint,query,cancellationToken).ConfigureAwait(false);
        if (document is null || !TryGames(document.RootElement, out JsonElement games)) return [];
        List<ArtworkMatchCandidate> result = [];
        foreach (JsonElement game in games.EnumerateArray().Take(Math.Clamp(request.Limit, 1, 50)))
        {
            string? id = Text(game, "id"); string? title = Text(game, "game_title") ?? Text(game, "title"); if (id is null || title is null) continue;
            result.Add(new(Id,id,title,CanonicalPlatform(Text(game,"platform")),null,Year(Text(game,"release_date")),GameSerial:serialSearch?request.Identity.GameSerial:null));
        }
        return result;
    }

    public async Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        using JsonDocument? document = await GetAsync("v1/Games/Images", new Dictionary<string, string?> { ["games_id"] = request.ProviderGameId }, cancellationToken).ConfigureAwait(false);
        if (document is null || !document.RootElement.TryGetProperty("data", out JsonElement data) || !data.TryGetProperty("images", out JsonElement images) || !images.TryGetProperty(request.ProviderGameId, out JsonElement list) || list.ValueKind != JsonValueKind.Array) return [];
        string? baseUrl = document.RootElement.TryGetProperty("include", out JsonElement include) && include.TryGetProperty("boxart", out JsonElement boxart) && boxart.TryGetProperty("base_url", out JsonElement baseUrls) ? Text(baseUrls, "original") : null;
        List<RemoteArtworkAsset> result = [];
        foreach (JsonElement image in list.EnumerateArray())
        {
            string? fileName = Text(image, "filename"); if (fileName is null || baseUrl is null || !Uri.TryCreate(new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/"), fileName, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps) continue;
            ArtworkKind type = MapType(Text(image, "type"), Text(image, "side"));if(type==ArtworkKind.BoxFront&&request.AssetTypes.Contains(ArtworkKind.CoverPortrait))type=ArtworkKind.CoverPortrait;if(type==ArtworkKind.BoxBack&&request.AssetTypes.Contains(ArtworkKind.CoverPortrait))continue;if (!request.AssetTypes.Contains(type)) continue;
            result.Add(new(Id, request.ProviderGameId, Text(image, "id") ?? fileName, type, uri, null, ParseInt(Text(image, "resolution"), true), ParseInt(Text(image, "resolution"), false), null, MimeFromName(fileName)));
        }
        return result;
    }
    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) => downloader.DownloadAsync(asset, request, cancellationToken);
    public async Task<ArtworkProviderStatus> TestConnectionAsync(CancellationToken cancellationToken=default)
    {
        try{using JsonDocument? document=await GetAsync("v1/API/Limit",new Dictionary<string,string?>(),cancellationToken).ConfigureAwait(false);if(document is null)return new(false,false,"authentication_failed","The TheGamesDB API key was rejected.");int? remaining=null;if(document.RootElement.TryGetProperty("remaining_monthly_allowance",out JsonElement direct)&&direct.TryGetInt32(out int directValue))remaining=directValue;else if(document.RootElement.TryGetProperty("data",out JsonElement data)&&data.TryGetProperty("remaining_monthly_allowance",out JsonElement nested)&&nested.TryGetInt32(out int nestedValue))remaining=nestedValue;return new(true,true,RemainingQuota:remaining);}
        catch(ArtworkProviderOperationException exception){return new(false,exception.FailureCode!="authentication_failed",exception.FailureCode,exception.Message,QuotaResetsAtUtc:exception.RetryAfterUtc);}catch(HttpRequestException){return new(false,true,"offline","TheGamesDB could not be reached.");}
    }
    async Task<IReadOnlyList<GameMetadataCandidate>> IGameMetadataProvider.SearchAsync(GameMetadataSearchRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ArtworkMatchCandidate> matches = await SearchAsync(new(request.Identity, request.SearchTitle, request.Identity.PlatformId, request.Identity.Region, Limit: request.Limit), cancellationToken).ConfigureAwait(false);
        return matches.Select(value => new GameMetadataCandidate(Id, value.ProviderGameId, value.Title, value.PlatformId, value.Region, value.ReleaseYear)).ToArray();
    }
    public async Task<GameMetadataResult> GetMetadataAsync(string providerGameId, CancellationToken cancellationToken = default)
    {
        using JsonDocument? document = await GetAsync("v1/Games/ByGameID", new Dictionary<string, string?> { ["id"] = providerGameId }, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string?> fields = [];
        if (document is not null && TryGames(document.RootElement, out JsonElement games) && games.EnumerateArray().FirstOrDefault() is JsonElement game && game.ValueKind == JsonValueKind.Object)
        {
            fields["Title"] = Text(game, "game_title"); fields["Description"] = Text(game, "overview"); fields["ReleaseDate"] = Text(game, "release_date"); fields["Platform"] = Text(game, "platform"); fields["Players"] = Text(game, "players");
        }
        return new(Id, providerGameId, fields);
    }

    private async Task<JsonDocument?> GetAsync(string relative, IReadOnlyDictionary<string, string?> query, CancellationToken cancellationToken)
    {
        ProviderCredential? credential = await credentials.GetAsync(Id, cancellationToken).ConfigureAwait(false); if (credential is null) return null;
        string pairs = string.Join('&', query.Where(pair => pair.Value is not null).Append(new("apikey", credential.Secret)).Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
        await using RateLimitLease lease = await limits.AcquireAsync(Id, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage response;
        try { response = await http.GetAsync(new Uri(BaseUri, relative + "?" + pairs), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false); }
        catch (HttpRequestException exception) { throw new HttpRequestException("TheGamesDB request failed without exposing its credential-bearing URL.", null, exception.StatusCode); }
        using (response)
        {
        await limits.ReportResponseAsync(Id, new((int)response.StatusCode, AuthenticationFailed: response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden), cancellationToken).ConfigureAwait(false);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new ArtworkProviderOperationException("authentication_failed","The TheGamesDB API key was rejected.");if((int)response.StatusCode==429)throw new ArtworkProviderOperationException("rate_limited","TheGamesDB temporarily limited requests.");if(!response.IsSuccessStatusCode)throw new ArtworkProviderOperationException("provider_error",$"TheGamesDB returned HTTP {(int)response.StatusCode}.");
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false); return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
    private static bool TryGames(JsonElement root, out JsonElement games) { games = default; return root.TryGetProperty("data", out JsonElement data) && data.TryGetProperty("games", out games) && games.ValueKind == JsonValueKind.Array; }
    private static string? Text(JsonElement value, string property) { if (!value.TryGetProperty(property, out JsonElement result)) return null; return result.ValueKind == JsonValueKind.String ? result.GetString() : result.ValueKind == JsonValueKind.Number ? result.ToString() : null; }
    private static int? Year(string? date) => DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value) ? value.Year : null;
    private static ArtworkKind MapType(string? type, string? side) => type?.ToLowerInvariant() switch { "boxart" when string.Equals(side, "back", StringComparison.OrdinalIgnoreCase) => ArtworkKind.BoxBack, "boxart" => ArtworkKind.BoxFront, "fanart" => ArtworkKind.HeroBackground, "screenshot" => ArtworkKind.Screenshot, "banner" => ArtworkKind.GridWide, _ => ArtworkKind.CoverPortrait };
    private static int? ParseInt(string? resolution, bool width) { if (resolution?.Split('x', 'X') is not { Length: 2 } parts) return null; return int.TryParse(parts[width ? 0 : 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null; }
    private static string? MimeFromName(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
    private static string? ProviderPlatform(string? platform)=>string.Equals(platform,"playstation-2",StringComparison.OrdinalIgnoreCase)?"Sony Playstation 2":platform;
    private static string? CanonicalPlatform(string? platform)=>platform?.Trim().ToLowerInvariant() switch{"11" or "sony playstation 2" or "playstation 2" or "sony playstation2"=>"playstation-2","1" or "pc"=>"pc",_=>platform};
}

public sealed class ArtworkProviderOperationException(string failureCode,string message,DateTimeOffset? retryAfterUtc=null) : Exception(message)
{
    public string FailureCode{get;}=failureCode;public DateTimeOffset? RetryAfterUtc{get;}=retryAfterUtc;
}
