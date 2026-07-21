using NexaStation.Core;
using NexaStation.Input.Detection;
using NexaStation.Input.Devices;
using NexaStation.Input.Mapping;

namespace NexaStation.Input.Glyphs;

public sealed class InputGlyphService : IInputGlyphService, IInputPromptPreferenceService, IDisposable
{
    private readonly IActiveInputModeService _activeMode;
    private readonly IInputBindingResolver _bindings;
    private PromptStyle _promptStyle;
    private NintendoConfirmConvention _nintendoConvention;
    private InputGlyphSetId _activeGlyphSet;

    public InputGlyphService(IActiveInputModeService activeMode, IInputBindingResolver bindings)
    {
        _activeMode = activeMode ?? throw new ArgumentNullException(nameof(activeMode));
        _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        _activeGlyphSet = ResolveSet(activeMode.CurrentMode);
        _activeMode.ModeChanged += OnModeChanged;
    }

    public InputGlyphSetId ActiveGlyphSet => _activeGlyphSet;
    public PromptStyle PromptStyle => _promptStyle;
    public NintendoConfirmConvention NintendoConvention => _nintendoConvention;
    public event EventHandler<InputGlyphSetChangedEventArgs>? GlyphSetChanged;

    public void Apply(PromptStyle promptStyle, NintendoConfirmConvention convention = NintendoConfirmConvention.PhysicalLabels)
    {
        _promptStyle = promptStyle;
        _nintendoConvention = convention;
        UpdateSet();
    }

    public InputGlyph Resolve(InputAction action, InputGlyphContext context)
    {
        InputContext mappingContext = context switch
        {
            InputGlyphContext.QuickMenu => InputContext.QuickMenu,
            InputGlyphContext.MediaPlayer => InputContext.MediaPlayer,
            InputGlyphContext.OnScreenKeyboard => InputContext.OnScreenKeyboard,
            InputGlyphContext.Calibration => InputContext.DisplayCalibration,
            InputGlyphContext.Dialog or InputGlyphContext.Recovery => InputContext.Dialog,
            _ => InputContext.Dashboard,
        };
        bool keyboardPresentation = _activeGlyphSet is InputGlyphSetId.KeyboardMouse or InputGlyphSetId.MinimalText;
        PhysicalInput? physical = _bindings.ActiveBindings
            .Where(binding => binding.Action == action && binding.Context == mappingContext)
            .Concat(_bindings.ActiveBindings.Where(binding => binding.Action == action && binding.Context == InputContext.Global))
            .Select(binding => binding.PhysicalInput)
            .FirstOrDefault(input => keyboardPresentation
                ? input.Kind is PhysicalInputKind.KeyboardKey or PhysicalInputKind.MouseButton
                : input.Kind is PhysicalInputKind.ControllerButton or PhysicalInputKind.ControllerAxis or PhysicalInputKind.ControllerChord);
        string code = physical?.NormalizedCode ?? action.ToString().ToUpperInvariant();
        string label = Label(_activeGlyphSet, code, action);
        string assetKey = AssetKey(_activeGlyphSet, code);
        bool chord = code.Contains('+', StringComparison.Ordinal);
        return new(action, _activeGlyphSet, assetKey, label, $"{label} button, {Readable(action)}", code, action is InputAction.QuickMenu or InputAction.EmergencyDisable, chord);
    }

    public IReadOnlyList<InputGlyph> ResolveChord(IReadOnlyList<InputAction> actions, InputGlyphContext context)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return actions.Select((action, index) => Resolve(action, context) with { IsChord = true, Order = index }).ToArray();
    }

    public void Dispose() => _activeMode.ModeChanged -= OnModeChanged;
    private void OnModeChanged(object? sender, ActiveInputModeChangedEventArgs e) => UpdateSet();
    private void UpdateSet()
    {
        InputGlyphSetId next = ResolveSet(_activeMode.CurrentMode);
        if (next == _activeGlyphSet) return;
        _activeGlyphSet = next;
        GlyphSetChanged?.Invoke(this, new(next));
    }

    private InputGlyphSetId ResolveSet(InputMode mode) => _promptStyle switch
    {
        PromptStyle.Keyboard => InputGlyphSetId.KeyboardMouse,
        PromptStyle.Xbox => InputGlyphSetId.Xbox,
        PromptStyle.PlayStation => InputGlyphSetId.PlayStation,
        PromptStyle.Nintendo => _nintendoConvention == NintendoConfirmConvention.PhysicalLabels ? InputGlyphSetId.NintendoPhysicalLabels : InputGlyphSetId.NintendoConfirmPosition,
        PromptStyle.Generic => InputGlyphSetId.GenericNumbered,
        PromptStyle.MinimalText => InputGlyphSetId.MinimalText,
        _ => mode switch
        {
            InputMode.XboxController => InputGlyphSetId.Xbox,
            InputMode.PlayStationController => InputGlyphSetId.PlayStation,
            InputMode.NintendoController => _nintendoConvention == NintendoConfirmConvention.PhysicalLabels ? InputGlyphSetId.NintendoPhysicalLabels : InputGlyphSetId.NintendoConfirmPosition,
            InputMode.GenericController or InputMode.UnknownController => InputGlyphSetId.GenericNumbered,
            InputMode.Mouse or InputMode.Touch => InputGlyphSetId.MinimalText,
            _ => InputGlyphSetId.KeyboardMouse,
        },
    };

    private static string Label(InputGlyphSetId set, string code, InputAction action)
    {
        if (set is InputGlyphSetId.KeyboardMouse or InputGlyphSetId.MinimalText) return KeyboardLabel(code, action);
        return set switch
        {
            InputGlyphSetId.Xbox => ControllerLabel(code, "A", "B", "X", "Y", "Menu", "View", "LB", "RB", "LT", "RT"),
            InputGlyphSetId.PlayStation => ControllerLabel(code, "Cross", "Circle", "Square", "Triangle", "Options", "Create", "L1", "R1", "L2", "R2"),
            InputGlyphSetId.NintendoConfirmPosition => ControllerLabel(code, "B", "A", "Y", "X", "+", "−", "L", "R", "ZL", "ZR"),
            InputGlyphSetId.NintendoPhysicalLabels => ControllerLabel(code, "A", "B", "Y", "X", "+", "−", "L", "R", "ZL", "ZR"),
            _ => ControllerLabel(code, "Button 1", "Button 2", "Button 3", "Button 4", "Start", "Select", "Left Shoulder", "Right Shoulder", "Left Trigger", "Right Trigger"),
        };
    }

    private static string KeyboardLabel(string code, InputAction action) => code switch
    {
        "ESCAPE" => "Esc", "CTRL+Q" => "Ctrl+Q", "ENTER" => "Enter", "MENU" => "Menu", _ when code.StartsWith("FACE_", StringComparison.Ordinal) => Readable(action), _ => code.Replace('_', ' '),
    };

    private static string AssetKey(InputGlyphSetId set, string code)
    {
        if (set != InputGlyphSetId.PlayStation) return $"InputGlyph.{set}.{code.Replace('+', '.')}";
        string semantic = code switch
        {
            "FACE_SOUTH" => "Cross", "FACE_EAST" => "Circle", "FACE_WEST" => "Square", "FACE_NORTH" => "Triangle",
            "DPAD_UP" or "DPAD_DOWN" or "DPAD_LEFT" or "DPAD_RIGHT" => "DPad",
            "MENU" => "Options", "VIEW" => "Create", "LEFT_SHOULDER" => "L1", "RIGHT_SHOULDER" => "R1", "LEFT_TRIGGER" => "L2", "RIGHT_TRIGGER" => "R2",
            _ => code.Replace('+', '.'),
        };
        return $"InputGlyph.PlayStation.{semantic}";
    }

    private static string ControllerLabel(string code, string south, string east, string west, string north, string menu, string view, string lb, string rb, string lt, string rt) => code switch
    {
        "FACE_SOUTH" => south, "FACE_EAST" => east, "FACE_WEST" => west, "FACE_NORTH" => north,
        "MENU" => menu, "VIEW" => view, "LEFT_SHOULDER" => lb, "RIGHT_SHOULDER" => rb, "LEFT_TRIGGER" => lt, "RIGHT_TRIGGER" => rt,
        "DPAD_UP" => "D-pad Up", "DPAD_DOWN" => "D-pad Down", "DPAD_LEFT" => "D-pad Left", "DPAD_RIGHT" => "D-pad Right",
        "LEFT_SHOULDER+RIGHT_SHOULDER+LEFT_STICK+RIGHT_STICK" => $"{lb}+{rb}+L3+R3",
        "LEFT_SHOULDER+RIGHT_SHOULDER+FACE_WEST" => $"{lb}+{rb}+{west}",
        _ => code.Replace('_', ' '),
    };

    private static string Readable(InputAction action) => action switch
    {
        InputAction.ContextMenu => "Context menu", InputAction.QuickMenu => "Quick Menu", InputAction.PreviousSection => "Previous section", InputAction.NextSection => "Next section", InputAction.ReturnToDesktop => "Return to desktop", InputAction.EmergencyDisable => "Emergency reset", _ => action.ToString(),
    };
}
