using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using NexaStation.UI.Navigation;
using NexaStation.UI.Themes;
using NexaStation.UI.Settings;

namespace NexaStation.UI.Themes.Crossbar;

public sealed class CrossbarTheme : INexaTheme
{
    private static readonly IReadOnlyDictionary<NavigationDestination,Type> Pages=ThemePages.Create();
    public ThemeDescriptor Descriptor { get; }=new("nexa.crossbar","Nexa Crossbar","An original TV-first horizontal category presentation with secondary vertical navigation.",new(1,0,0),"NexaStation",true,true,true,false,false,true,"ThemePreview.Crossbar",new(0,11,0),"Horizontal crossbar","Controller or keyboard","Balanced to cinematic");
    public FrameworkElement CreateShell(ThemeShellContext context)=>new CrossbarThemeShell(context);
    public FrameworkElement CreateSettingsView(SettingsPresentationContext context)=>new CrossbarSettingsView(context);
    public IReadOnlyDictionary<NavigationDestination,Type> GetPageMappings()=>Pages;
    public ThemeResourceBundle GetResources()=>new([Resources.Create],["Theme.Crossbar.Background","Theme.Crossbar.Wave","Theme.Crossbar.Focus"]);
    public ThemeValidationResult Validate()=>ThemeValidation.Validate(Descriptor,Pages,GetResources());
}

public sealed class CrossbarThemeShell : Grid, IDisposable
{
    private readonly List<Button> _categoryButtons=[];
    public CrossbarThemeShell(ThemeShellContext context)
    {
        Background=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1),GradientStops={new GradientStop{Color=Color.FromArgb(255,4,18,48),Offset=0},new GradientStop{Color=Color.FromArgb(255,7,74,112),Offset=.55},new GradientStop{Color=Color.FromArgb(255,5,15,38),Offset=1}}};Padding=new Thickness(64,38,64,38);RowDefinitions.Add(new(){Height=new GridLength(112)});RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});RowDefinitions.Add(new(){Height=new GridLength(46)});
        Canvas waves=new(){IsHitTestVisible=false,Opacity=context.ReducedMotion?.45:.72};Polyline wave=new(){Stroke=new SolidColorBrush(Color.FromArgb(135,82,220,255)),StrokeThickness=5};wave.Points.Add(new(0,220));wave.Points.Add(new(260,125));wave.Points.Add(new(520,285));wave.Points.Add(new(820,180));wave.Points.Add(new(1120,110));wave.Points.Add(new(1440,190));wave.Points.Add(new(1800,250));waves.Children.Add(wave);Children.Add(waves);
        StackPanel categories=new(){Orientation=Orientation.Horizontal,Spacing=18,HorizontalAlignment=HorizontalAlignment.Center};foreach((NavigationDestination destination,string glyph) in Primary()){Button button=new(){Content=new StackPanel{Spacing=5,Children={new FontIcon{Glyph=glyph,FontSize=30},new TextBlock{Text=destination.ToString(),FontSize=13}}},MinWidth=86,MinHeight=82,Tag=destination};button.Click+=(_,_)=>context.Navigate?.Invoke(destination);categories.Children.Add(button);_categoryButtons.Add(button);}Children.Add(categories);
        Grid content=new(){ColumnSpacing=28};content.ColumnDefinitions.Add(new(){Width=new GridLength(240)});content.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});ListView secondary=new(){ItemsSource=Secondary(context.NavigationState.Destination),SelectionMode=ListViewSelectionMode.Single,SelectedIndex=0};content.Children.Add(secondary);Border feature=new(){CornerRadius=new CornerRadius(14),Background=new SolidColorBrush(Color.FromArgb(92,0,0,0)),Padding=new Thickness(36),Child=new StackPanel{Spacing=18,Children={new TextBlock{Text=context.NavigationState.Destination.ToString(),FontSize=36,FontWeight=Microsoft.UI.Text.FontWeights.Light},new TextBlock{Text="Shared NexaStation data, commands, context actions, and adaptive prompts are presented through Crossbar-specific chrome.",FontSize=18,TextWrapping=TextWrapping.Wrap,MaxWidth=760}}}};Grid.SetColumn(feature,1);content.Children.Add(feature);SetRow(content,1);Children.Add(content);
        TextBlock footer=new(){Text="Navigate    Confirm  Select    Cancel  Back    Square  Context    Options  More    Quick Menu",HorizontalAlignment=HorizontalAlignment.Center,Opacity=.82,VerticalAlignment=VerticalAlignment.Center};SetRow(footer,2);Children.Add(footer);Loaded+=(_,_)=>_categoryButtons.FirstOrDefault(button=>Equals(button.Tag,context.NavigationState.Destination))?.Focus(FocusState.Programmatic);
    }
    public void Dispose(){foreach(Button button in _categoryButtons)button.Click-=NoOp;_categoryButtons.Clear();Children.Clear();}
    private static void NoOp(object sender,RoutedEventArgs e){}
    private static IEnumerable<(NavigationDestination,string)> Primary()=>[(NavigationDestination.Home,"\uE80F"),(NavigationDestination.Games,"\uE7FC"),(NavigationDestination.Apps,"\uECAA"),(NavigationDestination.Media,"\uE714"),(NavigationDestination.Gallery,"\uEB9F"),(NavigationDestination.Library,"\uE8F1"),(NavigationDestination.Collections,"\uE8B7"),(NavigationDestination.Settings,"\uE713"),(NavigationDestination.ExitToDesktop,"\uE7E8")];
    private static string[] Secondary(NavigationDestination destination)=>destination switch{NavigationDestination.Games=>["Continue Playing","All Games","Installed Games","Emulated Games","Favorites","Collections"],NavigationDestination.Apps=>["Recent","Installed","Media Apps","Communication","Browsers","Utilities","Remote Play","Discover Apps"],NavigationDestination.Media=>["Continue Watching","Local Videos","Music Apps","Video Apps","Gallery Recordings","Remote Play","Favorites"],NavigationDestination.Gallery=>["Recent","Photos","Videos","Albums","Favorites","Screenshots","Recordings","Sources"],NavigationDestination.Settings=>["Categories","Frequently Used","Search","Advanced","Help"],_=>["Featured","Recent","Favorites"]};
}

internal static class Resources { public static ResourceDictionary Create()=>new(){["Theme.Crossbar.Background"]=Color.FromArgb(255,4,18,48),["Theme.Crossbar.Surface"]=Color.FromArgb(92,0,0,0),["Theme.Crossbar.Accent"]=Color.FromArgb(255,82,220,255),["Theme.Crossbar.Focus"]=Color.FromArgb(255,255,255,255)}; }
internal static class ThemeValidation { public static ThemeValidationResult Validate(ThemeDescriptor descriptor,IReadOnlyDictionary<NavigationDestination,Type> pages,ThemeResourceBundle resources){List<ThemeValidationIssue> issues=[];foreach(NavigationDestination destination in Enum.GetValues<NavigationDestination>())if(!pages.ContainsKey(destination))issues.Add(new("MissingDestination",$"{descriptor.DisplayName} does not map {destination}."));if(resources.DictionaryFactories.Count==0)issues.Add(new("MissingResources","Theme has no resource dictionary."));return new(issues.Count==0,issues);} }
