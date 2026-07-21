using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using NexaStation.UI.Navigation;
using NexaStation.UI.Themes;
using NexaStation.UI.Settings;

namespace NexaStation.UI.Themes.Nexa;

public sealed class NexaTheme : INexaTheme
{
    private static readonly IReadOnlyDictionary<NavigationDestination,Type> Pages=ThemePages.Create();
    public ThemeDescriptor Descriptor { get; }=new("nexa.default","Nexa","NexaStation's modern artwork-first dashboard.",new(1,0,0),"NexaStation",true,true,true,true,true,false,"ThemePreview.Nexa",new(0,11,0),"Modern dashboard","Controller, keyboard, mouse, or touch","Balanced");
    public FrameworkElement CreateShell(ThemeShellContext context)=>new NexaThemeShell(context);
    public FrameworkElement CreateSettingsView(SettingsPresentationContext context)=>new NexaSettingsView(context);
    public IReadOnlyDictionary<NavigationDestination,Type> GetPageMappings()=>Pages;
    public ThemeResourceBundle GetResources()=>new([Resources.Create],["Theme.Nexa.Background","Theme.Nexa.Accent","Theme.Nexa.Focus"]);
    public ThemeValidationResult Validate()=>ThemeValidation.Validate(Descriptor,Pages,GetResources());
}

public sealed class NexaThemeShell : Grid, IDisposable
{
    public NexaThemeShell(ThemeShellContext context)
    {
        Background=new SolidColorBrush(Color.FromArgb(255,9,11,19));Padding=new Thickness(48,28,48,32);RowDefinitions.Add(new(){Height=new GridLength(72)});RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});RowDefinitions.Add(new(){Height=new GridLength(46)});
        TextBlock brand=new(){Text="NEXASTATION · NEXA",FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};Children.Add(brand);
        Border content=new(){Background=new SolidColorBrush(Color.FromArgb(224,23,27,43)),CornerRadius=new CornerRadius(18),Padding=new Thickness(30),Child=new TextBlock{Text=$"{context.NavigationState.Destination}\n\nThe default Nexa presentation keeps the existing feature view model and commands.",FontSize=26,TextWrapping=TextWrapping.Wrap}};SetRow(content,1);Children.Add(content);
        TextBlock footer=new(){Text="Confirm  Select    Cancel  Back    Search    Quick Menu",Opacity=.72,VerticalAlignment=VerticalAlignment.Center};SetRow(footer,2);Children.Add(footer);
    }
    public void Dispose()=>Children.Clear();
}

internal static class Resources
{
    public static ResourceDictionary Create()=>new(){["Theme.Nexa.Background"]=Color.FromArgb(255,9,11,19),["Theme.Nexa.Surface"]=Color.FromArgb(255,23,27,43),["Theme.Nexa.Accent"]=Color.FromArgb(255,45,212,191),["Theme.Nexa.Focus"]=Color.FromArgb(255,255,255,255)};
}

internal static class ThemeValidation
{
    public static ThemeValidationResult Validate(ThemeDescriptor descriptor,IReadOnlyDictionary<NavigationDestination,Type> pages,ThemeResourceBundle resources)
    {
        List<ThemeValidationIssue> issues=[];foreach(NavigationDestination destination in Enum.GetValues<NavigationDestination>())if(!pages.ContainsKey(destination))issues.Add(new("MissingDestination",$"{descriptor.DisplayName} does not map {destination}."));if(resources.DictionaryFactories.Count==0)issues.Add(new("MissingResources","Theme has no resource dictionary."));return new(issues.Count==0,issues);
    }
}
