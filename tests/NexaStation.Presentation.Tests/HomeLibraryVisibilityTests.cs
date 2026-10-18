using NexaStation.Domain;
using NexaStation.Presentation.Dashboard;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class HomeLibraryVisibilityTests
{
    [Fact]
    public void ShelfCannotReintroduceHiddenOrGuestRestrictedItems()
    {
        LibraryItem visible = Item("visible");
        LibraryItem hidden = Item("hidden") with { Hidden = true };
        LibraryItem restricted = Item("restricted");
        Dictionary<string, LibraryItem> authorized = new() { [visible.Id] = visible, [hidden.Id] = hidden };
        Assert.Equal(visible, Assert.Single(HomeLibraryVisibilityPolicy.Resolve([hidden with { Hidden = false }, restricted, visible, visible], authorized)));
    }

    [Fact]
    public void ShelfUsesCurrentMetadataAndKeepsItsOrder()
    {
        LibraryItem first = Item("first") with { Title = "Current title" };
        LibraryItem second = Item("second");
        Dictionary<string, LibraryItem> authorized = new() { [first.Id] = first, [second.Id] = second };
        Assert.Equal(new[] { second, first }, HomeLibraryVisibilityPolicy.Resolve([second, first with { Title = "Stale title" }], authorized));
    }

    private static LibraryItem Item(string id) => new() { Id = id, Title = id, SortTitle = id };
}
