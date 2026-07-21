namespace NexaStation.Domain;

public sealed record LibraryItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string SortTitle { get; init; }
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public ContentType ContentType { get; init; }
    public bool Favorite { get; init; }
    public bool Hidden { get; init; }
    public DateTime AddedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastLaunchedAtUtc { get; init; }
    public long TotalPlaytimeSeconds { get; init; }
    public string? DefaultLaunchTargetId { get; init; }
    public Asset? CoverAsset { get; init; }
    public Asset? BackgroundAsset { get; init; }
    public ApplicationCardStyle CardStyle { get; init; } = ApplicationCardStyle.Wide;
    public string? PrimaryCategoryName { get; init; }
    public bool DesktopControlEnabled { get; init; }
    public IReadOnlyList<LaunchTarget> LaunchTargets { get; init; } = [];

    public LaunchTarget? LaunchTarget
    {
        get
        {
            for (int index = 0; index < LaunchTargets.Count; index++)
                if (LaunchTargets[index].Id == DefaultLaunchTargetId && LaunchTargets[index].IsEnabled) return LaunchTargets[index];
            for (int index = 0; index < LaunchTargets.Count; index++)
                if (LaunchTargets[index].IsEnabled) return LaunchTargets[index];
            return LaunchTargets.Count == 0 ? null : LaunchTargets.FirstOrDefault(target => target.Id == DefaultLaunchTargetId) ?? LaunchTargets[0];
        }
    }

    public static LibraryItem Create(string title, ContentType contentType, LaunchTarget target, DateTime? nowUtc = null)
    {
        DateTime now = (nowUtc ?? DateTime.UtcNow).ToUniversalTime();
        string itemId = Guid.NewGuid().ToString("N");
        LaunchTarget persistedTarget = target with
        {
            Id = string.IsNullOrWhiteSpace(target.Id) ? Guid.NewGuid().ToString("N") : target.Id,
            LibraryItemId = itemId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        return new LibraryItem
        {
            Id = itemId,
            Title = title.Trim(),
            SortTitle = title.Trim(),
            ContentType = contentType,
            AddedAtUtc = now,
            UpdatedAtUtc = now,
            DefaultLaunchTargetId = persistedTarget.Id,
            LaunchTargets = [persistedTarget],
        };
    }
}
