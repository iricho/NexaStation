using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.Core;
using NexaStation.Input.Glyphs;

namespace NexaStation.App.Controls;

public sealed class NexaInputChord : StackPanel
{
    public NexaInputChord() { Orientation = Orientation.Horizontal; Spacing = 6; }
    public void Configure(IInputGlyphService service, IReadOnlyList<InputAction> actions, InputGlyphContext context)
    {
        Children.Clear(); foreach (InputGlyph glyph in service.ResolveChord(actions, context)) Children.Add(new NexaKeycap { Text = glyph.TextFallback, AutomationLabel = glyph.AccessibilityLabel });
    }
}

public sealed class NexaHoldPrompt : ContentControl
{
    public void Configure(IInputGlyphService service, InputAction action, string label, InputGlyphContext context) { InputGlyph glyph = service.Resolve(action, context); Content = $"Hold {glyph.TextFallback} · {label}"; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, $"Hold {glyph.AccessibilityLabel} for {label}"); }
}

public sealed class NexaKeycap : ContentControl
{
    public string Text { get => Content?.ToString() ?? string.Empty; set => Content = value; }
    public string AutomationLabel { get => Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(this); set => Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, value); }
}

public sealed class NexaControllerGlyph : ContentControl
{
    public void Configure(InputGlyph glyph) { ArgumentNullException.ThrowIfNull(glyph); Content = glyph.TextFallback; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(this, glyph.AccessibilityLabel); }
}
