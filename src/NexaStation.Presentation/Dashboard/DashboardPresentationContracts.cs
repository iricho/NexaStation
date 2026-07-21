using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

public enum ArtworkRequestKind
{
    Card,
    ApplicationIcon,
    Hero,
    Background,
}

public sealed record ArtworkPresentation(
    string? LocalPath,
    bool Available,
    bool IsWide,
    int DecodePixelWidth,
    int DecodePixelHeight,
    string? FallbackReason = null);

public interface IArtworkPresentationService
{
    ArtworkPresentation Resolve(LibraryItem item, ArtworkRequestKind requestKind, int targetWidth, int targetHeight);
}

public sealed record DashboardSemanticContent(
    IReadOnlyList<LibraryItem> ContinuePlaying,
    IReadOnlyList<LibraryItem> RecentlyUsedApplications,
    IReadOnlyList<LibraryItem> RecentlyAddedGames,
    IReadOnlyList<LibraryItem> RecentlyAddedApplications,
    IReadOnlyList<LibraryItem> Favorites,
    IReadOnlyList<LibraryItem> MediaServices,
    IReadOnlyList<LibraryItem> RecentlyAdded);

public interface IDashboardSemanticService
{
    DashboardSemanticContent Build(IReadOnlyList<LibraryItem> items);
}

public sealed record HeroSelectionRequest(
    IReadOnlyList<LibraryItem> Items,
    IReadOnlyList<string> PinnedItemIds,
    string? UserFeaturedItemId = null);

public sealed record HeroSelectionResult(
    LibraryItem? Item,
    string Rule,
    ArtworkPresentation Artwork,
    bool IsFallback);

public interface IHeroSelectionService
{
    HeroSelectionResult SelectHero(HeroSelectionRequest request);
    IReadOnlyList<HeroSelectionResult> SelectHeroes(HeroSelectionRequest request, int maximum = 5);
}

public enum HomeDashboardDensity
{
    Compact,
    Standard,
    Large,
}

public sealed record HomeDashboardLayout(
    HomeDashboardDensity Density,
    bool UsesVerticalScrolling,
    double HeaderHeight,
    double FooterHeight,
    double OuterMargin,
    double HeroHeight,
    double GameCardWidth,
    double GameCardHeight,
    double SquareTileSize,
    double PhotoCardWidth,
    double PhotoCardHeight,
    double ActivityRowWidth,
    double ActivityRowHeight,
    double EndpointWidth,
    int GameLimit,
    int ApplicationLimit,
    int PhotoLimit,
    int MediaLimit,
    int FavoriteLimit,
    int RecentActivityLimit);

public interface IHomeDashboardDensityPolicy
{
    HomeDashboardLayout Resolve(double viewportWidth, double viewportHeight);
}
