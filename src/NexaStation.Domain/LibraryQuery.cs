namespace NexaStation.Domain;

public sealed record LibraryQuery(
    string? SearchText = null,
    ContentType? ContentType = null,
    bool FavoriteOnly = false,
    bool InstalledOnly = false,
    bool IncludeHidden = false,
    LibrarySortMode SortMode = LibrarySortMode.Title,
    int MaximumResults = 200);

public enum LibrarySortMode { Title, RecentlyAdded, RecentlyLaunched, MostPlayed }
