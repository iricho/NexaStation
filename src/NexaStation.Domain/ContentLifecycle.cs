namespace NexaStation.Domain;

public enum ContentLifecycleStatus
{
    Unassigned = 0,
    Backlog = 1,
    Playing = 2,
    Completed = 3,
    Dropped = 4,
}

public enum ContentLifecycleTargetType
{
    LibraryItem = 0,
    GalleryMediaItem = 1,
}

public readonly record struct ContentLifecycleTarget(ContentLifecycleTargetType TargetType, string TargetId)
{
    public static ContentLifecycleTarget Library(string id) => new(ContentLifecycleTargetType.LibraryItem, id);
    public static ContentLifecycleTarget GalleryMedia(string id) => new(ContentLifecycleTargetType.GalleryMediaItem, id);
}

public sealed record ContentLifecycleEntry(
    ContentLifecycleTarget Target,
    ContentLifecycleStatus Status,
    DateTime UpdatedAtUtc);

public sealed record ContentLifecycleQuery(
    ContentLifecycleTargetType? TargetType = null,
    ContentLifecycleStatus? Status = null,
    int MaximumResults = 500);
