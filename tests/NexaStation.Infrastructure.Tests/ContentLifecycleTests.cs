using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class ContentLifecycleTests
{
    [Fact]
    public async Task AbsenceIsUnassignedAndUnassignedClearsTheRow()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteContentLifecycleService service=new(db.Factory);ContentLifecycleTarget target=ContentLifecycleTarget.Library("game-1");
        Assert.Equal(ContentLifecycleStatus.Unassigned,await service.GetStatusAsync(target));await service.SetStatusAsync(target,ContentLifecycleStatus.Backlog);Assert.Equal(ContentLifecycleStatus.Backlog,await service.GetStatusAsync(target));await service.SetStatusAsync(target,ContentLifecycleStatus.Unassigned);Assert.Equal(ContentLifecycleStatus.Unassigned,await service.GetStatusAsync(target));Assert.Equal(0,await db.Scalar("SELECT COUNT(*) FROM ContentLifecycleStatuses"));
    }

    [Fact]
    public async Task QueryFiltersByTypedTargetAndStatus()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteContentLifecycleService service=new(db.Factory);await service.SetStatusAsync(ContentLifecycleTarget.Library("game"),ContentLifecycleStatus.Playing);await service.SetStatusAsync(ContentLifecycleTarget.GalleryMedia("video"),ContentLifecycleStatus.Playing);await service.SetStatusAsync(ContentLifecycleTarget.Library("done"),ContentLifecycleStatus.Completed);
        IReadOnlyList<ContentLifecycleEntry> results=await service.QueryAsync(new(ContentLifecycleTargetType.LibraryItem,ContentLifecycleStatus.Playing));Assert.Single(results);Assert.Equal("game",results[0].Target.TargetId);
    }

    [Fact]
    public async Task StaleTargetCleanupOnlyTouchesRequestedTargetType()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteContentLifecycleService service=new(db.Factory);await service.SetStatusAsync(ContentLifecycleTarget.Library("keep"),ContentLifecycleStatus.Backlog);await service.SetStatusAsync(ContentLifecycleTarget.Library("stale"),ContentLifecycleStatus.Dropped);await service.SetStatusAsync(ContentLifecycleTarget.GalleryMedia("stale"),ContentLifecycleStatus.Backlog);
        Assert.Equal(1,await service.ClearStaleTargetsAsync(ContentLifecycleTargetType.LibraryItem,["keep"]));Assert.Equal(ContentLifecycleStatus.Unassigned,await service.GetStatusAsync(ContentLifecycleTarget.Library("stale")));Assert.Equal(ContentLifecycleStatus.Backlog,await service.GetStatusAsync(ContentLifecycleTarget.GalleryMedia("stale")));
    }

    [Fact]
    public async Task InterfaceSnapshotDoesNotExportOrClearLifecycleData()
    {
        await using PersistenceTests.TestDb db=await PersistenceTests.TestDb.Create();SqliteContentLifecycleService service=new(db.Factory);await service.SetStatusAsync(ContentLifecycleTarget.Library("game"),ContentLifecycleStatus.Completed);await new SqliteCustomizationStore(db.Factory).ClearInterfaceCustomizationAsync();Assert.Equal(ContentLifecycleStatus.Completed,await service.GetStatusAsync(ContentLifecycleTarget.Library("game")));
    }
}
