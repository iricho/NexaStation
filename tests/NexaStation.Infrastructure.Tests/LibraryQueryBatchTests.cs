using NexaStation.Domain;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class LibraryQueryBatchTests
{
    private static readonly string[] ExpectedArguments = ["--fullscreen", "a path with spaces"];
    [Fact]
    public async Task BatchHydrationPreservesTargetsAndArtworkAcrossBatchBoundaries()
    {
        await using var db = await PersistenceTests.TestDb.Create();
        for (int index = 0; index < 205; index++)
        {
            LibraryItem item = Item(index);
            await db.Library.AddAsync(item);
        }

        IReadOnlyList<LibraryItem> items = await db.Library.QueryAsync(new(MaximumResults: 300));
        Assert.Equal(205, items.Count);
        foreach (LibraryItem item in items)
        {
            Assert.Equal(item.Id, Assert.Single(item.LaunchTargets).LibraryItemId);
            Assert.Equal(item.Id + "-target", item.DefaultLaunchTargetId);
            Assert.Equal(item.Id + "-cover", item.CoverAsset?.Id);
            Assert.Equal(item.Id + "-background", item.BackgroundAsset?.Id);
            Assert.Equal(ExpectedArguments, item.LaunchTarget!.ArgumentList);
            Assert.Equal("value", item.LaunchTarget.EnvironmentOverrides["TEST"]);
        }
        LibraryItem single = (await db.Library.GetByIdAsync("0200"))!;
        Assert.Equal(items[200].LaunchTarget!.Id, single.LaunchTarget!.Id);
        Assert.Equal(items[200].LaunchTarget!.Uri, single.LaunchTarget!.Uri);
        Assert.Equal(items[200].LaunchTarget!.ArgumentList, single.LaunchTarget!.ArgumentList);
        Assert.Equal(items[200].CoverAsset, single.CoverAsset);
    }

    [Fact]
    public async Task PagesWithTiedTitlesHaveNoGapsOrDuplicates()
    {
        await using var db = await PersistenceTests.TestDb.Create();
        for (int index = 8; index >= 0; index--) await db.Library.AddAsync(Item(index) with { Title = "Same", SortTitle = "Same" });
        List<string> ids = [];
        for (int offset = 0; offset < 12; offset += 3)
            ids.AddRange((await db.Library.QueryAsync(new(MaximumResults: 3, Offset: offset))).Select(item => item.Id));
        Assert.Equal(Enumerable.Range(0, 9).Select(index => index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)), ids);
        Assert.Empty(await db.Library.QueryAsync(new(Offset: 20)));
    }

    [Fact]
    public async Task FilteringHappensBeforePagingAndEmptyChildrenStayEmpty()
    {
        await using var db = await PersistenceTests.TestDb.Create();
        await db.Library.AddAsync(Item(0) with { Hidden = true });
        await db.Library.AddAsync(Item(1) with { Favorite = true });
        await db.Library.AddAsync(Item(2) with { Favorite = true, CoverAsset = null, BackgroundAsset = null, LaunchTargets = [], DefaultLaunchTargetId = null });
        LibraryItem item = Assert.Single(await db.Library.QueryAsync(new(FavoriteOnly: true, MaximumResults: 1, Offset: 1)));
        Assert.Equal("0002", item.Id);
        Assert.Empty(item.LaunchTargets);
        Assert.Null(item.CoverAsset);
        Assert.Null(item.BackgroundAsset);
        Assert.Null(item.PrimaryCategoryName);
        Assert.False(item.DesktopControlEnabled);
    }

    [Fact]
    public async Task CancelledQueryDoesNotReturnPartialResults()
    {
        await using var db = await PersistenceTests.TestDb.Create();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Library.QueryAsync(new(), cancellation.Token));
    }

    private static LibraryItem Item(int index)
    {
        string id = index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        DateTime at = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        LaunchTarget target = new(LaunchType.Uri, Uri: new Uri("steam://run/" + index))
        {
            Id = id + "-target", LibraryItemId = id, CreatedAtUtc = at, UpdatedAtUtc = at,
            ArgumentList = ["--fullscreen", "a path with spaces"], EnvironmentOverrides = new Dictionary<string, string> { ["TEST"] = "value" },
        };
        return new()
        {
            Id = id, Title = id, SortTitle = id, AddedAtUtc = at, UpdatedAtUtc = at, ContentType = ContentType.Game,
            LaunchTargets = [target], DefaultLaunchTargetId = target.Id,
            CoverAsset = new(id + "-cover", id, AssetType.Cover, id + ".png", null, null, null, at),
            BackgroundAsset = new(id + "-background", id, AssetType.Background, id + "-wide.png", null, null, null, at),
        };
    }
}
