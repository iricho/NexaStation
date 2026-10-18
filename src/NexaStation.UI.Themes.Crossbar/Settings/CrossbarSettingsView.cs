using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using NexaStation.UI.Settings;
using NexaStation.UI.Themes;

namespace NexaStation.UI.Themes.Crossbar;

public sealed class CrossbarSettingsView : Grid
{
    private readonly SettingsPresentationContext _context;
    private readonly ListView _categories=new(){SelectionMode=ListViewSelectionMode.Single};
    private readonly StackPanel _content=new(){Spacing=12};
    private readonly TextBox _search=new(){PlaceholderText="Search settings",MinWidth=400};
    private readonly TextBlock _breadcrumb=new(){Text="Settings",Opacity=.72};

    public CrossbarSettingsView(SettingsPresentationContext context)
    {
        _context=context;Background=Application.Current.Resources.TryGetValue("NexaBackgroundBrush",out object? backdrop)&&backdrop is Brush brush?brush:new SolidColorBrush(Color.FromArgb(255,9,11,19));Padding=new Thickness(64,40,64,40);RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});Grid header=new(){ColumnSpacing=18};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.Children.Add(new StackPanel{Spacing=5,Children={new TextBlock{Text="SETTINGS",FontSize=32,FontWeight=Microsoft.UI.Text.FontWeights.Light},_breadcrumb,_search}});Button close=new(){Content="Back",MinWidth=110,VerticalAlignment=VerticalAlignment.Top};close.Click+=async(_,_)=>await _context.CloseAsync();Grid.SetColumn(close,1);header.Children.Add(close);Children.Add(header);
        Grid body=new(){ColumnSpacing=36,Margin=new Thickness(0,28,0,0)};body.ColumnDefinitions.Add(new(){Width=new GridLength(300)});body.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});_categories.ItemsSource=_context.Catalog.Categories.Select(value=>new CategoryItem(value.Id,value.IconGlyph,value.Title,value.Description)).ToArray();_categories.DisplayMemberPath=nameof(CategoryItem.Title);_categories.SelectionChanged+=CategoryChanged;body.Children.Add(_categories);ScrollViewer scroll=new(){Content=_content};Grid.SetColumn(scroll,1);body.Children.Add(scroll);SetRow(body,1);Children.Add(body);_search.TextChanged+=SearchChanged;
        if(context.InitialSettingId is { } id){SettingDescriptor setting=context.Catalog.GetById(id);SelectCategory(setting.CategoryId,setting.Id);}else _categories.SelectedIndex=0;
    }

    private void CategoryChanged(object sender,SelectionChangedEventArgs e){if(_categories.SelectedItem is CategoryItem category)RenderCategory(category.Id);}
    private void SelectCategory(string id,string? focus=null){CategoryItem? category=((IEnumerable<CategoryItem>)_categories.ItemsSource).FirstOrDefault(value=>value.Id==id);if(category is null)return;_categories.SelectedItem=category;RenderCategory(id,focus);}
    private void RenderCategory(string id,string? focus=null)
    {
        SettingsCategoryDescriptor category=_context.Catalog.Categories.Single(value=>value.Id==id);_breadcrumb.Text=$"Settings › {category.Title}";_content.Children.Clear();_content.Children.Add(new TextBlock{Text=category.Title,FontSize=38,FontWeight=Microsoft.UI.Text.FontWeights.Light});_content.Children.Add(new TextBlock{Text=category.Description,FontSize=17,Opacity=.75,TextWrapping=TextWrapping.Wrap});foreach(IGrouping<string,SettingDescriptor> group in _context.Catalog.GetCategory(id).GroupBy(value=>value.GroupId)){_content.Children.Add(new TextBlock{Text=group.Key.Replace('-', ' ').ToUpperInvariant(),FontSize=14,CharacterSpacing=100,Opacity=.7,Margin=new Thickness(0,16,0,0)});foreach(SettingDescriptor setting in group){string badges=string.Join(" · ",new[]{setting.Recommended?"RECOMMENDED":null,setting.Experimental?"EXPERIMENTAL":null,setting.RestartRequired?"RESTART REQUIRED":null,setting.Dangerous?"DANGEROUS":null}.Where(value=>value is not null));Button row=new(){Tag=setting.Id,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(18,13,18,13),Content=new StackPanel{Spacing=4,Children={new TextBlock{Text=setting.Title,FontSize=20},new TextBlock{Text=setting.Description,TextWrapping=TextWrapping.Wrap,Opacity=.72},new TextBlock{Text=badges,FontSize=12,Visibility=badges.Length==0?Visibility.Collapsed:Visibility.Visible}}}};row.Click+=async(_,_)=>await _context.OpenSettingAsync(setting.Id);_content.Children.Add(row);if(setting.Id==focus)row.Loaded+=(_,_)=>row.Focus(FocusState.Programmatic);}}Button reset=new(){Content="Reset Section",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,18,0,0)};reset.Click+=async(_,_)=>await _context.ResetCategoryAsync(id);_content.Children.Add(reset);
    }

    private async void SearchChanged(object sender,TextChangedEventArgs e)
    {
        if(string.IsNullOrWhiteSpace(_search.Text)){if(_categories.SelectedItem is CategoryItem category)RenderCategory(category.Id);return;}IReadOnlyList<SettingsSearchResult> results=await _context.Search.SearchAsync(_search.Text);_breadcrumb.Text="Settings › Search";_content.Children.Clear();_content.Children.Add(new TextBlock{Text=$"SEARCH · {results.Count} RESULT(S)",FontSize=22});foreach(SettingsSearchResult result in results){Button row=new(){HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Content=new StackPanel{Spacing=3,Children={new TextBlock{Text=result.Title,FontSize=20},new TextBlock{Text=result.Breadcrumb,Opacity=.65},new TextBlock{Text=result.Description,TextWrapping=TextWrapping.Wrap,Opacity=.8}}}};row.Click+=(_,_)=>SelectCategory(result.CategoryId,result.SettingId);_content.Children.Add(row);}
    }
    private sealed record CategoryItem(string Id,string Icon,string Title,string Description);
}
