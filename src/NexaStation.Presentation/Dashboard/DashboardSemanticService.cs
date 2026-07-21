using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

public sealed class DashboardSemanticService : IDashboardSemanticService
{
    public DashboardSemanticContent Build(IReadOnlyList<LibraryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        LibraryItem[] visible = items.Where(item => !item.Hidden).ToArray();
        return new(
            OrderByActivityThenAdded(visible.Where(item => item.ContentType == ContentType.Game)),
            OrderByActivityThenAdded(visible.Where(item => item.ContentType == ContentType.Application)),
            visible.Where(item => item.ContentType == ContentType.Game)
                .OrderByDescending(item => item.AddedAtUtc).ThenBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            visible.Where(item => item.ContentType == ContentType.Application)
                .OrderByDescending(item => item.AddedAtUtc).ThenBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            visible.Where(item => item.Favorite).ToArray(),
            visible.Where(IsMediaService).OrderByDescending(item => item.LastLaunchedAtUtc ?? item.AddedAtUtc).ToArray(),
            visible.OrderByDescending(item => item.AddedAtUtc).ThenBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }

    private static LibraryItem[] OrderByActivityThenAdded(IEnumerable<LibraryItem> items) => items
        .OrderBy(item => item.LastLaunchedAtUtc is null)
        .ThenByDescending(item => item.LastLaunchedAtUtc)
        .ThenByDescending(item => item.AddedAtUtc)
        .ThenBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    private static bool IsMediaService(LibraryItem item)
    {
        if (item.ContentType == ContentType.Media) return true;
        if (item.ContentType != ContentType.Application) return false;
        string category = item.PrimaryCategoryName ?? string.Empty;
        return category.Contains("media", StringComparison.OrdinalIgnoreCase)
            || category.Contains("stream", StringComparison.OrdinalIgnoreCase)
            || category.Contains("music", StringComparison.OrdinalIgnoreCase)
            || category.Contains("entertainment", StringComparison.OrdinalIgnoreCase);
    }
}
