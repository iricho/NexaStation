namespace NexaStation.Profiles.Sessions;

public sealed class InMemoryFrontendProfileStore : IFrontendProfileStore
{
    private readonly FrontendProfile _owner = new("owner", "Owner", FrontendProfileType.Owner, "owner", null, null, true, false, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    private FrontendProfile? _guest;
    private GuestModeSettings _settings = new();
    public Task<FrontendProfile> GetOwnerAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_owner); }
    public Task<FrontendProfile?> GetGuestAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_guest); }
    public Task<GuestModeSettings> GetGuestSettingsAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(_settings); }
    public Task SaveGuestAsync(FrontendProfile? profile, GuestModeSettings settings, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _guest = profile; _settings = settings ?? throw new ArgumentNullException(nameof(settings)); return Task.CompletedTask; }
}

public sealed class InMemoryGuestActivityStore : IGuestActivityStore
{
    public int ClearCount { get; private set; }
    public Task ClearTemporaryAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); ClearCount++; return Task.CompletedTask; }
}
