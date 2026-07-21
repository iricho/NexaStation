using Microsoft.UI.Xaml.Controls;
using NexaStation.Core;
using NexaStation.Input.Glyphs;

namespace NexaStation.App.Controls;

public sealed partial class NexaInputHintBar : UserControl
{
    public NexaInputHintBar() => InitializeComponent();
    public void Configure(IInputGlyphService glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs); PrimaryHints.Children.Clear();
        Add(glyphs, InputAction.Confirm, "Select"); Add(glyphs, InputAction.Cancel, "Back"); Add(glyphs, InputAction.Search, "Search"); Add(glyphs, InputAction.Favorite, "Favorite"); Add(glyphs, InputAction.ContextMenu, "Item actions"); Add(glyphs, InputAction.Options, "Options");
        QuickMenuHint.Configure(glyphs, InputAction.QuickMenu, "Quick Menu", InputGlyphContext.Footer, true);
    }
    private void Add(IInputGlyphService glyphs, InputAction action, string label) { NexaInputHint hint = new(); hint.Configure(glyphs, action, label); PrimaryHints.Children.Add(hint); }
}
