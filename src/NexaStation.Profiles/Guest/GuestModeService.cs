using NexaStation.Profiles.Sessions;

namespace NexaStation.Profiles.Guest;

public sealed class GuestModeService : IGuestModeService
{
    private readonly IFrontendProfileStore _profiles;
    private readonly IGuestActivityStore _activity;
    private GuestModeSettings _settings = new();

    public GuestModeService(IFrontendProfileStore profiles, IGuestActivityStore activity)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        ActiveProfile = profiles.GetOwnerAsync().GetAwaiter().GetResult();
    }

    public FrontendProfile ActiveProfile { get; private set; }
    public bool IsGuest => ActiveProfile.ProfileType == FrontendProfileType.Guest;
    public event EventHandler<FrontendProfileChangedEventArgs>? ProfileChanged;

    public async Task<bool> EnterAsync(CancellationToken cancellationToken = default)
    {
        _settings = await _profiles.GetGuestSettingsAsync(cancellationToken).ConfigureAwait(false);
        FrontendProfile? guest = await _profiles.GetGuestAsync(cancellationToken).ConfigureAwait(false);
        if (!_settings.Enabled || guest is not { Enabled: true }) return false;
        ActiveProfile = guest; ProfileChanged?.Invoke(this, new(guest)); return true;
    }

    public async Task<bool> ExitAsync(bool ownerPinVerified, CancellationToken cancellationToken = default)
    {
        if (!IsGuest) return true;
        if (_settings.RequireOwnerPinToLeave && !ownerPinVerified) return false;
        if (_settings.ClearActivityOnExit) await _activity.ClearTemporaryAsync(cancellationToken).ConfigureAwait(false);
        ActiveProfile = await _profiles.GetOwnerAsync(cancellationToken).ConfigureAwait(false);
        ProfileChanged?.Invoke(this, new(ActiveProfile)); return true;
    }
}
