using Microsoft.Extensions.DependencyInjection;
using NexaStation.Profiles.Guest;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;

namespace NexaStation.Profiles.DependencyInjection;

public static class ProfileServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendProfiles(this IServiceCollection services) => services
        .AddSingleton<IFrontendProfileStore, InMemoryFrontendProfileStore>()
        .AddSingleton<IGuestActivityStore, InMemoryGuestActivityStore>()
        .AddSingleton<IGuestModeService, GuestModeService>()
        .AddSingleton<IContentRestrictionPolicy, ContentRestrictionPolicy>()
        .AddSingleton<IPinCredentialStore, InMemoryPinCredentialStore>()
        .AddSingleton<IOwnerPinService, OwnerPinService>();
}
