using NexaStation.Core;
using NexaStation.Input.Devices;
using NexaStation.Input.Mapping;

namespace NexaStation.Input.Glyphs;

public enum InputGlyphSetId { KeyboardMouse, Xbox, PlayStation, NintendoPhysicalLabels, NintendoConfirmPosition, GenericNumbered, UserCustom, MinimalText }
public enum PromptStyle { Automatic, Keyboard, Xbox, PlayStation, Nintendo, Generic, MinimalText }
public enum NintendoConfirmConvention { PhysicalLabels, ConfirmPosition }
public enum InputGlyphContext { Footer, Card, Hero, Dialog, QuickMenu, MediaPlayer, OnScreenKeyboard, Calibration, Recovery }

public sealed record InputGlyph(
    InputAction Action,
    InputGlyphSetId GlyphSetId,
    string AssetKey,
    string TextFallback,
    string AccessibilityLabel,
    string PhysicalButton,
    bool IsHoldAction = false,
    bool IsChord = false,
    int Order = 0);

public sealed class InputGlyphSetChangedEventArgs(InputGlyphSetId glyphSet) : EventArgs { public InputGlyphSetId GlyphSet { get; } = glyphSet; }

public interface IInputGlyphService
{
    InputGlyphSetId ActiveGlyphSet { get; }
    event EventHandler<InputGlyphSetChangedEventArgs>? GlyphSetChanged;
    InputGlyph Resolve(InputAction action, InputGlyphContext context);
    IReadOnlyList<InputGlyph> ResolveChord(IReadOnlyList<InputAction> actions, InputGlyphContext context);
}

public interface IInputPromptPreferenceService
{
    PromptStyle PromptStyle { get; }
    NintendoConfirmConvention NintendoConvention { get; }
    void Apply(PromptStyle promptStyle, NintendoConfirmConvention convention = NintendoConfirmConvention.PhysicalLabels);
}
