using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

public sealed class HeroSelectionService(IArtworkPresentationService artwork) : IHeroSelectionService
{
    private static readonly ArtworkPresentation FallbackArtwork = new(null, false, false, 1920, 1080, "NexaStation fallback presentation.");

    public HeroSelectionResult SelectHero(HeroSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        LibraryItem[] eligible = request.Items.Where(Eligible).ToArray();

        HeroSelectionResult? user = SelectById(eligible, request.UserFeaturedItemId, "UserFeatured");
        if (user is not null) return user;

        foreach (string pinnedId in request.PinnedItemIds)
        {
            HeroSelectionResult? pinned = SelectById(eligible, pinnedId, "Pinned");
            if (pinned is not null) return pinned;
        }

        LibraryItem? selected = eligible
            .Where(item => item.ContentType == ContentType.Game && item.Favorite && item.LastLaunchedAtUtc is not null && HasWideArtwork(item))
            .OrderByDescending(item => item.LastLaunchedAtUtc)
            .FirstOrDefault();
        if (selected is not null) return Result(selected, "RecentFavoriteGame");

        selected = eligible
            .Where(item => item.ContentType == ContentType.Game && item.LastLaunchedAtUtc is not null && HasWideArtwork(item))
            .OrderByDescending(item => item.LastLaunchedAtUtc)
            .FirstOrDefault();
        if (selected is not null) return Result(selected, "RecentGame");

        selected = eligible
            .Where(item => item.ContentType == ContentType.Game && HasWideArtwork(item))
            .OrderByDescending(item => item.AddedAtUtc)
            .FirstOrDefault();
        if (selected is not null) return Result(selected, "RecentlyAddedGame");

        return new(null, "NexaStationFallback", FallbackArtwork, true);
    }

    public IReadOnlyList<HeroSelectionResult> SelectHeroes(HeroSelectionRequest request, int maximum = 5)
    {
        ArgumentNullException.ThrowIfNull(request);
        maximum = Math.Clamp(maximum, 1, 5);
        HeroSelectionResult primary = SelectHero(request);
        if (primary.Item is null) return [primary];

        LibraryItem[] eligible = request.Items.Where(Eligible).Where(HasWideArtwork).ToArray();
        Dictionary<string, int> pinOrder = request.PinnedItemIds.Distinct(StringComparer.Ordinal).Select((id, index) => (id, index)).ToDictionary(value => value.id, value => value.index, StringComparer.Ordinal);
        IEnumerable<LibraryItem> ordered = eligible
            .OrderBy(item => string.Equals(item.Id, request.UserFeaturedItemId, StringComparison.Ordinal) ? 0 : pinOrder.ContainsKey(item.Id) ? 1 : item.Favorite ? 2 : item.LastLaunchedAtUtc is not null ? 3 : 4)
            .ThenBy(item => pinOrder.TryGetValue(item.Id, out int index) ? index : int.MaxValue)
            .ThenByDescending(item => item.LastLaunchedAtUtc ?? item.AddedAtUtc)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase);

        List<HeroSelectionResult> results = [primary];
        foreach (LibraryItem item in ordered)
        {
            if (results.Any(result => result.Item?.Id == item.Id)) continue;
            results.Add(Result(item, "CarouselCandidate"));
            if (results.Count == maximum) break;
        }
        return results;
    }

    private HeroSelectionResult? SelectById(IEnumerable<LibraryItem> eligible, string? id, string rule)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        LibraryItem? item = eligible.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        return item is not null && HasWideArtwork(item) ? Result(item, rule) : null;
    }

    private HeroSelectionResult Result(LibraryItem item, string rule) =>
        new(item, rule, artwork.Resolve(item, ArtworkRequestKind.Hero, 1920, 560), false);

    private bool HasWideArtwork(LibraryItem item) => artwork.Resolve(item, ArtworkRequestKind.Hero, 1920, 560).Available;

    private static bool Eligible(LibraryItem item) =>
        !item.Hidden
        && item.LaunchTarget is not null
        && LaunchTargetValidator.Validate(item.LaunchTarget) is null;
}
