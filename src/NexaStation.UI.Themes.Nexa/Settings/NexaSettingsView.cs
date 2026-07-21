using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using NexaStation.UI.Settings;
using NexaStation.UI.Themes;

namespace NexaStation.UI.Themes.Nexa;

public sealed class NexaSettingsView : Grid
{
    private readonly SettingsPresentationContext _context;
    private readonly StackPanel _content=new(){Spacing=12};
    private readonly TextBox _search=new(){PlaceholderText="Search settings",MinWidth=360,MaxWidth=680,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock _breadcrumb=new(){Text="Settings",Opacity=.7};

    public NexaSettingsView(SettingsPresentationContext context)
    {
        _context=context;Background=new SolidColorBrush(Color.FromArgb(255,9,11,19));Padding=new Thickness(48,30,48,30);RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        Grid header=new(){ColumnSpacing=12};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});StackPanel title=new(){Spacing=4,Children={new TextBlock{Text="Settings",FontSize=34,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold},_breadcrumb,_search}};header.Children.Add(title);Button close=new(){Content="Back",MinWidth=100,VerticalAlignment=VerticalAlignment.Top};close.Click+=async(_,_)=>await _context.CloseAsync();Grid.SetColumn(close,1);header.Children.Add(close);Children.Add(header);
        ScrollViewer scroller=new(){Content=_content,Margin=new Thickness(0,24,0,0)};SetRow(scroller,1);Children.Add(scroller);_search.TextChanged+=SearchChanged;ShowLanding();
        if(context.InitialSettingId is { } id)OpenDescriptor(id);
    }

    private void ShowLanding()
    {
        _breadcrumb.Text="Settings";_content.Children.Clear();_content.Children.Add(new TextBlock{Text="Frequently used",FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});StackPanel frequent=new(){Orientation=Orientation.Horizontal,Spacing=10};foreach(SettingDescriptor setting in _context.Catalog.Settings.Where(value=>value.Recommended).Take(5))frequent.Children.Add(SettingButton(setting,true));_content.Children.Add(frequent);_content.Children.Add(new TextBlock{Text="Categories",FontSize=22,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,14,0,0)});VariableSizedWrapGrid grid=new(){Orientation=Orientation.Horizontal,MaximumRowsOrColumns=4};foreach(SettingsCategoryDescriptor category in _context.Catalog.Categories.Where(value=>!value.Dangerous)){Button card=new(){Width=300,Height=112,Margin=new Thickness(6),HorizontalContentAlignment=HorizontalAlignment.Stretch,Content=new StackPanel{Spacing=6,Children={new FontIcon{Glyph=category.IconGlyph,FontSize=25,HorizontalAlignment=HorizontalAlignment.Left},new TextBlock{Text=category.Title,FontSize=18,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold},new TextBlock{Text=category.Description,TextWrapping=TextWrapping.Wrap,Opacity=.7,MaxLines=2}}}};card.Click+=(_,_)=>ShowCategory(category.Id);grid.Children.Add(card);}_content.Children.Add(grid);
        SettingsCategoryDescriptor recovery=_context.Catalog.Categories.Single(value=>value.Dangerous);Button danger=new(){Content=$"⚠ {recovery.Title}",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(6,18,6,6)};danger.Click+=(_,_)=>ShowCategory(recovery.Id);_content.Children.Add(danger);
    }

    private void ShowCategory(string categoryId,string? focusSetting=null)
    {
        SettingsCategoryDescriptor category=_context.Catalog.Categories.Single(value=>value.Id==categoryId);_breadcrumb.Text=$"Settings › {category.Title}";_content.Children.Clear();_content.Children.Add(new TextBlock{Text=category.Title,FontSize=30,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});_content.Children.Add(new TextBlock{Text=category.Description,TextWrapping=TextWrapping.Wrap,Opacity=.75});foreach(IGrouping<string,SettingDescriptor> group in _context.Catalog.GetCategory(categoryId).GroupBy(value=>value.GroupId)){_content.Children.Add(new TextBlock{Text=Humanize(group.Key),FontSize=19,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,16,0,2)});foreach(SettingDescriptor setting in group){Button button=SettingButton(setting,false);_content.Children.Add(button);if(setting.Id==focusSetting)button.Loaded+=(_,_)=>button.Focus(FocusState.Programmatic);}}Button reset=new(){Content="Reset this section",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,20,0,0)};reset.Click+=async(_,_)=>await _context.ResetCategoryAsync(categoryId);_content.Children.Add(reset);
    }

    private Button SettingButton(SettingDescriptor setting,bool compact)
    {
        string badges=string.Join(" · ",new[]{setting.Recommended?"Recommended":null,setting.Experimental?"Experimental":null,setting.RestartRequired?"Restart Required":null,setting.Dangerous?"Dangerous":null,setting.SaveBehavior==SettingSaveBehavior.Staged?"Apply/Cancel":null}.Where(value=>value is not null));StackPanel panel=new(){Spacing=3};panel.Children.Add(new TextBlock{Text=setting.Title,FontSize=compact?16:18,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold});if(!compact)panel.Children.Add(new TextBlock{Text=setting.Description,TextWrapping=TextWrapping.Wrap,Opacity=.75});if(badges.Length>0)panel.Children.Add(new TextBlock{Text=badges,Foreground=new SolidColorBrush(setting.Dangerous?Color.FromArgb(255,255,155,120):Color.FromArgb(255,45,212,191)),FontSize=12});Button button=new(){Content=panel,Tag=setting.Id,MinWidth=compact?190:0,HorizontalAlignment=compact?HorizontalAlignment.Left:HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(16,12,16,12)};button.Click+=async(_,_)=>await _context.OpenSettingAsync(setting.Id);return button;
    }

    private async void SearchChanged(object sender,TextChangedEventArgs e)
    {
        string query=_search.Text;if(string.IsNullOrWhiteSpace(query)){ShowLanding();return;}IReadOnlyList<SettingsSearchResult> results=await _context.Search.SearchAsync(query);_breadcrumb.Text="Settings › Search";_content.Children.Clear();_content.Children.Add(new TextBlock{Text=$"{results.Count} result(s)",FontSize=20});foreach(SettingsSearchResult result in results){Button button=new(){HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Content=new StackPanel{Spacing=3,Children={new TextBlock{Text=result.Title,FontSize=18},new TextBlock{Text=result.Breadcrumb,Opacity=.65},new TextBlock{Text=result.Description,TextWrapping=TextWrapping.Wrap,Opacity=.8}}}};button.Click+=(_,_)=>ShowCategory(result.CategoryId,result.SettingId);_content.Children.Add(button);}
    }
    private void OpenDescriptor(string id){SettingDescriptor setting=_context.Catalog.GetById(id);ShowCategory(setting.CategoryId,id);}
    private static string Humanize(string value)=>string.Join(' ',value.Split('-',StringSplitOptions.RemoveEmptyEntries).Select(part=>char.ToUpperInvariant(part[0])+part[1..]));
}
