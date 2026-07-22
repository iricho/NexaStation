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

public sealed class CustomizationViewModel(IInterfaceLayoutService layouts,IThemeService themes,IDashboardConfigurationService dashboards,ICollectionService collections,ISmartCollectionQueryService smartCollections,IPerformancePresetService performance,IInterfacePresentationService presentation,ILibraryRepository library,IGalleryPersistence gallery,IContentLifecycleService lifecycle,ICollectionMosaicService mosaics):ObservableObject
{
    private string _currentLayoutId="modern-dashboard";private string _currentThemeId="nexa-dark";private LayoutPresentationState? _presentation;private string _shelfTitle="Shelf";private string _shelfDescription=string.Empty;private bool _canManageShelf;private CollectionDefinition? _openShelf;private IReadOnlyList<ShelfItemViewModel> _allShelfItems=[];
    public ObservableCollection<InterfaceLayoutDefinition> Layouts{get;}=[];public ObservableCollection<ThemeDefinition> Themes{get;}=[];public ObservableCollection<DashboardRowConfiguration> Rows{get;}=[];public ObservableCollection<CollectionDefinition> Collections{get;}=[];public ObservableCollection<CollectionCardViewModel> CollectionCards{get;}=[];public ObservableCollection<CollectionCardViewModel> PersonalShelfCards{get;}=[];public ObservableCollection<CollectionCardViewModel> AutomaticShelfCards{get;}=[];public ObservableCollection<ShelfItemViewModel> ShelfItems{get;}=[];
    public string CurrentLayoutId{get=>_currentLayoutId;private set=>SetProperty(ref _currentLayoutId,value);}public string CurrentThemeId{get=>_currentThemeId;private set=>SetProperty(ref _currentThemeId,value);}public LayoutPresentationState? Presentation{get=>_presentation;private set=>SetProperty(ref _presentation,value);}public string ShelfTitle{get=>_shelfTitle;private set=>SetProperty(ref _shelfTitle,value);}public string ShelfDescription{get=>_shelfDescription;private set=>SetProperty(ref _shelfDescription,value);}public bool CanManageShelf{get=>_canManageShelf;private set=>SetProperty(ref _canManageShelf,value);}public CollectionDefinition? OpenShelf=>_openShelf;
    public async Task InitializeAsync(CancellationToken token=default){Layouts.ReplaceWith(await layouts.GetAvailableAsync(token));Themes.ReplaceWith(await themes.GetAvailableAsync(token));await RefreshCollectionsAsync(token);CurrentLayoutId=layouts.CurrentLayoutId;CurrentThemeId=themes.CurrentThemeId;await RefreshPresentationAsync(token);}
    public async Task ApplyLayoutAsync(string id,CancellationToken token=default){await layouts.ApplyAsync(id,token);CurrentLayoutId=id;await RefreshPresentationAsync(token);}
    public async Task PreviewThemeAsync(string id,CancellationToken token=default)=>await themes.PreviewAsync(id,token);public async Task CancelThemePreviewAsync(CancellationToken token=default)=>await themes.CancelPreviewAsync(token);public async Task ApplyThemeAsync(string id,CancellationToken token=default){await themes.ApplyAsync(id,token);CurrentThemeId=id;}
    public async Task RefreshPresentationAsync(CancellationToken token=default){InterfaceLayoutDefinition layout=(await layouts.GetAvailableAsync(token)).First(x=>x.Id==layouts.CurrentLayoutId);DashboardConfiguration dashboard=await dashboards.GetOrCreateAsync(layout.Id,token);Rows.ReplaceWith(await dashboards.GetRowsAsync(dashboard.Id,token));Presentation=presentation.Create(layout,dashboard,await performance.GetAsync(token));presentation.Activate(Presentation);}
    public async Task RefreshCollectionsAsync(CancellationToken token=default)
    {
        IReadOnlyList<CollectionDefinition> definitions=await collections.GetAsync(token);Collections.ReplaceWith(definitions);List<CollectionCardViewModel> cards=[];
        foreach(CollectionDefinition definition in definitions.Where(value=>!value.Hidden).OrderBy(value=>value.SortOrder))
        {
            IReadOnlyList<CollectionItem> items=await collections.GetItemsAsync(definition.Id,token);IReadOnlyList<CollectionResolvedItem> dynamicItems=definition.CollectionType==CollectionType.Smart?await smartCollections.EvaluateAsync(definition.Id,100,token):[];List<string> visuals=[];DateTime updated=definition.UpdatedAtUtc;
            foreach(CollectionItem entry in items.Where(value=>value.TargetType==CollectionTargetType.LibraryItem).OrderBy(value=>value.SortOrder).Take(4))
            {
                updated=entry.AddedAtUtc>updated?entry.AddedAtUtc:updated;LibraryItem? item=await library.GetByIdAsync(entry.TargetId,token);string? path=item?.CoverAsset?.LocalPath??item?.BackgroundAsset?.LocalPath;if(path is not null&&File.Exists(path))visuals.Add(path);
            }
            if(visuals.Count<4)foreach(LibraryItem item in dynamicItems.Select(value=>value.LibraryItem).Where(value=>value is not null).Cast<LibraryItem>().Take(4-visuals.Count)){string? path=item.CoverAsset?.LocalPath??item.BackgroundAsset?.LocalPath;if(path is not null&&File.Exists(path))visuals.Add(path);}CollectionMosaicResult mosaic=await mosaics.ResolveAsync(new(definition.Id,visuals,updated),token);cards.Add(new(definition,definition.CollectionType==CollectionType.Smart?dynamicItems.Count:items.Count,mosaic));
        }
        CollectionCards.ReplaceWith(cards);PersonalShelfCards.ReplaceWith(cards.Where(value=>!value.Id.StartsWith("auto:",StringComparison.Ordinal)));AutomaticShelfCards.ReplaceWith(cards.Where(value=>value.Id.StartsWith("auto:",StringComparison.Ordinal)));
    }

    public async Task OpenShelfAsync(CollectionDefinition definition,CancellationToken token=default)
    {
        _openShelf=definition;ShelfTitle=definition.Name;ShelfDescription=definition.Description??(definition.CollectionType==CollectionType.Smart?"Items are selected by Dynamic Shelf rules.":"Items you placed on this Shelf.");CanManageShelf=definition.CollectionType==CollectionType.Manual;
        IReadOnlyList<CollectionResolvedItem> items=definition.CollectionType==CollectionType.Smart?await smartCollections.EvaluateAsync(definition.Id,500,token):await collections.ResolveItemsAsync(definition.Id,token);
        SetShelfItems(items.Select((item,index)=>new ShelfItemViewModel(item,index)));
    }

    public async Task OpenLifecycleAsync(ContentLifecycleTargetType targetType,ContentLifecycleStatus status,string title,string description,CancellationToken token=default)
    {
        _openShelf=null;ShelfTitle=title;ShelfDescription=description;CanManageShelf=false;List<ShelfItemViewModel> items=[];
        foreach(ContentLifecycleEntry entry in await lifecycle.QueryAsync(new(targetType,status,500),token))
        {
            if(targetType==ContentLifecycleTargetType.LibraryItem&&await library.GetByIdAsync(entry.Target.TargetId,token) is { } libraryItem)items.Add(new(new(CollectionTargetType.LibraryItem,libraryItem.Id,libraryItem.Title,libraryItem.LaunchTarget is not null,libraryItem),items.Count));
            else if(targetType==ContentLifecycleTargetType.GalleryMediaItem&&await gallery.GetMediaItemAsync(entry.Target.TargetId,token) is { } media)items.Add(new(new(CollectionTargetType.GalleryMediaItem,media.Id,media.FileName,media.Available,GalleryMediaItem:media),items.Count));
        }
        SetShelfItems(items);
    }

    public async Task OpenSpecialShelfAsync(bool favorites,string title,CancellationToken token=default)
    {
        _openShelf=null;ShelfTitle=title;ShelfDescription=favorites?"Favorite games, apps, photos, and videos in one place.":"The latest things added to your library and Gallery.";CanManageShelf=false;List<CollectionResolvedItem> resolved=[];
        IReadOnlyList<LibraryItem> libraryItems=await library.QueryAsync(new(FavoriteOnly:favorites,IncludeHidden:false,SortMode:favorites?LibrarySortMode.Title:LibrarySortMode.RecentlyAdded,MaximumResults:100),token);resolved.AddRange(libraryItems.Select(item=>new CollectionResolvedItem(CollectionTargetType.LibraryItem,item.Id,item.Title,item.LaunchTarget is not null,item)));
        IReadOnlyList<GalleryMediaItem> galleryItems=await gallery.QueryAsync(new(FavoritesOnly:favorites,IncludeHidden:false,MaximumResults:100,SortMode:GallerySortMode.RecentlyAdded),token);resolved.AddRange(galleryItems.Select(item=>new CollectionResolvedItem(CollectionTargetType.GalleryMediaItem,item.Id,item.FileName,item.Available,GalleryMediaItem:item)));
        if(!favorites)resolved=resolved.OrderByDescending(item=>item.LibraryItem?.AddedAtUtc??item.GalleryMediaItem?.AddedAtUtc??DateTime.MinValue).Take(100).ToList();SetShelfItems(resolved.Select((item,index)=>new ShelfItemViewModel(item,index)));
    }
    public async Task OpenAlbumAsync(GalleryAlbum album,CancellationToken token=default){_openShelf=null;ShelfTitle=album.Name;ShelfDescription=album.Description??"Gallery album";CanManageShelf=false;IReadOnlyList<GalleryMediaItem> media=await gallery.QueryAsync(new(AlbumId:album.Id,IncludeHidden:false,MaximumResults:500),token);SetShelfItems(media.Select((item,index)=>new ShelfItemViewModel(new(CollectionTargetType.GalleryMediaItem,item.Id,item.FileName,item.Available,GalleryMediaItem:item),index)));}

    public void FilterShelf(string? text){string query=text?.Trim()??string.Empty;ShelfItems.ReplaceWith(string.IsNullOrEmpty(query)?_allShelfItems:_allShelfItems.Where(item=>item.DisplayName.Contains(query,StringComparison.CurrentCultureIgnoreCase)||item.TypeLabel.Contains(query,StringComparison.CurrentCultureIgnoreCase)));}
    public async Task RemoveShelfItemAsync(ShelfItemViewModel item,CancellationToken token=default){if(_openShelf?.CollectionType!=CollectionType.Manual)return;await collections.RemoveItemAsync(_openShelf.Id,item.Item.TargetType,item.Item.TargetId,token);await OpenShelfAsync(_openShelf,token);await RefreshCollectionsAsync(token);}
    public async Task MoveShelfItemAsync(ShelfItemViewModel item,int offset,CancellationToken token=default){if(_openShelf?.CollectionType!=CollectionType.Manual)return;List<CollectionItem> items=(await collections.GetItemsAsync(_openShelf.Id,token)).OrderBy(value=>value.SortOrder).ToList();int current=items.FindIndex(value=>value.TargetType==item.Item.TargetType&&value.TargetId==item.Item.TargetId);if(current<0)return;int target=Math.Clamp(current+offset,0,items.Count-1);if(target==current)return;(items[current],items[target])=(items[target],items[current]);await collections.ReorderItemsAsync(_openShelf.Id,items.Select(value=>(value.TargetType,value.TargetId)).ToArray(),token);await OpenShelfAsync(_openShelf,token);}
    private void SetShelfItems(IEnumerable<ShelfItemViewModel> items){_allShelfItems=items.ToArray();ShelfItems.ReplaceWith(_allShelfItems);}
}
public sealed class CollectionCardViewModel
{
    public CollectionCardViewModel(CollectionDefinition definition,int itemCount,CollectionMosaicResult mosaic){Definition=definition;ItemCount=itemCount;MosaicResult=mosaic;if(mosaic.CachePath is { } path&&File.Exists(path))Mosaic=new BitmapImage(new Uri(path));}
    public CollectionDefinition Definition{get;}public string Id=>Definition.Id;public string Name=>Definition.Name;public string Description=>Definition.Description??(ItemCount==0?"Empty Shelf":"Mixed-content Shelf");public int ItemCount{get;}public string ItemCountLabel=>ItemCount==1?"1 item":$"{ItemCount} items";public CollectionMosaicResult MosaicResult{get;}public BitmapImage? Mosaic{get;}public Visibility MosaicVisibility=>Mosaic is null?Visibility.Collapsed:Visibility.Visible;public Visibility FallbackVisibility=>Mosaic is null?Visibility.Visible:Visibility.Collapsed;public string AccessibilityName=>$"{Name} Shelf, {ItemCountLabel}";
}
public sealed class ShelfItemViewModel(CollectionResolvedItem item,int sortOrder)
{
    public CollectionResolvedItem Item{get;}=item;public int SortOrder{get;}=sortOrder;public string DisplayName=>Item.DisplayName;public bool Available=>Item.Available;public string AvailabilityLabel=>Available?"Available":"Reference unavailable";public string TypeLabel=>Item.TargetType switch{CollectionTargetType.LibraryItem=>Item.LibraryItem?.ContentType==ContentType.Application?"App":"Game",CollectionTargetType.GalleryMediaItem=>Item.GalleryMediaItem?.MediaType==GalleryMediaType.Video?"Video":"Photo",CollectionTargetType.GalleryAlbum=>"Album",CollectionTargetType.Collection=>"Nested Shelf",_=>Item.TargetType.ToString()};
}
internal static class ObservableCollectionExtensions{public static void ReplaceWith<T>(this ObservableCollection<T> collection,IEnumerable<T> values){collection.Clear();foreach(T value in values)collection.Add(value);}}
