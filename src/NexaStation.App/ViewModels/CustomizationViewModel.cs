using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Presentation.LayoutContracts;
using NexaStation.Core;
using NexaStation.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NexaStation.App.ViewModels;

public sealed class CustomizationViewModel(IInterfaceLayoutService layouts,IThemeService themes,IDashboardConfigurationService dashboards,ICollectionService collections,IPerformancePresetService performance,IInterfacePresentationService presentation,ILibraryRepository library,ICollectionMosaicService mosaics):ObservableObject
{
    private string _currentLayoutId="modern-dashboard";private string _currentThemeId="nexa-dark";private LayoutPresentationState? _presentation;
    public ObservableCollection<InterfaceLayoutDefinition> Layouts{get;}=[];public ObservableCollection<ThemeDefinition> Themes{get;}=[];public ObservableCollection<DashboardRowConfiguration> Rows{get;}=[];public ObservableCollection<CollectionDefinition> Collections{get;}=[];public ObservableCollection<CollectionCardViewModel> CollectionCards{get;}=[];
    public string CurrentLayoutId{get=>_currentLayoutId;private set=>SetProperty(ref _currentLayoutId,value);}public string CurrentThemeId{get=>_currentThemeId;private set=>SetProperty(ref _currentThemeId,value);}public LayoutPresentationState? Presentation{get=>_presentation;private set=>SetProperty(ref _presentation,value);}
    public async Task InitializeAsync(CancellationToken token=default){Layouts.ReplaceWith(await layouts.GetAvailableAsync(token));Themes.ReplaceWith(await themes.GetAvailableAsync(token));await RefreshCollectionsAsync(token);CurrentLayoutId=layouts.CurrentLayoutId;CurrentThemeId=themes.CurrentThemeId;await RefreshPresentationAsync(token);}
    public async Task ApplyLayoutAsync(string id,CancellationToken token=default){await layouts.ApplyAsync(id,token);CurrentLayoutId=id;await RefreshPresentationAsync(token);}
    public async Task PreviewThemeAsync(string id,CancellationToken token=default)=>await themes.PreviewAsync(id,token);public async Task CancelThemePreviewAsync(CancellationToken token=default)=>await themes.CancelPreviewAsync(token);public async Task ApplyThemeAsync(string id,CancellationToken token=default){await themes.ApplyAsync(id,token);CurrentThemeId=id;}
    public async Task RefreshPresentationAsync(CancellationToken token=default){InterfaceLayoutDefinition layout=(await layouts.GetAvailableAsync(token)).First(x=>x.Id==layouts.CurrentLayoutId);DashboardConfiguration dashboard=await dashboards.GetOrCreateAsync(layout.Id,token);Rows.ReplaceWith(await dashboards.GetRowsAsync(dashboard.Id,token));Presentation=presentation.Create(layout,dashboard,await performance.GetAsync(token));presentation.Activate(Presentation);}
    public async Task RefreshCollectionsAsync(CancellationToken token=default)
    {
        IReadOnlyList<CollectionDefinition> definitions=await collections.GetAsync(token);Collections.ReplaceWith(definitions);List<CollectionCardViewModel> cards=[];
        foreach(CollectionDefinition definition in definitions.Where(value=>!value.Hidden).OrderBy(value=>value.SortOrder))
        {
            IReadOnlyList<CollectionItem> items=await collections.GetItemsAsync(definition.Id,token);List<string> visuals=[];DateTime updated=definition.UpdatedAtUtc;
            foreach(CollectionItem entry in items.Where(value=>value.TargetType==CollectionTargetType.LibraryItem).OrderBy(value=>value.SortOrder).Take(4))
            {
                updated=entry.AddedAtUtc>updated?entry.AddedAtUtc:updated;LibraryItem? item=await library.GetByIdAsync(entry.TargetId,token);string? path=item?.CoverAsset?.LocalPath??item?.BackgroundAsset?.LocalPath;if(path is not null&&File.Exists(path))visuals.Add(path);
            }
            CollectionMosaicResult mosaic=await mosaics.ResolveAsync(new(definition.Id,visuals,updated),token);cards.Add(new(definition,items.Count,mosaic));
        }
        CollectionCards.ReplaceWith(cards);
    }
}
public sealed class CollectionCardViewModel
{
    public CollectionCardViewModel(CollectionDefinition definition,int itemCount,CollectionMosaicResult mosaic){Definition=definition;ItemCount=itemCount;MosaicResult=mosaic;if(mosaic.CachePath is { } path&&File.Exists(path))Mosaic=new BitmapImage(new Uri(path));}
    public CollectionDefinition Definition{get;}public string Id=>Definition.Id;public string Name=>Definition.Name;public string Description=>Definition.Description??(ItemCount==0?"Empty collection":"Mixed content collection");public int ItemCount{get;}public string ItemCountLabel=>ItemCount==1?"1 item":$"{ItemCount} items";public CollectionMosaicResult MosaicResult{get;}public BitmapImage? Mosaic{get;}public Visibility MosaicVisibility=>Mosaic is null?Visibility.Collapsed:Visibility.Visible;public Visibility FallbackVisibility=>Mosaic is null?Visibility.Visible:Visibility.Collapsed;public string AccessibilityName=>$"{Name}, {ItemCountLabel}";
}
internal static class ObservableCollectionExtensions{public static void ReplaceWith<T>(this ObservableCollection<T> collection,IEnumerable<T> values){collection.Clear();foreach(T value in values)collection.Add(value);}}
