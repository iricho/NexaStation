using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace NexaStation.App.Controls;

public sealed partial class NexaArtworkSurface : UserControl
{
    public NexaArtworkSurface(){InitializeComponent();Loaded+=(_,_)=>UpdateLayers();}
    public ImageSource? Source{get=>(ImageSource?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}
    public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(ImageSource),typeof(NexaArtworkSurface),new PropertyMetadata(null,OnPresentationChanged));
    public Brush? FallbackBrush{get=>(Brush?)GetValue(FallbackBrushProperty);set=>SetValue(FallbackBrushProperty,value);}
    public static readonly DependencyProperty FallbackBrushProperty=DependencyProperty.Register(nameof(FallbackBrush),typeof(Brush),typeof(NexaArtworkSurface),new PropertyMetadata(null));
    public string FallbackText{get=>(string)GetValue(FallbackTextProperty);set=>SetValue(FallbackTextProperty,value);}
    public static readonly DependencyProperty FallbackTextProperty=DependencyProperty.Register(nameof(FallbackText),typeof(string),typeof(NexaArtworkSurface),new PropertyMetadata("?"));
    public Stretch ArtworkStretch{get=>(Stretch)GetValue(ArtworkStretchProperty);set=>SetValue(ArtworkStretchProperty,value);}
    public static readonly DependencyProperty ArtworkStretchProperty=DependencyProperty.Register(nameof(ArtworkStretch),typeof(Stretch),typeof(NexaArtworkSurface),new PropertyMetadata(Stretch.UniformToFill));
    private static void OnPresentationChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)=>(sender as NexaArtworkSurface)?.UpdateLayers();
    private void ArtworkImage_ImageFailed(object sender,ExceptionRoutedEventArgs e){ArtworkImage.Visibility=Visibility.Collapsed;FallbackLayer.Visibility=Visibility.Visible;}
    private void UpdateLayers(){bool artwork=Source is not null;ArtworkImage.Visibility=artwork?Visibility.Visible:Visibility.Collapsed;FallbackLayer.Visibility=artwork?Visibility.Collapsed:Visibility.Visible;}
}
