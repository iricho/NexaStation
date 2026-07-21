using NexaStation.Core;
using NexaStation.Display.Calibration;
using NexaStation.Display.Monitors;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Input.Mapping;
using NexaStation.Profiles.Pin;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;
using NexaStation.TextInput.Layouts;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class Phase10PersistenceTests
{
    [Fact] public async Task Phase10MigrationsCreateAllRequiredTables()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); string[] tables = ["InputProfiles", "InputBindings", "ControllerPreferences", "TextInputSettings", "DisplayPreferences", "DisplayCalibrations", "FrontendProfiles", "GuestModeSettings", "ContentRestrictions", "OwnerPinCredential"]; foreach (string table in tables) Assert.Equal(1, await db.Scalar($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}';"));
    }

    [Fact] public async Task InputProfileAndBindingsRoundTrip()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store store = new(db.Factory); DateTimeOffset now = DateTimeOffset.UtcNow; InputProfile profile = new("user.pad", "Living Room Pad", InputDeviceScope.ControllerFamily, "{\"family\":\"playstation\"}", "PlayStation", true, false, now, now); InputBinding[] bindings = DefaultInputBindings.Create(now).Select(binding => binding with { Id = "user." + binding.Id, InputProfileId = profile.Id }).ToArray(); await store.SaveProfileAsync(profile, bindings); Assert.Equal(profile, (await store.GetProfilesAsync()).Single(value => value.Id == profile.Id)); Assert.Equal(bindings.Length, (await store.GetBindingsAsync(profile.Id)).Count);
    }

    [Fact] public async Task ControllerPreferencePersistsWithoutSerialData()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store store = new(db.Factory); IControllerPreferenceStore preferences = store; ControllerPreference expected = new("family:playstation", "PlayStation", 0, .68, 420, 140, true, true, false, DateTimeOffset.UtcNow); await preferences.SaveAsync(expected); ControllerPreference actual = (await preferences.GetAsync(expected.DeviceMatchKey))!; Assert.Equal(expected.DeviceMatchKey, actual.DeviceMatchKey); Assert.Equal(expected.Deadzone, actual.Deadzone); Assert.DoesNotContain("serial", actual.DeviceMatchKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task TextAndDisplayPreferencesRoundTrip()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store store = new(db.Factory); ITextInputSettingsStore text = store; await text.SaveAsync(new("ar", true, false, 6, DateTimeOffset.UtcNow)); Assert.Equal("ar", (await text.GetAsync()).PreferredLayoutId); IDisplayPreferenceStore display = store; await display.SaveAsync(new(PreferredDisplayMode.SpecificDisplay, "DISPLAY-A", true, FullscreenMode.BorderlessFullscreen, 1.25, true)); DisplayPreferences result = await display.GetAsync(); Assert.Equal("DISPLAY-A", result.PreferredDisplayId); Assert.Equal(1.25, result.InterfaceScale);
    }

    [Fact] public async Task SafeAreaPersistsPerDisplay()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store calibrations = new(db.Factory); DisplayCalibration expected = new("DISPLAY-A", new(20, 10, 20, 10), 1920, 1080, 1, .35, DateTimeOffset.UtcNow); await calibrations.SaveAsync(expected); Assert.Equal(expected.SafeArea, (await calibrations.GetAsync("DISPLAY-A"))!.SafeArea); Assert.Null(await calibrations.GetAsync("DISPLAY-B"));
    }

    [Fact] public async Task GuestProfileSettingsPinAndRestrictionRoundTrip()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store store = new(db.Factory); DateTimeOffset now = DateTimeOffset.UtcNow; FrontendProfile guest = new("guest", "Family Guest", FrontendProfileType.Guest, "guest", "theme", "compact-handheld", false, true, true, now, now); await store.SaveGuestAsync(guest, new() { Enabled = true, RequireOwnerPinToLeave = true, RestrictedCollectionIds = ["private"] }); Assert.Equal("Family Guest", (await store.GetGuestAsync())!.Name); Assert.True((await store.GetGuestSettingsAsync()).RequireOwnerPinToLeave); IPinCredentialStore pins = store; PinCredential credential = new([1, 2, 3], [4, 5, 6], 210_000, now); await pins.SaveAsync(credential); Assert.Equal(credential.Hash, (await pins.GetAsync())!.Hash); await store.SetAsync("secret", RestrictedContentKind.LibraryItem, ContentRestrictionState.OwnerOnly); Assert.Equal(ContentRestrictionState.OwnerOnly, await store.GetAsync("secret", RestrictedContentKind.LibraryItem));
    }

    [Fact] public async Task ResetDeletesOnlySelectedUserInputProfile()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create(); SqlitePhase10Store store = new(db.Factory); DateTimeOffset now = DateTimeOffset.UtcNow; InputProfile one = new("one", "One", InputDeviceScope.Global, null, null, true, false, now, now); InputProfile two = one with { Id = "two", Name = "Two" }; await store.SaveProfileAsync(one, []); await store.SaveProfileAsync(two, []); await store.ResetAsync("one"); IReadOnlyList<InputProfile> values = await store.GetProfilesAsync(); Assert.DoesNotContain(values, profile => profile.Id == "one"); Assert.Contains(values, profile => profile.Id == "two"); Assert.Contains(values, profile => profile.Id == DefaultInputBindings.BuiltInProfileId);
    }
}
