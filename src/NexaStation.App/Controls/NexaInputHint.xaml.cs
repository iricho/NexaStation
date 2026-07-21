using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using NexaStation.Core;
using NexaStation.Input.Glyphs;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

namespace NexaStation.App.Controls;

public sealed partial class NexaInputHint : UserControl
{
    private IInputGlyphService? _glyphs;
    private InputAction _action;
    private string _label = string.Empty;
    private InputGlyphContext _context;
    private bool _isHold;

    public NexaInputHint() { InitializeComponent(); Unloaded += OnUnloaded; }

    public void Configure(IInputGlyphService glyphs, InputAction action, string label, InputGlyphContext context = InputGlyphContext.Footer, bool isHold = false)
    {
        if (_glyphs is not null) _glyphs.GlyphSetChanged -= OnGlyphSetChanged;
        _glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs)); _action = action; _label = label; _context = context; _isHold = isHold;
        _glyphs.GlyphSetChanged += OnGlyphSetChanged; Render();
    }

    private void OnGlyphSetChanged(object? sender, InputGlyphSetChangedEventArgs e) => DispatcherQueue.TryEnqueue(Render);
    private void OnUnloaded(object sender, RoutedEventArgs e) { if (_glyphs is not null) _glyphs.GlyphSetChanged -= OnGlyphSetChanged; }
    private void Render()
    {
        if (_glyphs is null) return;
        InputGlyph glyph = _glyphs.Resolve(_action, _context); bool minimal = glyph.GlyphSetId == InputGlyphSetId.MinimalText;
        string? assetUri = InputGlyphAssetUri.Resolve(glyph); bool highContrast = new AccessibilitySettings().HighContrast; bool showAsset = assetUri is not null && !highContrast;
        GlyphSurface.Visibility = minimal ? Visibility.Collapsed : Visibility.Visible; GlyphImage.Visibility = showAsset ? Visibility.Visible : Visibility.Collapsed; GlyphText.Visibility = showAsset ? Visibility.Collapsed : Visibility.Visible; GlyphText.Text = glyph.TextFallback; GlyphImage.Source = showAsset ? new BitmapImage(new Uri(assetUri!)) : null; LabelText.Text = _isHold ? $"Hold {_label}" : _label;
        AutomationProperties.SetName(this, _isHold ? $"Hold {glyph.AccessibilityLabel} for {_label}" : $"{glyph.AccessibilityLabel}, {_label}");
    }
}
