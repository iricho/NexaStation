using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Presentation.CardPresentation;

public sealed class ContentCardPresentationResolver : IContentCardPresentationResolver
{
    public ContentCardPresentation Resolve(ContentCardPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ContentCardPresentationType type = request.UserOverride ?? ResolveType(request);
        bool compact = request.ViewportWidth < 1280;
        return type switch
        {
            ContentCardPresentationType.GameCover => new(type, compact ? 132 : 174, compact ? 198 : 260, true, false, true, "game"),
            ContentCardPresentationType.GameWide => new(type, compact ? 224 : 300, compact ? 126 : 169, true, false, true, "game"),
            ContentCardPresentationType.ApplicationSquare => new(type, compact ? 136 : 172, compact ? 136 : 172, false, true, true, "application"),
            ContentCardPresentationType.ApplicationWide => new(type, compact ? 224 : 300, compact ? 126 : 169, false, true, true, "application"),
            ContentCardPresentationType.StreamingService => new(type, compact ? 142 : 176, compact ? 142 : 176, false, true, true, "streaming"),
            ContentCardPresentationType.GalleryThumbnail or ContentCardPresentationType.VideoThumbnail => new(type, compact ? 220 : 286, compact ? 124 : 161, true, false, true, type == ContentCardPresentationType.VideoThumbnail ? "video" : "gallery"),
            ContentCardPresentationType.CollectionMosaic => new(type, compact ? 220 : 286, compact ? 150 : 190, true, false, true, "collection"),
            ContentCardPresentationType.WebApplication => new(type, compact ? 136 : 172, compact ? 136 : 172, false, true, true, "web-application"),
            ContentCardPresentationType.CompactList => new(type, compact ? 300 : 420, 82, false, false, true, "content"),
            ContentCardPresentationType.ViewAll => new(type, compact ? 136 : 172, compact ? 136 : 172, false, false, true, "view-all"),
            _ => new(ContentCardPresentationType.MissingContent, compact ? 136 : 172, compact ? 136 : 172, false, false, true, "missing"),
        };
    }

    private static ContentCardPresentationType ResolveType(ContentCardPresentationRequest request)
    {
        if (!request.IsAvailable) return ContentCardPresentationType.MissingContent;
        if (request.IsCollection) return ContentCardPresentationType.CollectionMosaic;
        if (request.IsGalleryItem) return request.IsVideo ? ContentCardPresentationType.VideoThumbnail : ContentCardPresentationType.GalleryThumbnail;
        if (request.IsStreamingService) return ContentCardPresentationType.StreamingService;
        if (request.IsWebApplication) return ContentCardPresentationType.WebApplication;
        if (string.Equals(request.SelectedCardStyle, "CompactList", StringComparison.OrdinalIgnoreCase) || string.Equals(request.SelectedCardStyle, "List", StringComparison.OrdinalIgnoreCase)) return ContentCardPresentationType.CompactList;
        if (request.ContentType == ContentType.Game)
            return string.Equals(request.SelectedCardStyle, "GameCover", StringComparison.OrdinalIgnoreCase) || string.Equals(request.SelectedCardStyle, "VerticalCover", StringComparison.OrdinalIgnoreCase)
                ? ContentCardPresentationType.GameCover
                : ContentCardPresentationType.GameWide;
        if (request.ContentType is ContentType.Application or ContentType.Media)
            return string.Equals(request.SelectedCardStyle, "ApplicationWide", StringComparison.OrdinalIgnoreCase) || string.Equals(request.SelectedCardStyle, "Wide", StringComparison.OrdinalIgnoreCase)
                ? ContentCardPresentationType.ApplicationWide
                : ContentCardPresentationType.ApplicationSquare;
        return ContentCardPresentationType.MissingContent;
    }
}
