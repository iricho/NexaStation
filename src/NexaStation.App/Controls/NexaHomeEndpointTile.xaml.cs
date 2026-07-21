using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NexaStation.App.Controls;

public sealed partial class NexaHomeEndpointTile : UserControl
{
    public NexaHomeEndpointTile()=>InitializeComponent();
    public event RoutedEventHandler? Click;
    public string Label{get=>(string)GetValue(LabelProperty);set=>SetValue(LabelProperty,value);}
    public static readonly DependencyProperty LabelProperty=DependencyProperty.Register(nameof(Label),typeof(string),typeof(NexaHomeEndpointTile),new PropertyMetadata("View All"));
    public string Glyph{get=>(string)GetValue(GlyphProperty);set=>SetValue(GlyphProperty,value);}
    public static readonly DependencyProperty GlyphProperty=DependencyProperty.Register(nameof(Glyph),typeof(string),typeof(NexaHomeEndpointTile),new PropertyMetadata("\uE71D"));
    private void Endpoint_Click(object sender,RoutedEventArgs e)=>Click?.Invoke(this,e);
}
