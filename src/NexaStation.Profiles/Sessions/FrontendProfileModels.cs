namespace NexaStation.Profiles.Sessions;

public enum FrontendProfileType { Owner, Guest }
public sealed record FrontendProfile(
    string Id,
    string Name,
    FrontendProfileType ProfileType,
    string? AvatarKey,
    string? ThemeOverrideId,
    string? LayoutOverrideId,
    bool IsDefault,
    bool PinRequired,
    bool Enabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record GuestModeSettings
{
    public bool Enabled { get; init; }
    public string GuestName { get; init; } = "Guest";
    public string? LayoutId { get; init; }
    public string? ThemeId { get; init; }
    public bool AllowGames { get; init; } = true;
    public bool AllowApplications { get; init; } = true;
    public bool AllowGallery { get; init; } = true;
    public bool AllowMedia { get; init; } = true;
    public bool AllowDiscoverApps { get; init; } = true;
    public bool AllowDesktopExit { get; init; } = true;
    public bool AllowQuickMenuPowerActions { get; init; } = true;
    public bool ClearActivityOnExit { get; init; } = true;
    public bool RequireOwnerPinToLeave { get; init; }
    public IReadOnlyList<string> RestrictedCollectionIds { get; init; } = [];
    public IReadOnlyList<string> GuestAllowedCollectionIds { get; init; } = [];
}

public interface IFrontendProfileStore
{
    Task<FrontendProfile> GetOwnerAsync(CancellationToken cancellationToken = default);
    Task<FrontendProfile?> GetGuestAsync(CancellationToken cancellationToken = default);
    Task<GuestModeSettings> GetGuestSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveGuestAsync(FrontendProfile? profile, GuestModeSettings settings, CancellationToken cancellationToken = default);
}

public interface IGuestActivityStore { Task ClearTemporaryAsync(CancellationToken cancellationToken = default); }

public sealed class FrontendProfileChangedEventArgs(FrontendProfile profile) : EventArgs { public FrontendProfile Profile { get; } = profile; }

public interface IGuestModeService
{
    FrontendProfile ActiveProfile { get; }
    bool IsGuest { get; }
    event EventHandler<FrontendProfileChangedEventArgs>? ProfileChanged;
    Task<bool> EnterAsync(CancellationToken cancellationToken = default);
    Task<bool> ExitAsync(bool ownerPinVerified, CancellationToken cancellationToken = default);
}
