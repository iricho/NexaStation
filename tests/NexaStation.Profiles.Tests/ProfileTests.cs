using NexaStation.Profiles.Guest;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;
using Xunit;

namespace NexaStation.Profiles.Tests;

public sealed class ProfileTests
{
    [Fact] public async Task GuestModeEntersAndClearsTemporaryActivityOnExit()
    {
        InMemoryFrontendProfileStore store = new(); FrontendProfile guest = Guest(); await store.SaveGuestAsync(guest, new() { Enabled = true, ClearActivityOnExit = true }); InMemoryGuestActivityStore activity = new(); GuestModeService service = new(store, activity); Assert.True(await service.EnterAsync()); Assert.True(service.IsGuest); Assert.True(await service.ExitAsync(false)); Assert.False(service.IsGuest); Assert.Equal(1, activity.ClearCount);
    }
    [Fact] public async Task GuestModeCanRequireVerifiedPinToExit()
    {
        InMemoryFrontendProfileStore store = new(); await store.SaveGuestAsync(Guest(), new() { Enabled = true, RequireOwnerPinToLeave = true }); GuestModeService service = new(store, new InMemoryGuestActivityStore()); Assert.True(await service.EnterAsync()); Assert.False(await service.ExitAsync(false)); Assert.True(await service.ExitAsync(true));
    }
    [Fact] public async Task DisabledGuestModeCannotEnter() { InMemoryFrontendProfileStore store = new(); await store.SaveGuestAsync(Guest(), new() { Enabled = false }); Assert.False(await new GuestModeService(store, new InMemoryGuestActivityStore()).EnterAsync()); }

    [Theory][InlineData(ContentRestrictionState.Allowed, true)][InlineData(ContentRestrictionState.HiddenInGuest, false)][InlineData(ContentRestrictionState.OwnerOnly, false)][InlineData(ContentRestrictionState.PinRequired, false)] public void GuestRestrictionsHideProtectedMetadata(ContentRestrictionState state, bool visible)
    {
        RestrictedContentDecision decision = new ContentRestrictionPolicy().Evaluate(new("id", RestrictedContentKind.LibraryItem, state, "Secret", "secret.jpg", "secret.png"), true); Assert.Equal(visible, decision.Visible); if (!visible) { Assert.Null(decision.DisplayTitle); Assert.Null(decision.ArtworkPath); Assert.Null(decision.FileName); }
    }
    [Fact] public void NeutralPlaceholderLeaksNoMetadata() { RestrictedContentDecision decision = new ContentRestrictionPolicy().Evaluate(new("id", RestrictedContentKind.GalleryAlbum, ContentRestrictionState.HiddenInGuest, "Private Trip", "cover.jpg", "beach.png"), true, showNeutralPlaceholder: true); Assert.True(decision.Visible); Assert.Equal("Restricted item", decision.DisplayTitle); Assert.Null(decision.ArtworkPath); Assert.Null(decision.FileName); }
    [Fact] public void OwnerPinUnlocksPinRequiredContent() { ContentRestrictionPolicy policy = new(); RestrictedContentDescriptor item = new("id", RestrictedContentKind.SettingsDestination, ContentRestrictionState.PinRequired, "Settings"); Assert.False(policy.Evaluate(item, false).Visible); Assert.True(policy.Evaluate(item, false, true).Visible); }

    [Fact] public async Task PinIsSaltedHashedAndVerifies()
    {
        InMemoryPinCredentialStore store = new(); OwnerPinService service = new(store, iterations: 10_000); await service.SetAsync("1234".AsMemory()); PinCredential credential = (await store.GetAsync())!; Assert.NotEqual("1234", Convert.ToBase64String(credential.Hash)); Assert.Equal(16, credential.Salt.Length); Assert.Equal(PinVerificationStatus.Succeeded, (await service.VerifyAsync("1234".AsMemory())).Status); Assert.Equal(PinVerificationStatus.Incorrect, (await service.VerifyAsync("9999".AsMemory())).Status);
    }
    [Theory][InlineData("123")][InlineData("123456789")][InlineData("12a4")] public async Task InvalidPinsAreRejected(string pin) { OwnerPinService service = new(new InMemoryPinCredentialStore(), iterations: 10_000); await Assert.ThrowsAsync<ArgumentException>(() => service.SetAsync(pin.AsMemory())); }
    [Fact] public async Task RepeatedFailuresAreRateLimited()
    {
        InMemoryPinCredentialStore store = new(); OwnerPinService service = new(store, iterations: 10_000); await service.SetAsync("1234".AsMemory()); for (int attempt = 0; attempt < 4; attempt++) Assert.Equal(PinVerificationStatus.Incorrect, (await service.VerifyAsync("9999".AsMemory())).Status); PinVerificationResult result = await service.VerifyAsync("9999".AsMemory()); Assert.Equal(PinVerificationStatus.LockedOut, result.Status); Assert.True(result.RetryAfter > TimeSpan.Zero);
    }
    [Fact] public async Task RecoveryResetRequiresDeliberateConfirmation()
    {
        InMemoryPinCredentialStore store = new(); OwnerPinService service = new(store, iterations: 10_000); await service.SetAsync("1234".AsMemory()); await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResetThroughLocalRecoveryAsync(false)); await service.ResetThroughLocalRecoveryAsync(true); Assert.Equal(PinVerificationStatus.NotConfigured, (await service.VerifyAsync("1234".AsMemory())).Status);
    }

    private static FrontendProfile Guest() => new("guest", "Guest", FrontendProfileType.Guest, "guest", null, null, false, false, true, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}
