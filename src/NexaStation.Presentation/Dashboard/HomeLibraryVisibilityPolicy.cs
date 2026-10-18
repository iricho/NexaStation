using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

public static class HomeLibraryVisibilityPolicy
{
    /// <summary>Resolve shelf and pin references through the current profile's authorized snapshot.</summary>
    public static IEnumerable<LibraryItem> Resolve(IEnumerable<LibraryItem> candidates, IReadOnlyDictionary<string, LibraryItem> authorizedItems)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (LibraryItem candidate in candidates)
            if (authorizedItems.TryGetValue(candidate.Id, out LibraryItem? item) && !item.Hidden && seen.Add(item.Id))
                yield return item;
    }
}
