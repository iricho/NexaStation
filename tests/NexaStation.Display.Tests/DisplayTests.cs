using NexaStation.Display.Calibration;
using NexaStation.Display.Handheld;
using NexaStation.Display.Monitors;
using NexaStation.Display.Scaling;
using NexaStation.Display.Television;
using Xunit;

namespace NexaStation.Display.Tests;

public sealed class DisplayTests
{
    [Fact] public async Task DisplaysEnumerateAndPreferredPersists()
    {
        Adapter adapter = new([Display("one", true), Display("two")]); InMemoryDisplayPreferenceStore store = new(); DisplaySelectionService service = new(adapter, store); Assert.Equal(2, (await service.GetDisplaysAsync()).Count); DisplaySelectionResult result = await service.SelectAsync("two"); Assert.True(result.Succeeded); Assert.Equal("two", (await service.GetPreferredAsync())!.Id); Assert.Equal("two", adapter.MovedTo);
    }
    [Fact] public async Task MissingPreferredDisplayFallsBackWithoutDiscardingPreference()
    {
        Adapter adapter = new([Display("primary", true)]); InMemoryDisplayPreferenceStore store = new(); await store.SaveAsync(new(PreferredDisplayMode.SpecificDisplay, "missing", true)); DisplaySelectionService service = new(adapter, store); DisplaySelectionResult result = await service.SelectAsync("missing"); Assert.True(result.Succeeded); Assert.True(result.UsedFallback); Assert.Equal("missing", (await store.GetAsync()).PreferredDisplayId);
    }
    [Fact] public async Task NoDisplayFailsSafely() { DisplaySelectionResult result = await new DisplaySelectionService(new Adapter([]), new InMemoryDisplayPreferenceStore()).SelectAsync("missing"); Assert.False(result.Succeeded); Assert.Null(result.Display); }

    [Fact] public async Task SafeAreaIsBoundedAndPersistedPerDisplay()
    {
        InMemoryDisplayCalibrationStore store = new(); SafeAreaCalibrationService service = new(store); DisplayCalibration value = await service.SaveAsync("tv", new(999, 999, 999, 999), 1920, 1080, 1, .3); Assert.Equal(new SafeAreaInsets(288, 162, 288, 162), value.SafeArea); Assert.Equal(value, await store.GetAsync("tv"));
    }
    [Fact] public async Task SafeAreaScalesWithResolution() { SafeAreaCalibrationService service = new(new InMemoryDisplayCalibrationStore()); DisplayCalibration value = await service.SaveAsync("tv", new(20, 10, 20, 10), 1920, 1080, 1, .3); Assert.Equal(new SafeAreaInsets(40, 20, 40, 20), SafeAreaCalibrationService.ScaleForResolution(value, 3840, 2160)); }
    [Fact] public async Task CalibrationNeverChangesWindowsSettings() { Assert.All(DisplayGuidance.Screens, screen => Assert.False(screen.ChangesWindowsSettings)); await Task.CompletedTask; }

    [Theory][InlineData(.8, true)][InlineData(1, true)][InlineData(1.5, true)][InlineData(.7, false)][InlineData(1.6, false)] public void InterfaceScaleUsesSafeBounds(double scale, bool valid) => Assert.Equal(valid, InterfaceScalePolicy.Validate(scale, 1920, 1080).Valid);
    [Fact] public void ScaleRejectsUnreachableLayout() => Assert.False(InterfaceScalePolicy.Validate(1.5, 800, 600).Valid);

    [Fact] public void BatteryReductionIsOptionalOnDesktop() { EffectiveBatteryVisualState result = BatteryVisualPolicy.Resolve(new(false, VisualQualityPreset.Quality, false), Status(50, PowerSourceKind.Battery)); Assert.Equal(VisualQualityPreset.Quality, result.Preset); Assert.True(result.AnimationsEnabled); }
    [Fact] public void CompactHandheldReducesEffectsOnBattery() { EffectiveBatteryVisualState result = BatteryVisualPolicy.Resolve(new(false, VisualQualityPreset.Balanced, true), Status(50, PowerSourceKind.Battery)); Assert.Equal(VisualQualityPreset.Efficient, result.Preset); Assert.False(result.BlurEnabled); Assert.False(result.GalleryPreloadEnabled); }
    [Fact] public void AcPowerRestoresSelectedPreset() { EffectiveBatteryVisualState result = BatteryVisualPolicy.Resolve(new(true, VisualQualityPreset.Quality, true), Status(100, PowerSourceKind.Ac)); Assert.Equal(VisualQualityPreset.Quality, result.Preset); Assert.True(result.AnimationsEnabled); }
    [Fact] public void LowBatteryUsesBoundedPollingAndPausesNonessentialRefresh() { EffectiveBatteryVisualState result = BatteryVisualPolicy.Resolve(new(true, VisualQualityPreset.Balanced, false), Status(10, PowerSourceKind.Battery)); Assert.Equal(TimeSpan.FromMinutes(5), result.PollInterval); Assert.True(result.PauseProviderRefresh); }

    private static DisplayDescriptor Display(string id, bool primary = false) => new(id, id, new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), 1, (1920, 1080), primary, primary, true);
    private static HandheldPowerStatus Status(int? percent, PowerSourceKind source) => new(percent, source == PowerSourceKind.Ac ? BatteryChargeState.Charging : BatteryChargeState.Discharging, source, null, DateTimeOffset.UtcNow);
    private sealed class Adapter(IReadOnlyList<DisplayDescriptor> displays) : IDisplayPlatformAdapter
    {
        public string? MovedTo { get; private set; }
        public Task<IReadOnlyList<DisplayDescriptor>> EnumerateAsync(CancellationToken cancellationToken = default) => Task.FromResult(displays);
        public Task<bool> MoveCurrentWindowAsync(DisplayDescriptor display, CancellationToken cancellationToken = default) { MovedTo = display.Id; return Task.FromResult(true); }
    }
}
