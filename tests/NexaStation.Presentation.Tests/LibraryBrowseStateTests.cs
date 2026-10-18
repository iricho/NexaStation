using NexaStation.Domain;
using NexaStation.Presentation.Dashboard;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class LibraryBrowseStateTests
{
    [Fact]
    public void SearchSurvivesSortingAndFilteringWithoutChangingTheSource()
    {
        LibraryItem[] items = [Item("a", "Puzzle One") with { Favorite = true, TotalPlaytimeSeconds = 2 }, Item("b", "Puzzle Two") with { TotalPlaytimeSeconds = 10 }, Item("c", "Racing")];
        LibraryBrowseState state = new() { SearchText = "  puzzle  " };
        Assert.Equal(["a", "b"], state.Apply(items).Select(item => item.Id));
        state.SortMode = LibrarySortMode.MostPlayed;
        Assert.Equal(["b", "a"], state.Apply(items).Select(item => item.Id));
        state.View = "Favorites";
        Assert.Equal("a", Assert.Single(state.Apply(items)).Id);
        Assert.Equal(3, items.Length);
        Assert.Equal("  puzzle  ", state.SearchText);
    }

    [Theory]
    [InlineData("subtitle")]
    [InlineData("description")]
    [InlineData("100%_complete")]
    public void SearchMatchesLiteralMetadata(string query)
    {
        LibraryItem item = Item("a", "100%_complete") with { Subtitle = "A Subtitle", Description = "A Description" };
        Assert.Single(new LibraryBrowseState { SearchText = query }.Apply([item]));
        Assert.Empty(new LibraryBrowseState { SearchText = "not found" }.Apply([item]));
    }

    [Theory]
    [InlineData("All", "visible")]
    [InlineData("Favorites", "visible")]
    [InlineData("Hidden", "hidden")]
    public void HiddenItemsOnlyAppearInTheHiddenView(string view, string expected)
    {
        LibraryItem[] items = [Item("visible", "Game") with { Favorite = true }, Item("hidden", "Game") with { Hidden = true, Favorite = true }];
        Assert.Equal(expected, Assert.Single(new LibraryBrowseState { View = view }.Apply(items)).Id);
    }

    [Fact]
    public void TiedSortValuesHaveStableOrdering()
    {
        LibraryItem[] items = [Item("b", "Same"), Item("a", "Same")];
        foreach (LibrarySortMode sort in Enum.GetValues<LibrarySortMode>())
            Assert.Equal(["a", "b"], new LibraryBrowseState { SortMode = sort }.Apply(items).Select(item => item.Id));
    }

    [Fact]
    public void RecentViewUsesActivityAndExcludesUnplayedItems()
    {
        LibraryItem[] items = [Item("a", "A"), Item("b", "B") with { LastLaunchedAtUtc = DateTime.UtcNow.AddDays(-1) }, Item("c", "C") with { LastLaunchedAtUtc = DateTime.UtcNow }];
        Assert.Equal(["c", "b"], new LibraryBrowseState { View = "Recently Used" }.Apply(items).Select(item => item.Id));
    }

    [Fact]
    public void ResetRestoresTheDefaultView()
    {
        LibraryBrowseState state = new() { SearchText = "missing", View = "Hidden", Platform = "steam", Genre = "Action", SortMode = LibrarySortMode.MostPlayed };
        state.Reset();
        Assert.False(state.HasFilters);
        Assert.Equal(LibrarySortMode.Title, state.SortMode);
        Assert.Single(state.Apply([Item("a", "Game")]));
    }

    [Fact]
    public void SourceAndGenreIntersectSearchAndFavoritesWithoutExposingHiddenItems()
    {
        LibraryItem match = Item("a", "Space Adventure") with { Favorite = true, PrimaryCategoryName = "Action", LaunchTargets = [new(LaunchType.Uri, Uri: new Uri("steam://run/1")) { ProviderId = "steam" }] };
        LibraryItem[] items = [match, match with { Id = "hidden", Hidden = true }, match with { Id = "genre", PrimaryCategoryName = "RPG" }, match with { Id = "source", LaunchTargets = [] }, match with { Id = "favorite", Favorite = false }];
        LibraryBrowseState state = new() { SearchText = "space", View = "Favorites", Platform = "STEAM", Genre = "action" };
        Assert.True(state.HasFilters);
        Assert.Equal("a", Assert.Single(state.Apply(items)).Id);
        state.Reset();
        Assert.Equal(4, state.Apply(items).Count);
    }

    [Fact]
    public void LocalSourceIncludesItemsWithoutAProvider()
    {
        Assert.Single(new LibraryBrowseState { Platform = "Local" }.Apply([Item("a", "Local game")]));
        Assert.Empty(new LibraryBrowseState { Genre = "Unknown" }.Apply([Item("a", "Local game")]));
    }

    [Fact]
    public void DisabledTargetsAreMissingRatherThanValid()
    {
        LibraryItem item = Item("a", "Game") with { LaunchTargets = [new(LaunchType.Uri, Uri: new Uri("steam://run/1")) { IsEnabled = false }] };
        Assert.Single(new LibraryBrowseState { View = "Missing targets" }.Apply([item]));
        Assert.Empty(new LibraryBrowseState { View = "Valid targets" }.Apply([item]));
    }

    private static LibraryItem Item(string id, string title) => new() { Id = id, Title = title, SortTitle = title, ContentType = ContentType.Game };
}
