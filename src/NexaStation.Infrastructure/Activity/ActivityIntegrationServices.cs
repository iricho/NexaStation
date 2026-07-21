using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;

namespace NexaStation.Infrastructure.Activity;

public sealed class ProfileActivityPrivacyService(IGuestModeService guestMode,IContentRestrictionStore restrictions) : IActivityPrivacyService
{
    public async Task<ActivityPrivacyLevel> EvaluateAsync(LibraryItem item,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(item);if(item.Hidden)return ActivityPrivacyLevel.Private;if(!guestMode.IsGuest)return ActivityPrivacyLevel.Normal;ContentRestrictionState state=await restrictions.GetAsync(item.Id,RestrictedContentKind.LibraryItem,cancellationToken).ConfigureAwait(false);return state==ContentRestrictionState.Allowed?ActivityPrivacyLevel.GuestTemporary:ActivityPrivacyLevel.Hidden;
    }
}

public sealed class SettingsPresenceCoordinator(IPresenceProvider presenceProvider,ISettingsService settings,IGuestModeService guestMode) : IPresenceCoordinator
{
    public async Task SetForLaunchAsync(LibraryItem item,ActivityPrivacyLevel privacy,string? provider,DateTime startedAtUtc,CancellationToken cancellationToken=default)
    {
        bool enabled=await settings.GetAsync("Presence.Discord.Enabled",false,cancellationToken).ConfigureAwait(false);bool games=await settings.GetAsync("Presence.Discord.GamePresence",false,cancellationToken).ConfigureAwait(false);if(!enabled||!games||guestMode.IsGuest||privacy is ActivityPrivacyLevel.Private or ActivityPrivacyLevel.Hidden or ActivityPrivacyLevel.GuestTemporary){await presenceProvider.ClearAsync(cancellationToken).ConfigureAwait(false);return;}
        bool includeProvider=await settings.GetAsync("Presence.Discord.IncludeProvider",false,cancellationToken).ConfigureAwait(false);bool includePlatform=await settings.GetAsync("Presence.Discord.IncludePlatform",false,cancellationToken).ConfigureAwait(false);bool includeTime=await settings.GetAsync("Presence.Discord.IncludeElapsedTime",false,cancellationToken).ConfigureAwait(false);await presenceProvider.SetAsync(new(item.Title,"Playing through NexaStation",includePlatform?item.LaunchTarget?.EmulatedPlatformId:null,includeProvider?provider:null,includeTime?startedAtUtc:null),cancellationToken).ConfigureAwait(false);
    }
    public Task ClearAsync(CancellationToken cancellationToken=default)=>presenceProvider.ClearAsync(cancellationToken);
}

public sealed class UnavailableDiscordPresenceProvider : IPresenceProvider
{
    public string Id=>"discord";
    public Task<PresenceAvailability> GetAvailabilityAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(new PresenceAvailability(PresenceAvailabilityState.Unavailable,"Discord Rich Presence requires an approved Discord application registration and Social SDK redistribution review."));}
    public Task<PresenceResult> SetAsync(PresencePayload payload,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(new PresenceResult(false,"Discord presence is not configured in this build."));}
    public Task ClearAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.CompletedTask;}
}
