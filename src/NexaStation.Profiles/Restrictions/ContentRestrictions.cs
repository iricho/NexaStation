namespace NexaStation.Profiles.Restrictions;

public enum ContentRestrictionState { Allowed, HiddenInGuest, PinRequired, OwnerOnly }
public enum RestrictedContentKind { LibraryItem, Collection, GallerySource, GalleryAlbum, Application, MediaRow, SettingsDestination }

public sealed record RestrictedContentDescriptor(
    string Id,
    RestrictedContentKind Kind,
    ContentRestrictionState Restriction,
    string? Title = null,
    string? ArtworkPath = null,
    string? FileName = null,
    bool GuestAllowed = false);

public sealed record RestrictedContentDecision(bool Visible, bool RequiresPin, string? DisplayTitle, string? ArtworkPath, string? FileName, string Reason);

public interface IContentRestrictionPolicy
{
    RestrictedContentDecision Evaluate(RestrictedContentDescriptor content, bool isGuest, bool ownerPinVerified = false, bool showNeutralPlaceholder = false);
}

public interface IContentRestrictionStore
{
    Task<ContentRestrictionState> GetAsync(string targetId, RestrictedContentKind kind, CancellationToken cancellationToken = default);
    Task SetAsync(string targetId, RestrictedContentKind kind, ContentRestrictionState state, CancellationToken cancellationToken = default);
}

public sealed class ContentRestrictionPolicy : IContentRestrictionPolicy
{
    public RestrictedContentDecision Evaluate(RestrictedContentDescriptor content, bool isGuest, bool ownerPinVerified = false, bool showNeutralPlaceholder = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        bool permitted = content.Restriction switch
        {
            ContentRestrictionState.Allowed => true,
            ContentRestrictionState.HiddenInGuest => !isGuest || content.GuestAllowed,
            ContentRestrictionState.PinRequired => !isGuest && ownerPinVerified,
            ContentRestrictionState.OwnerOnly => !isGuest,
            _ => false,
        };
        if (permitted) return new(true, false, content.Title, content.ArtworkPath, content.FileName, "Allowed by the active frontend profile.");
        bool requiresPin = !isGuest && content.Restriction == ContentRestrictionState.PinRequired;
        if (!showNeutralPlaceholder) return new(false, requiresPin, null, null, null, requiresPin ? "Owner PIN required." : "Hidden by Guest Mode.");
        return new(true, requiresPin, "Restricted item", null, null, requiresPin ? "Owner PIN required." : "Restricted content is hidden in Guest Mode.");
    }
}
