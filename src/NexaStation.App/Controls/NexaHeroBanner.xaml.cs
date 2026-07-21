using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace NexaStation.App.Controls;
public sealed partial class NexaHeroBanner:UserControl
{
    public NexaHeroBanner(){InitializeComponent();Loaded+=(_,_)=>BuildIndicators();}
    public event RoutedEventHandler? PrimaryClick;public event RoutedEventHandler? SecondaryClick;
    public event EventHandler<int>? HeroIndexRequested;
    public string Title{get=>(string)GetValue(TitleProperty);set=>SetValue(TitleProperty,value);}public static readonly DependencyProperty TitleProperty=DependencyProperty.Register(nameof(Title),typeof(string),typeof(NexaHeroBanner),new PropertyMetadata("Your world, ready to play"));
    public string Description{get=>(string)GetValue(DescriptionProperty);set=>SetValue(DescriptionProperty,value);}public static readonly DependencyProperty DescriptionProperty=DependencyProperty.Register(nameof(Description),typeof(string),typeof(NexaHeroBanner),new PropertyMetadata(string.Empty));
    public ImageSource? Artwork{get=>(ImageSource?)GetValue(ArtworkProperty);set=>SetValue(ArtworkProperty,value);}public static readonly DependencyProperty ArtworkProperty=DependencyProperty.Register(nameof(Artwork),typeof(ImageSource),typeof(NexaHeroBanner),new PropertyMetadata(null));
    public string PrimaryText{get=>(string)GetValue(PrimaryTextProperty);set=>SetValue(PrimaryTextProperty,value);}public static readonly DependencyProperty PrimaryTextProperty=DependencyProperty.Register(nameof(PrimaryText),typeof(string),typeof(NexaHeroBanner),new PropertyMetadata("Play Now"));
    public string SecondaryText{get=>(string)GetValue(SecondaryTextProperty);set=>SetValue(SecondaryTextProperty,value);}public static readonly DependencyProperty SecondaryTextProperty=DependencyProperty.Register(nameof(SecondaryText),typeof(string),typeof(NexaHeroBanner),new PropertyMetadata("Learn More"));
    public int IndicatorCount{get=>(int)GetValue(IndicatorCountProperty);set=>SetValue(IndicatorCountProperty,value);}public static readonly DependencyProperty IndicatorCountProperty=DependencyProperty.Register(nameof(IndicatorCount),typeof(int),typeof(NexaHeroBanner),new PropertyMetadata(0,OnIndicatorsChanged));
    public int SelectedIndex{get=>(int)GetValue(SelectedIndexProperty);set=>SetValue(SelectedIndexProperty,value);}public static readonly DependencyProperty SelectedIndexProperty=DependencyProperty.Register(nameof(SelectedIndex),typeof(int),typeof(NexaHeroBanner),new PropertyMetadata(0,OnIndicatorsChanged));
    private void PrimaryButton_Click(object sender,RoutedEventArgs e)=>PrimaryClick?.Invoke(this,e);private void SecondaryButton_Click(object sender,RoutedEventArgs e)=>SecondaryClick?.Invoke(this,e);
    private static void OnIndicatorsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)=>(sender as NexaHeroBanner)?.BuildIndicators();
    private void BuildIndicators()
    {
        if(HeroIndicators is null)return;HeroIndicators.Children.Clear();
        for(int index=0;index<Math.Clamp(IndicatorCount,0,5);index++)
        {
            int captured=index;Button button=new(){Width=22,Height=22,Padding=new Thickness(0),Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),BorderThickness=new Thickness(0),Content=new Microsoft.UI.Xaml.Shapes.Ellipse{Width=8,Height=8,Fill=index==SelectedIndex?(Brush)Application.Current.Resources["NexaAccentBrush"]:new SolidColorBrush(Windows.UI.Color.FromArgb(112,255,255,255))}};
            button.SetValue(AutomationProperties.NameProperty,$"Show featured item {index+1}");button.Click+=(_,_)=>HeroIndexRequested?.Invoke(this,captured);HeroIndicators.Children.Add(button);
        }
    }
}
