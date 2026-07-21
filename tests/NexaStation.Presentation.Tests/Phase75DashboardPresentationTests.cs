using NexaStation.Domain;
using NexaStation.Presentation.Dashboard;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class Phase75DashboardPresentationTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NexaStation.Phase75." + Guid.NewGuid().ToString("N"));
    private readonly ArtworkPresentationService _artwork = new();

    public Phase75DashboardPresentationTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void UserFeaturedItemWinsWhenEligibleAndWide()
    {
        LibraryItem recent = Item("recent", ContentType.Game, launched: DateTime.UtcNow, background: Wide("recent"));
        LibraryItem chosen = Item("chosen", ContentType.Game, launched: DateTime.UtcNow.AddDays(-2), background: Wide("chosen"));
        HeroSelectionResult result = Hero().SelectHero(new([recent, chosen], [], chosen.Id));
        Assert.Equal(chosen.Id, result.Item?.Id);
        Assert.Equal("UserFeatured", result.Rule);
    }

    [Fact]
    public void PinnedFeaturedItemWinsBeforeAutomaticRules()
    {
        LibraryItem recent = Item("recent", ContentType.Game, launched: DateTime.UtcNow, background: Wide("recent"));
        LibraryItem pinned = Item("pinned", ContentType.Game, launched: DateTime.UtcNow.AddDays(-5), background: Wide("pinned"));
        HeroSelectionResult result = Hero().SelectHero(new([recent, pinned], [pinned.Id]));
        Assert.Equal(pinned.Id, result.Item?.Id);
        Assert.Equal("Pinned", result.Rule);
    }

    [Fact]
    public void HiddenAndBrokenItemsAreExcludedFromHero()
    {
        LibraryItem hidden = Item("hidden", ContentType.Game, launched: DateTime.UtcNow, background: Wide("hidden")) with { Hidden = true };
        LibraryItem broken = Item("broken", ContentType.Game, launched: DateTime.UtcNow, background: Wide("broken")) with { LaunchTargets = [] };
        HeroSelectionResult result = Hero().SelectHero(new([hidden, broken], []));
        Assert.True(result.IsFallback);
        Assert.Null(result.Item);
    }

    [Fact]
    public void RecentFavoriteGameWinsAutomaticSelection()
    {
        LibraryItem favorite = Item("favorite", ContentType.Game, favorite: true, launched: DateTime.UtcNow.AddDays(-2), background: Wide("favorite"));
        LibraryItem newer = Item("newer", ContentType.Game, launched: DateTime.UtcNow, background: Wide("newer"));
        HeroSelectionResult result = Hero().SelectHero(new([newer, favorite], []));
        Assert.Equal(favorite.Id, result.Item?.Id);
        Assert.Equal("RecentFavoriteGame", result.Rule);
    }

    [Fact]
    public void RecentGameFallbackUsesLastLaunchTime()
    {
        LibraryItem older = Item("older", ContentType.Game, launched: DateTime.UtcNow.AddDays(-3), background: Wide("older"));
        LibraryItem newer = Item("newer", ContentType.Game, launched: DateTime.UtcNow.AddHours(-1), background: Wide("newer"));
        HeroSelectionResult result = Hero().SelectHero(new([older, newer], []));
        Assert.Equal(newer.Id, result.Item?.Id);
        Assert.Equal("RecentGame", result.Rule);
    }

    [Fact]
    public void HeroFallsBackWhenArtworkIsMissingOrNarrow()
    {
        LibraryItem missing = Item("missing", ContentType.Game, launched: DateTime.UtcNow);
        LibraryItem narrow = Item("narrow", ContentType.Game, launched: DateTime.UtcNow, background: Narrow("narrow"));
        HeroSelectionResult result = Hero().SelectHero(new([missing, narrow], []));
        Assert.Equal("NexaStationFallback", result.Rule);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public void HeroSelectionIsDeterministic()
    {
        LibraryItem item = Item("stable", ContentType.Game, launched: DateTime.UtcNow, background: Wide("stable"));
        HeroSelectionRequest request = new([item], []);
        Assert.Equal(Hero().SelectHero(request), Hero().SelectHero(request));
    }

    [Fact]
    public void ContinuePlayingUsesRecentLaunchesThenNewLibraryGames()
    {
        LibraryItem never = Item("never", ContentType.Game) with { AddedAtUtc = DateTime.UtcNow };
        LibraryItem played = Item("played", ContentType.Game, launched: DateTime.UtcNow.AddHours(-1));
        DashboardSemanticContent result = Semantics(never, played, Item("app", ContentType.Application, launched: DateTime.UtcNow));
        Assert.Equal(["played", "never"], result.ContinuePlaying.Select(item => item.Id));
    }

    [Fact]
    public void RecentlyUsedApplicationsUseRecentLaunchesThenNewApplications()
    {
        LibraryItem unused = Item("unused", ContentType.Application) with { AddedAtUtc = DateTime.UtcNow };
        LibraryItem used = Item("used", ContentType.Application, launched: DateTime.UtcNow.AddHours(-1));
        DashboardSemanticContent result = Semantics(unused, used);
        Assert.Equal(["used", "unused"], result.RecentlyUsedApplications.Select(item => item.Id));
    }

    [Fact]
    public void ActivityRowsOrderPlayedItemsByMostRecentLaunch()
    {
        LibraryItem older = Item("older", ContentType.Game, launched: DateTime.UtcNow.AddDays(-1));
        LibraryItem newer = Item("newer", ContentType.Game, launched: DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(["newer", "older"], Semantics(older, newer).ContinuePlaying.Select(item => item.Id));
    }

    [Fact]
    public void FavoritesContainOnlyVisibleFavorites()
    {
        LibraryItem hidden = Item("hidden", ContentType.Game, favorite: true) with { Hidden = true };
        DashboardSemanticContent result = Semantics(Item("plain", ContentType.Game), Item("favorite", ContentType.Application, favorite: true), hidden);
        Assert.Single(result.Favorites);
        Assert.Equal("favorite", result.Favorites[0].Id);
    }

    [Fact]
    public void RecentlyAddedUsesAddedTimestamp()
    {
        DateTime now = DateTime.UtcNow;
        LibraryItem older = Item("older", ContentType.Game) with { AddedAtUtc = now.AddDays(-1) };
        LibraryItem newer = Item("newer", ContentType.Game) with { AddedAtUtc = now };
        Assert.Equal(["newer", "older"], Semantics(older, newer).RecentlyAdded.Select(item => item.Id));
    }

    [Fact]
    public void MediaServicesUseContentTypeAndApprovedCategories()
    {
        LibraryItem categorized = Item("music", ContentType.Application) with { PrimaryCategoryName = "Music" };
        DashboardSemanticContent result = Semantics(Item("media", ContentType.Media), categorized, Item("tool", ContentType.Application));
        Assert.Equal(["media", "music"], result.MediaServices.Select(item => item.Id).OrderBy(id => id));
    }

    [Fact]
    public void ArtworkResolverReturnsValidLocalArtwork()
    {
        LibraryItem item = Item("art", ContentType.Game, cover: Narrow("cover"));
        ArtworkPresentation result = _artwork.Resolve(item, ArtworkRequestKind.Card, 480, 270);
        Assert.True(result.Available);
        Assert.Equal(item.CoverAsset?.LocalPath, result.LocalPath);
    }

    [Fact]
    public void ArtworkResolverRejectsMissingAndUnsupportedFiles()
    {
        Asset unsupported = new("bad", "item", AssetType.Cover, File("bad.txt"), 800, 450, 3, DateTime.UtcNow);
        ArtworkPresentation result = _artwork.Resolve(Item("item", ContentType.Game, cover: unsupported), ArtworkRequestKind.Card, 480, 270);
        Assert.False(result.Available);
        Assert.NotNull(result.FallbackReason);
    }

    [Fact]
    public void HeroArtworkRejectsVerticalCoverStretching()
    {
        ArtworkPresentation result = _artwork.Resolve(Item("item", ContentType.Game, background: Narrow("vertical")), ArtworkRequestKind.Hero, 1600, 500);
        Assert.False(result.Available);
        Assert.Contains("not wide", result.FallbackReason, StringComparison.OrdinalIgnoreCase);
    }

    private HeroSelectionService Hero() => new(_artwork);
    private static DashboardSemanticContent Semantics(params LibraryItem[] items) => new DashboardSemanticService().Build(items);

    private static LibraryItem Item(string id, ContentType type, bool favorite = false, DateTime? launched = null, Asset? cover = null, Asset? background = null)
    {
        LaunchTarget target = new(LaunchType.Uri, Uri: new Uri("https://example.invalid/" + id)) { Id = id + "-target", LibraryItemId = id };
        return new()
        {
            Id = id,
            Title = id,
            SortTitle = id,
            ContentType = type,
            Favorite = favorite,
            AddedAtUtc = DateTime.UtcNow.AddHours(-1),
            UpdatedAtUtc = DateTime.UtcNow,
            LastLaunchedAtUtc = launched,
            CoverAsset = cover,
            BackgroundAsset = background,
            DefaultLaunchTargetId = target.Id,
            LaunchTargets = [target],
        };
    }

    private Asset Wide(string name) => new(name, name, AssetType.Background, File(name + ".png"), 1600, 700, 4, DateTime.UtcNow);
    private Asset Narrow(string name) => new(name, name, AssetType.Cover, File(name + ".png"), 300, 600, 4, DateTime.UtcNow);
    private string File(string name)
    {
        string path = Path.Combine(_folder, name);
        System.IO.File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); }
        catch (IOException) { }
    }
}
