using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Metadata;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class Phase11StructuredProviderTests
{
    [Fact]
    public async Task SteamPublicArtworkUsesExactAppIdWithoutCredentials()
    {
        using RemoteArtworkDownloader downloader=new();SteamPublicArtworkProvider provider=new(downloader);GameIdentity identity=new("item",ContentType.Game,"portal 2","Portal 2","pc",SteamAppId:"620");
        ArtworkProviderStatus status=await provider.GetStatusAsync();ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));IReadOnlyList<RemoteArtworkAsset> assets=await provider.GetAssetsAsync(new(match.ProviderGameId,[ArtworkKind.CoverPortrait,ArtworkKind.HeroBackground]));
        Assert.True(status.Available&&status.Authenticated);Assert.Equal("620",match.ExternalIds!["steam"]);Assert.Contains(assets,asset=>asset.DownloadUri.AbsoluteUri.EndsWith("/620/library_600x900_2x.jpg",StringComparison.Ordinal));Assert.Contains(assets,asset=>asset.DownloadUri.AbsoluteUri.EndsWith("/620/library_hero.jpg",StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ps2CommunityCoversUsesCanonicalSerialWithoutCredentials()
    {
        using RemoteArtworkDownloader downloader=new();Ps2CoverArtworkProvider provider=new(downloader);GameIdentity identity=new("item",ContentType.Game,"god of war ii","God of War II","playstation-2",GameSerial:"scus_974.81",Confidence:IdentityConfidence.VerifiedSerial);
        ArtworkProviderStatus status=await provider.GetStatusAsync();ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));RemoteArtworkAsset asset=Assert.Single(await provider.GetAssetsAsync(new(match.ProviderGameId,[ArtworkKind.CoverPortrait])));
        Assert.True(status.Available&&status.Authenticated);Assert.Equal("SCUS-97481",match.GameSerial);Assert.Equal("https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/SCUS-97481.jpg",asset.DownloadUri.AbsoluteUri);
    }

    [Fact]
    public async Task SteamGridDbUsesBearerAuthenticationAndStaticAssetFilter()
    {
        SequenceHandler handler=new(
            """{"success":true,"data":{"id":42,"name":"Example Game"}}""",
            """{"success":true,"data":[{"id":7,"url":"https://cdn.example.test/grid.png","thumb":"https://cdn.example.test/thumb.png","width":600,"height":900,"author":{"name":"Artist"}}]}""");
        using RemoteArtworkDownloader downloader=new(new HttpClient(handler));SteamGridDbArtworkProvider provider=new(new HttpClient(handler),new FakeCredentials(),new PassThroughLimits(),downloader);GameIdentity identity=new("item",ContentType.Game,"example game","Example Game","pc",SteamAppId:"1091500");
        ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));RemoteArtworkAsset asset=Assert.Single(await provider.GetAssetsAsync(new(match.ProviderGameId,[ArtworkKind.CoverPortrait])));
        Assert.Equal("steamgriddb",asset.ProviderId);Assert.Equal("1091500",match.ExternalIds!["steam"]);Assert.Equal("Artist",asset.Attribution!["author"]);Assert.Contains("games/steam/1091500",handler.Requests[0].Uri,StringComparison.Ordinal);Assert.All(handler.Requests,request=>Assert.Equal("Bearer secret",request.Authorization));Assert.Contains("types=static",handler.Requests[1].Uri,StringComparison.Ordinal);
    }

    [Fact]
    public async Task SteamGridDbUsesExactEpicCatalogIdentity()
    {
        const string catalogId="0f691c8b-57ff-45e3-a063-f807d2365c26";
        SequenceHandler handler=new("""{"success":true,"data":{"id":84,"name":"Epic Example"}}""");
        using RemoteArtworkDownloader downloader=new(new HttpClient(handler));SteamGridDbArtworkProvider provider=new(new HttpClient(handler),new FakeCredentials(),new PassThroughLimits(),downloader);
        GameIdentity identity=new("item",ContentType.Game,"epic example","Epic Example","pc","epic",catalogId,Confidence:IdentityConfidence.VerifiedProviderId);
        ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));
        Assert.Equal(catalogId,match.ExternalIds!["epic"]);
        Assert.Contains($"games/egs/{catalogId}",handler.Requests[0].Uri,StringComparison.Ordinal);
    }

    [Fact]
    public async Task GogPublicArtworkUsesExactProductIdForCoverAndHero()
    {
        SequenceHandler handler=new(
            """{"id":1207658924,"title":"The Witcher: Enhanced Edition","images":{"background":"//images.gog.test/background.jpg"}}""",
            """{"products":[{"id":"1207658924","title":"The Witcher: Enhanced Edition","coverVertical":"https://images.gog.test/cover.jpg","galaxyBackgroundImage":"https://images.gog.test/hero.jpg"}]}""");
        using RemoteArtworkDownloader downloader=new(new HttpClient(handler));GogPublicArtworkProvider provider=new(new HttpClient(handler),downloader);
        GameIdentity identity=new("item",ContentType.Game,"the witcher enhanced edition","The Witcher: Enhanced Edition","pc","gog","1207658924",Confidence:IdentityConfidence.VerifiedProviderId);
        ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));
        IReadOnlyList<RemoteArtworkAsset> assets=await provider.GetAssetsAsync(new(match.ProviderGameId,[ArtworkKind.CoverPortrait,ArtworkKind.HeroBackground]));
        Assert.Equal("1207658924",match.ExternalIds!["gog"]);
        Assert.Contains(assets,value=>value.Type==ArtworkKind.CoverPortrait&&value.DownloadUri.AbsoluteUri=="https://images.gog.test/cover.jpg");
        Assert.Contains(assets,value=>value.Type==ArtworkKind.HeroBackground&&value.DownloadUri.AbsoluteUri=="https://images.gog.test/hero.jpg");
        Assert.Contains("api.gog.com/products/1207658924",handler.Requests[0].Uri,StringComparison.Ordinal);
        Assert.Contains("catalog.gog.com/v1/catalog",handler.Requests[1].Uri,StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheGamesDbMapsStructuredSearchArtworkAndMetadata()
    {
        SequenceHandler handler=new(
            """{"data":{"games":[{"id":42,"game_title":"Example Game","platform":"PC","release_date":"2020-01-02"}]}}""",
            """{"data":{"images":{"42":[{"id":9,"type":"boxart","side":"front","filename":"cover.png","resolution":"600x900"}]}},"include":{"boxart":{"base_url":{"original":"https://cdn.example.test/original/"}}}}""",
            """{"data":{"games":[{"id":42,"game_title":"Example Game","overview":"Description","release_date":"2020-01-02","platform":"PC"}]}}""");
        using RemoteArtworkDownloader downloader=new(new HttpClient(handler));TheGamesDbProvider provider=new(new HttpClient(handler),new FakeCredentials(),new PassThroughLimits(),downloader);GameIdentity identity=new("item",ContentType.Game,"example game","Example Game","pc");
        ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));RemoteArtworkAsset asset=Assert.Single(await provider.GetAssetsAsync(new(match.ProviderGameId,[ArtworkKind.CoverPortrait])));GameMetadataResult metadata=await provider.GetMetadataAsync(match.ProviderGameId);
        Assert.Equal(2020,match.ReleaseYear);Assert.Equal(ArtworkKind.CoverPortrait,asset.Type);Assert.Equal(new Uri("https://cdn.example.test/original/cover.png"),asset.DownloadUri);Assert.Equal("Description",metadata.Fields["Description"]);Assert.All(handler.Requests,request=>Assert.Contains("apikey=secret",request.Uri,StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheGamesDbUsesPs2SerialBeforeTitleSearchAndCanonicalizesPlatform()
    {
        SequenceHandler handler=new("""{"data":{"games":[{"id":84,"game_title":"God of War II","platform":"Sony Playstation 2"}]}}""");
        using RemoteArtworkDownloader downloader=new(new HttpClient(handler));TheGamesDbProvider provider=new(new HttpClient(handler),new FakeCredentials(),new PassThroughLimits(),downloader);GameIdentity identity=new("item",ContentType.Game,"god of war ii","God of War II","playstation-2",GameSerial:"SCUS-97481",Confidence:IdentityConfidence.VerifiedSerial);
        ArtworkMatchCandidate match=Assert.Single(await provider.SearchAsync(new(identity)));
        Assert.Contains("ByGameUniqueID",handler.Requests[0].Uri,StringComparison.Ordinal);Assert.Contains("uid=SCUS-97481",handler.Requests[0].Uri,StringComparison.Ordinal);Assert.Equal("playstation-2",match.PlatformId);
    }

    [Fact]
    public async Task ScreenScraperIsExplicitlyDisabledWithoutApprovedDeveloperCredentials()
    {
        using RemoteArtworkDownloader downloader=new();ScreenScraperArtworkProvider provider=new(downloader);ArtworkProviderStatus status=await provider.GetStatusAsync();Assert.False(status.Available);Assert.Equal("credential_strategy_unavailable",status.FailureCode);Assert.Empty(await provider.SearchAsync(new(new("item",ContentType.Game,"game","Game"))));
    }

    [Fact]
    public async Task TheGamesDbTransportFailureDoesNotExposeCredentialBearingUrl()
    {
        using RemoteArtworkDownloader downloader=new();TheGamesDbProvider provider=new(new HttpClient(new ThrowingHandler()),new FakeCredentials(),new PassThroughLimits(),downloader);HttpRequestException exception=await Assert.ThrowsAsync<HttpRequestException>(()=>provider.SearchAsync(new(new("item",ContentType.Game,"game","Game"))));Assert.DoesNotContain("secret",exception.ToString(),StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("apikey",exception.ToString(),StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MetadataImplementationsContainNoHtmlScrapingFallback()
    {
        string sourceRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","src","NexaStation.Infrastructure","Metadata"));string source=string.Join('\n',Directory.EnumerateFiles(sourceRoot,"*.cs").Select(File.ReadAllText));Assert.DoesNotContain("HtmlAgilityPack",source,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("AngleSharp",source,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("document.querySelector",source,StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeCredentials:IProviderCredentialStore
    {
        public Task SaveAsync(ProviderCredential credential,CancellationToken cancellationToken=default)=>Task.CompletedTask;
        public Task<ProviderCredential?> GetAsync(string providerId,CancellationToken cancellationToken=default)=>Task.FromResult<ProviderCredential?>(new(providerId,"secret"));
        public Task DeleteAsync(string providerId,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    }
    private sealed class PassThroughLimits:IProviderRateLimitCoordinator
    {
        public Task<RateLimitLease> AcquireAsync(string providerId,CancellationToken cancellationToken=default)=>Task.FromResult(new RateLimitLease(providerId,DateTimeOffset.UtcNow));
        public Task ReportResponseAsync(string providerId,ProviderResponseMetadata metadata,CancellationToken cancellationToken=default)=>Task.CompletedTask;
    }
    private sealed class SequenceHandler(params string[] responses):HttpMessageHandler
    {
        private int _index;public List<(string Uri,string? Authorization)> Requests{get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Requests.Add((request.RequestUri!.AbsoluteUri,request.Headers.Authorization?.ToString()));string body=responses[Math.Min(_index++,responses.Length-1)];return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new StringContent(body,Encoding.UTF8,"application/json")});}
    }
    private sealed class ThrowingHandler:HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>throw new HttpRequestException($"Failed {request.RequestUri}"); }
}
