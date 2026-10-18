using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

/// <summary>Projects a loaded library without changing the dashboard's source data.</summary>
public sealed class LibraryBrowseState
{
    public string SearchText { get; set; } = string.Empty;
    public string View { get; set; } = "All";
    public string Platform { get; set; } = "All Platforms";
    public string Genre { get; set; } = "All Genres";
    public LibrarySortMode SortMode { get; set; } = LibrarySortMode.Title;
    public bool HasFilters => !string.IsNullOrWhiteSpace(SearchText) || View != "All" || Platform != "All Platforms" || Genre != "All Genres";

    public IReadOnlyList<LibraryItem> Apply(IReadOnlyList<LibraryItem> items)
    {
        IEnumerable<LibraryItem> selected = items.Where(item => View == "Hidden" ? item.Hidden : !item.Hidden);
        selected = View switch
        {
            "Games" => selected.Where(item => item.ContentType == ContentType.Game),
            "Applications" => selected.Where(item => item.ContentType == ContentType.Application),
            "Favorites" => selected.Where(item => item.Favorite),
            "Recently Used" => selected.Where(item => item.LastLaunchedAtUtc is not null),
            "Native" => selected.Where(item => !IsEmulated(item)),
            "Emulated" => selected.Where(IsEmulated),
            "Valid targets" => selected.Where(item => LaunchTargetValidator.Validate(item.LaunchTarget) is null),
            "Missing targets" => selected.Where(item => LaunchTargetValidator.Validate(item.LaunchTarget) is not null),
            _ => selected,
        };

        if (Platform != "All Platforms")
            selected = selected.Where(item => string.Equals(SourceLabel(item), Platform, StringComparison.OrdinalIgnoreCase));
        if (Genre != "All Genres")
            selected = selected.Where(item => string.Equals(item.PrimaryCategoryName, Genre, StringComparison.OrdinalIgnoreCase));

        string query = SearchText.Trim();
        if (query.Length > 0)
            selected = selected.Where(item => Matches(item.Title, query) || Matches(item.Subtitle, query) || Matches(item.Description, query));

        LibrarySortMode effectiveSort = View switch
        {
            "Recently Added" => LibrarySortMode.RecentlyAdded,
            "Recently Used" => LibrarySortMode.RecentlyLaunched,
            _ => SortMode,
        };
        IOrderedEnumerable<LibraryItem> ordered = effectiveSort switch
        {
            LibrarySortMode.RecentlyAdded => selected.OrderByDescending(item => item.AddedAtUtc),
            LibrarySortMode.RecentlyLaunched => selected.OrderByDescending(item => item.LastLaunchedAtUtc),
            LibrarySortMode.MostPlayed => selected.OrderByDescending(item => item.TotalPlaytimeSeconds),
            _ => selected.OrderBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase),
        };
        return ordered.ThenBy(item => item.SortTitle, StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    public void Reset()
    {
        SearchText = string.Empty;
        View = "All";
        Platform = "All Platforms";
        Genre = "All Genres";
        SortMode = LibrarySortMode.Title;
    }

    private static bool IsEmulated(LibraryItem item) => item.LaunchTargets.Any(target => string.Equals(target.ProviderId, "emulation", StringComparison.OrdinalIgnoreCase));
    public static string SourceLabel(LibraryItem item) => item.LaunchTarget?.ProviderId is { Length: > 0 } source ? source : "Local";
    private static bool Matches(string? value, string query) => value?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
}
