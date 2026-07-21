using NexaStation.Domain;

namespace NexaStation.Core;

public enum ContentCardPresentationType
{
    GameWide,
    GameCover,
    ApplicationSquare,
    ApplicationWide,
    GalleryThumbnail,
    VideoThumbnail,
    StreamingService,
    CollectionMosaic,
    WebApplication,
    CompactList,
    ViewAll,
    MissingContent,
}

public enum ContentCardTargetType { Game, Application, GalleryItem, Video, StreamingService, Collection, WebApplication, ViewAll, MissingContent }
public enum ApplicationIconSourceType { UserSelected, PackagedApplication, ShellLink, Executable, ProgressiveWebApplication, DiscoverCatalogue, AssociatedShell, GeneratedInitial }

public sealed record ContentCardPresentationRequest(
    ContentType ContentType,
    NavigationSection CurrentPage,
    string? ProviderId = null,
    string? DashboardRow = null,
    string? SelectedCardStyle = null,
    double ViewportWidth = 1920,
    bool HasArtwork = false,
    bool HasApplicationIcon = false,
    bool IsGalleryItem = false,
    bool IsVideo = false,
    bool IsStreamingService = false,
    bool IsCollection = false,
    bool IsWebApplication = false,
    bool IsAvailable = true,
    ContentCardPresentationType? UserOverride = null);

public sealed record ContentCardPresentation(
    ContentCardPresentationType PresentationType,
    double Width,
    double Height,
    bool UsesArtwork,
    bool UsesApplicationIcon,
    bool Wraps,
    string FallbackKind);

public interface IContentCardPresentationResolver
{
    ContentCardPresentation Resolve(ContentCardPresentationRequest request);
}

public sealed record ApplicationVisualRequest(
    string Identity,
    string DisplayName,
    string? UserSelectedIconPath = null,
    string? PackagedIconPath = null,
    string? ShortcutPath = null,
    string? ExecutablePath = null,
    string? IconLocation = null,
    string? AppUserModelId = null,
    string? PwaIconPath = null,
    string? CatalogueIconPath = null,
    string? ShellParsingPath = null,
    string? Category = null,
    int DesiredSize = 256,
    bool HighContrast = false,
    string? UserBackgroundColor = null);

public sealed record ApplicationVisualResult(
    string? IconPath,
    ApplicationIconSourceType IconSourceType,
    int? SourceWidth,
    int? SourceHeight,
    bool HasTransparency,
    string SuggestedBackgroundColor,
    string SuggestedForegroundColor,
    bool IsFallback,
    string? FailureReason,
    string FallbackInitials);

public interface IApplicationVisualResolver
{
    Task<ApplicationVisualResult> ResolveAsync(ApplicationVisualRequest request, CancellationToken cancellationToken = default);
}

public sealed record ContentCardViewModel(
    string TargetId,
    ContentCardTargetType TargetType,
    string Title,
    string? Subtitle,
    string? Badge,
    ContentCardPresentationType PresentationType,
    string? PrimaryVisual,
    string? SecondaryVisual,
    string BackgroundColor,
    string ForegroundColor,
    bool IsInstalled,
    bool IsAvailable,
    string PrimaryAction,
    string? SecondaryAction,
    string AccessibilityName);

public sealed record CollectionMosaicRequest(string CollectionId, IReadOnlyList<string> VisualPaths, DateTime ContentsUpdatedAtUtc, int Size = 384);
public sealed record CollectionMosaicResult(string? CachePath, int SourceCount, bool IsFallback, string CacheKey);
public interface ICollectionMosaicService
{
    Task<CollectionMosaicResult> ResolveAsync(CollectionMosaicRequest request, CancellationToken cancellationToken = default);
}

public static class ContentCardInitials
{
    public static string Create(string? title)
    {
        if(string.IsNullOrWhiteSpace(title))return "?";string[] words=title.Split([' ','-','_'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);string[] ignored=["the","and","for","of"];string[] meaningful=words.Where(word=>!ignored.Contains(word,StringComparer.OrdinalIgnoreCase)).ToArray();if(meaningful.Length==0)meaningful=words;
        if(meaningful.Length>1)return string.Concat(meaningful.Take(2).Select(word=>char.ToUpperInvariant(word[0])));
        string value=meaningful[0];string capitals=new(value.Where(char.IsUpper).Take(2).ToArray());return capitals.Length>=2?capitals:value[..Math.Min(2,value.Length)].ToUpperInvariant();
    }
}
