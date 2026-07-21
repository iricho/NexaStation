using NexaStation.Core;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.Input.Glyphs;
using NexaStation.Input.Mapping;
using NexaStation.Input.Validation;
using Xunit;

namespace NexaStation.Input.Tests;

public sealed class AdaptiveInputTests
{
    [Fact] public void KeyboardInputSelectsKeyboardMode() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); service.ReportInput(new(InputMode.Keyboard, IsButtonPress: true)); Assert.Equal(InputMode.Keyboard, service.CurrentMode); }
    [Fact] public void MeaningfulMouseMovementSelectsMouseMode() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(InputMode.Mouse, Magnitude: 8)); Assert.Equal(InputMode.Mouse, service.CurrentMode); }
    [Fact] public void TinyAndSyntheticMouseMovementAreIgnored() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(InputMode.Mouse, Magnitude: 1)); service.ReportInput(new(InputMode.Mouse, Magnitude: 20, IsSynthetic: true)); Assert.Equal(InputMode.Keyboard, service.CurrentMode); }
    [Theory][InlineData(InputMode.XboxController)][InlineData(InputMode.PlayStationController)][InlineData(InputMode.NintendoController)][InlineData(InputMode.GenericController)] public void ControllerButtonSelectsFamily(InputMode mode) { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(mode, "pad", IsButtonPress: true)); Assert.Equal(mode, service.CurrentMode); }
    [Fact] public void StickDriftDoesNotSwitchMode() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(InputMode.XboxController, "pad", Magnitude: .2)); Assert.Equal(InputMode.Keyboard, service.CurrentMode); }
    [Fact] public void TouchTakesOverImmediately() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.FromSeconds(10)); service.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); service.ReportInput(new(InputMode.Touch)); Assert.Equal(InputMode.Touch, service.CurrentMode); }
    [Fact] public void DisconnectFallsBackToKeyboard() { ActiveInputModeService service = new(stabilityThreshold: TimeSpan.Zero); service.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); service.ReportDisconnected("pad"); Assert.Equal(InputMode.Keyboard, service.CurrentMode); }

    [Fact] public void FamilyDetectorUsesOverrideFirst() { ControllerFamilyDetector detector = new(); Assert.Equal(ControllerFamily.Nintendo, detector.Detect(new("id", "Xbox compatible", UserOverride: ControllerFamily.Nintendo))); }
    [Theory][InlineData("DualSense Wireless Controller", ControllerFamily.PlayStation)][InlineData("Nintendo Switch Pro Controller", ControllerFamily.Nintendo)][InlineData("Xbox Wireless Controller", ControllerFamily.Xbox)][InlineData("Arcade USB Pad", ControllerFamily.Generic)] public void FamilyDetectorUsesSafeSignals(string name, ControllerFamily expected) => Assert.Equal(expected, new ControllerFamilyDetector().Detect(new("id", name)));
    [Theory][InlineData("054C", "0CE6")][InlineData("0x054c", "0x0ce6")] public void FamilyDetectorUsesDualSenseVendorProductEvidence(string vendor, string product) => Assert.Equal(ControllerFamily.PlayStation, new ControllerFamilyDetector().Detect(new("id", null, vendor, product, "Windows.Gaming.Input")));

    [Fact]
    public async Task ControllerResolverSuppressesDuplicateVirtualXboxRepresentation()
    {
        ControllerDeviceResolver resolver = new(new ControllerFamilyDetector());
        ControllerBackendDevice physical = new("physical", "Windows.Gaming.Input", "DualSense Wireless Controller", "054C", "0CE6", ControllerConnectionType.Bluetooth, false);
        ControllerBackendDevice virtualXbox = new("virtual", "XInput", "Virtual Xbox Controller", "054C", "0CE6", ControllerConnectionType.Virtual, true);
        ControllerResolutionResult result = await resolver.ResolveAsync([physical, virtualXbox]);
        Assert.NotNull(result.ActiveDevice);
        Assert.Equal("physical", result.ActiveDevice.RuntimeId);
        Assert.Single(result.SuppressedDuplicates);
        Assert.Equal("virtual", result.SuppressedDuplicates[0].RuntimeId);
    }

    [Fact]
    public async Task ControllerResolverAllowsPlayStationGlyphsOnVirtualXboxRepresentation()
    {
        ControllerDeviceResolver resolver = new(new ControllerFamilyDetector());
        ControllerBackendDevice virtualXbox = new("virtual", "XInput", "Virtual Xbox Controller", "045E", "028E", ControllerConnectionType.Virtual, true);
        ControllerResolutionResult result = await resolver.ResolveAsync([virtualXbox], new(RepresentationPreference: ControllerRepresentationPreference.VirtualXboxController, UserGlyphSetOverride: "PlayStation"));
        Assert.NotNull(result.ActiveDevice);
        Assert.True(result.ActiveDevice.IsVirtual);
        Assert.Equal("PlayStation", result.ActiveDevice.PreferredGlyphSet);
    }

    [Fact]
    public async Task DisabledControllerRepresentationProducesNoActiveDevice()
    {
        ControllerDeviceResolver resolver = new(new ControllerFamilyDetector());
        ControllerBackendDevice device = new("pad", "Windows.Gaming.Input", "Xbox Wireless Controller", "045E", "028E");
        ControllerResolutionResult initial = await resolver.ResolveAsync([device]);
        Assert.NotNull(initial.ActiveDevice);
        ControllerResolutionResult disabled = await resolver.ResolveAsync([device], new(DisabledRepresentationKeys: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { initial.ActiveDevice.PhysicalIdentityKey }));
        Assert.Null(disabled.ActiveDevice);
        Assert.Empty(disabled.Devices);
    }

    [Fact]
    public void DualSenseBuiltInProfileMapsPlayStationSemanticDefaults()
    {
        RawControllerMappingProfile profile = Assert.Single(BuiltInRawControllerMappingProfiles.All, value => value.Id == BuiltInRawControllerMappingProfiles.DualSenseProfileId);
        Assert.Equal(ControllerFamily.PlayStation, profile.ControllerFamily);
        Assert.Equal(SemanticPhysicalButton.FaceSouth, BuiltInRawControllerMappingProfiles.DefaultSemanticButtons(ControllerFamily.PlayStation)[InputAction.Confirm]);
        Assert.Equal(SemanticPhysicalButton.FaceEast, BuiltInRawControllerMappingProfiles.DefaultSemanticButtons(ControllerFamily.PlayStation)[InputAction.Cancel]);
        Assert.Equal(SemanticPhysicalButton.FaceWest, BuiltInRawControllerMappingProfiles.DefaultSemanticButtons(ControllerFamily.PlayStation)[InputAction.ContextMenu]);
        Assert.DoesNotContain(profile.ButtonMappings, mapping => mapping.Button == SemanticPhysicalButton.Guide && BuiltInRawControllerMappingProfiles.DefaultSemanticButtons(ControllerFamily.PlayStation).Values.Contains(mapping.Button));
    }

    [Fact] public void GlyphsFollowActiveModeAndHaveAccessibleFallbacks()
    {
        ActiveInputModeService mode = new(stabilityThreshold: TimeSpan.Zero); InputBindingService bindings = new(); using InputGlyphService glyphs = new(mode, bindings);
        Assert.Equal("Enter", glyphs.Resolve(InputAction.Confirm, InputGlyphContext.Footer).TextFallback);
        mode.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); Assert.Equal("A", glyphs.Resolve(InputAction.Confirm, InputGlyphContext.Footer).TextFallback);
        mode.ReportInput(new(InputMode.PlayStationController, "pad2", IsButtonPress: true)); InputGlyph cancel = glyphs.Resolve(InputAction.Cancel, InputGlyphContext.Footer); Assert.Equal("Circle", cancel.TextFallback); Assert.Equal("InputGlyph.PlayStation.Circle", cancel.AssetKey); Assert.Contains("Circle button", cancel.AccessibilityLabel, StringComparison.Ordinal);
    }

    [Fact] public void PromptOverrideWinsOverBackend()
    {
        ActiveInputModeService mode = new(stabilityThreshold: TimeSpan.Zero); mode.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); using InputGlyphService glyphs = new(mode, new InputBindingService()); glyphs.Apply(PromptStyle.PlayStation); Assert.Equal("Cross", glyphs.Resolve(InputAction.Confirm, InputGlyphContext.Footer).TextFallback);
    }

    [Theory]
    [InlineData(InputAction.Confirm, "InputGlyph.PlayStation.Cross")]
    [InlineData(InputAction.Cancel, "InputGlyph.PlayStation.Circle")]
    [InlineData(InputAction.ContextMenu, "InputGlyph.PlayStation.Square")]
    [InlineData(InputAction.ToggleDetails, "InputGlyph.PlayStation.Triangle")]
    public void PlayStationFaceActionsResolveSemanticShapeAssets(InputAction action, string expectedAsset)
    {
        ActiveInputModeService mode = new(stabilityThreshold: TimeSpan.Zero);
        using InputGlyphService glyphs = new(mode, new InputBindingService());
        glyphs.Apply(PromptStyle.PlayStation);
        InputGlyph glyph = glyphs.Resolve(action, InputGlyphContext.MediaPlayer);
        Assert.Equal(expectedAsset, glyph.AssetKey);
        Assert.Contains("button", glyph.AccessibilityLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaptureChordUsesControllerGlyphsButRemainsDashboardScoped()
    {
        ActiveInputModeService mode = new(stabilityThreshold: TimeSpan.Zero);
        mode.ReportInput(new(InputMode.PlayStationController, "dualsense", IsButtonPress: true));
        using InputGlyphService glyphs = new(mode, new InputBindingService());
        InputGlyph capture = glyphs.Resolve(InputAction.CaptureScreenshot, InputGlyphContext.Footer);
        Assert.Equal("L1+R1+Square", capture.TextFallback);
        Assert.True(capture.IsChord);
        Assert.Single(DefaultInputBindings.Create(), binding => binding.Action == InputAction.CaptureScreenshot && binding.Context == InputContext.Dashboard);
    }

    [Fact] public void RemappedBindingChangesResolvedGlyph()
    {
        InputBindingService bindings = new(); InputBinding original = bindings.ActiveBindings.Single(binding => binding.Action == InputAction.Confirm && binding.PhysicalInput.Kind == PhysicalInputKind.ControllerButton && binding.Context == InputContext.Global); bindings.Apply(bindings.ActiveBindings.Select(binding => binding == original ? binding with { PhysicalInput = new(PhysicalInputKind.ControllerButton, "FACE_NORTH") } : binding).ToArray()); ActiveInputModeService mode = new(stabilityThreshold: TimeSpan.Zero); mode.ReportInput(new(InputMode.XboxController, "pad", IsButtonPress: true)); using InputGlyphService glyphs = new(mode, bindings); Assert.Equal("Y", glyphs.Resolve(InputAction.Confirm, InputGlyphContext.Footer).TextFallback);
    }

    [Fact] public void MappingConflictsAndMissingSafetyAreRejected()
    {
        InputBinding[] bindings = DefaultInputBindings.Create().Where(binding => binding.Action != InputAction.EmergencyDisable).ToArray(); InputBinding confirm = bindings.First(binding => binding.Action == InputAction.Confirm && binding.Context == InputContext.Global); InputBinding cancel = bindings.First(binding => binding.Action == InputAction.Cancel && binding.Context == InputContext.Global); bindings[Array.IndexOf(bindings, cancel)] = cancel with { PhysicalInput = confirm.PhysicalInput }; InputMappingValidationResult result = new InputMappingValidator().Validate(bindings); Assert.False(result.IsValid); Assert.Contains(result.Conflicts, conflict => conflict.Type == MappingConflictType.ExactConflict); Assert.Contains(result.Conflicts, conflict => conflict.Type == MappingConflictType.ReservedSafetyConflict);
    }

    [Fact] public void EmergencyChordRaisesOnceAfterHold()
    {
        ManualTimeProvider time = new(); EmergencyMappingResetDetector detector = new(time, TimeSpan.FromSeconds(3)); Assert.False(detector.Update(true, true, true, true)); time.Advance(TimeSpan.FromSeconds(3)); Assert.True(detector.Update(true, true, true, true)); Assert.False(detector.Update(true, true, true, true));
    }

    [Fact]
    public void ApplicationKeyIsAContextMenuBinding()
    {
        InputBinding binding=Assert.Single(DefaultInputBindings.Create(),value=>value.Action==InputAction.ContextMenu&&value.PhysicalInput.Kind==PhysicalInputKind.KeyboardKey&&value.PhysicalInput.Code=="APPLICATION");
        Assert.Equal(InputContext.Global,binding.Context);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
