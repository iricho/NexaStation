using Microsoft.UI.Xaml.Controls;
using NexaStation.Input.Glyphs;

namespace NexaStation.App.Controls;

public sealed partial class NexaControllerFooter : UserControl
{
    public NexaControllerFooter() => InitializeComponent();
    public void Configure(IInputGlyphService glyphs) => AdaptiveHints.Configure(glyphs);
}
