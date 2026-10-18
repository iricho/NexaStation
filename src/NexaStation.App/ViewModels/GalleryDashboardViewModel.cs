using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexaStation.Core;
using NexaStation.Domain;
using Microsoft.UI.Xaml;

namespace NexaStation.App.ViewModels;

public sealed class GalleryDashboardViewModel(IGalleryPersistence persistence,IGalleryThumbnailService thumbnails) : ObservableObject,IDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly List<GalleryRowViewModel> _pendingRows = [];
    private bool _disposed;
    private string? _loadError;
    private GalleryViewKind _view=GalleryViewKind.Home;private GalleryGridSize _grid=GalleryGridSize.Medium;private GallerySortMode _sort=GallerySortMode.NewestCaptured;private string? _sourceId;private string? _albumId;private string? _search;private bool _loading;private CancellationTokenSource? _searchDebounce;private CancellationTokenSource? _stateDebounce;
    public event EventHandler<string?>? FocusRestoreRequested;public ObservableCollection<GalleryRowViewModel> Rows{get;}=[];public ObservableCollection<GalleryCardViewModel> HomePreview{get;}=[];public ObservableCollection<GallerySource> Sources{get;}=[];public ObservableCollection<GalleryAlbum> Albums{get;}=[];public bool IsLoading{get=>_loading;private set=>SetProperty(ref _loading,value);}public bool IsEmpty=>Rows.Sum(row=>row.Items.Count)==0;public Visibility EmptyVisibility=>!IsLoading&&(IsEmpty||_loadError is not null)?Visibility.Visible:Visibility.Collapsed;public Visibility HomePreviewVisibility=>HomePreview.Count>0?Visibility.Visible:Visibility.Collapsed;public string EmptyMessage=>_loadError??(Sources.Count==0?"Add a photo, screenshot, recording, or video folder.":"No media matches this Gallery view.");public string EmptyTitle=>_loadError is not null?"Gallery unavailable":Sources.Count==0?"Your Gallery is empty":"Nothing matches this view";public GalleryViewKind CurrentView=>_view;public GalleryGridSize GridSize=>_grid;public GallerySortMode SortMode=>_sort;

    public async Task InitializeAsync(CancellationToken cancellationToken=default){GalleryApplicationState? state=await persistence.GetApplicationStateAsync(cancellationToken);if(state is not null){_view=state.CurrentView;_grid=state.GridSize;_sort=state.SortMode;_sourceId=state.SelectedGallerySourceId;_albumId=state.SelectedAlbumId;}await RefreshAsync(cancellationToken:cancellationToken);}
    public async Task RefreshAsync(string? search=null,CancellationToken cancellationToken=default)
    {
        if (_disposed) return;
        await _refreshGate.WaitAsync(cancellationToken);
        try{if(_disposed)return;IsLoading=true;_loadError=null;OnPropertyChanged(nameof(EmptyVisibility));if(search is not null)_search=search;GallerySource[] sources=(await persistence.GetSourcesAsync(cancellationToken)).ToArray();GalleryAlbum[] albums=(await persistence.GetAlbumsAsync(cancellationToken)).ToArray();Sources.Clear();foreach(GallerySource source in sources)Sources.Add(source);Albums.Clear();foreach(GalleryAlbum album in albums)Albums.Add(album);HomePreview.Clear();foreach(GalleryMediaItem item in await persistence.QueryAsync(new(HomePreviewOnly:true,IncludePrivate:false,MaximumResults:5),cancellationToken))HomePreview.Add(new(item,thumbnails,176,94));_pendingRows.Clear();
            if(_view==GalleryViewKind.Home)await BuildHomeAsync(cancellationToken);else if(_view==GalleryViewKind.Timeline)await BuildTimelineAsync(cancellationToken);else if(_view==GalleryViewKind.Folders)await BuildFoldersAsync(cancellationToken);else if(_view==GalleryViewKind.Albums)await BuildAlbumsAsync(cancellationToken);else AddRow("Gallery",await QueryAsync(cancellationToken));cancellationToken.ThrowIfCancellationRequested();if(_disposed)return;Rows.Clear();foreach(GalleryRowViewModel row in _pendingRows)Rows.Add(row);OnPropertyChanged(nameof(IsEmpty));OnPropertyChanged(nameof(EmptyVisibility));OnPropertyChanged(nameof(HomePreviewVisibility));OnPropertyChanged(nameof(EmptyTitle));OnPropertyChanged(nameof(EmptyMessage));}
        catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception){_loadError="Your media could not be loaded. Choose a Gallery view to try again.";}
        finally{IsLoading=false;OnPropertyChanged(nameof(EmptyVisibility));OnPropertyChanged(nameof(EmptyTitle));OnPropertyChanged(nameof(EmptyMessage));_refreshGate.Release();}
    }
    public async Task ToggleFavoriteAsync(GalleryCardViewModel card,CancellationToken cancellationToken=default){bool value=!card.IsFavorite;await persistence.SetFavoriteAsync(card.Id,value,cancellationToken);card.SetFavorite(value);await RefreshAsync(cancellationToken:cancellationToken);FocusRestoreRequested?.Invoke(this,card.Id);}
    public void Search(string text){if(_disposed)return;_searchDebounce?.Cancel();_searchDebounce?.Dispose();_searchDebounce=new();CancellationToken token=_searchDebounce.Token;_=DebouncedSearchAsync(text,token);}
    private async Task DebouncedSearchAsync(string text,CancellationToken token)
    {
        try{await Task.Delay(300,token);await RefreshAsync(text,token);}
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
    }
    public async Task SetViewAsync(GalleryViewKind view,CancellationToken cancellationToken=default){_view=view;_sourceId=null;_albumId=null;ScheduleState();OnPropertyChanged(nameof(CurrentView));await RefreshAsync(cancellationToken:cancellationToken);}
    public async Task SetSourceAsync(string? sourceId,CancellationToken cancellationToken=default){_sourceId=sourceId;_view=GalleryViewKind.Grid;ScheduleState();await RefreshAsync(cancellationToken:cancellationToken);}
    public async Task SetAlbumAsync(string? albumId,CancellationToken cancellationToken=default){_albumId=albumId;_view=GalleryViewKind.Albums;ScheduleState();await RefreshAsync(cancellationToken:cancellationToken);}
    public async Task SetGridAsync(GalleryGridSize size,CancellationToken cancellationToken=default){_grid=size;ScheduleState();OnPropertyChanged(nameof(GridSize));await RefreshAsync(cancellationToken:cancellationToken);}
    public async Task SetSortAsync(GallerySortMode sort,CancellationToken cancellationToken=default){_sort=sort;ScheduleState();OnPropertyChanged(nameof(SortMode));await RefreshAsync(cancellationToken:cancellationToken);}
    public Task<IReadOnlyList<GalleryMediaItem>> CurrentItemsAsync(CancellationToken token=default)=>QueryAsync(token);
    private async Task BuildHomeAsync(CancellationToken token){AddRow("Recent",await persistence.QueryAsync(new(SearchText:_search,SortMode:GallerySortMode.RecentlyOpened,MaximumResults:12),token));AddRow("Favorites",await persistence.QueryAsync(new(SearchText:_search,FavoritesOnly:true,MaximumResults:12),token));foreach(GallerySourceKind kind in Enum.GetValues<GallerySourceKind>()){IReadOnlyList<GalleryMediaItem> values=await persistence.QueryAsync(new(SearchText:_search,SourceKind:kind,MaximumResults:12),token);AddRow(kind.ToString(),values);}foreach(GalleryAlbum album in Albums){IReadOnlyList<GalleryMediaItem> values=await persistence.QueryAsync(new(SearchText:_search,AlbumId:album.Id,MaximumResults:12),token);AddRow(album.Name,values);}}
    private async Task BuildTimelineAsync(CancellationToken token){IReadOnlyList<GalleryMediaItem> items=await QueryAsync(token);DateTime now=DateTime.UtcNow.Date;foreach(IGrouping<string,GalleryMediaItem> group in items.GroupBy(item=>TimelineLabel((item.CapturedAtUtc??item.ModifiedAtUtc).Date,now)))AddRow(group.Key,group);}
    private async Task BuildFoldersAsync(CancellationToken token){IReadOnlyList<GalleryMediaItem> items=await QueryAsync(token);foreach(IGrouping<string,GalleryMediaItem> group in items.GroupBy(item=>item.ParentFolder).OrderBy(group=>group.Key,StringComparer.OrdinalIgnoreCase))AddRow(Path.GetFileName(group.Key) is {Length:>0} name?name:group.Key,group);}
    private async Task BuildAlbumsAsync(CancellationToken token){foreach(GalleryAlbum album in Albums){if(_albumId is not null&&album.Id!=_albumId)continue;AddRow(album.Name,await persistence.QueryAsync(new(SearchText:_search,AlbumId:album.Id,SortMode:_sort,MaximumResults:1000),token));}}
    private Task<IReadOnlyList<GalleryMediaItem>> QueryAsync(CancellationToken token)=>persistence.QueryAsync(new(SearchText:_search,SourceId:_sourceId,AlbumId:_albumId,SortMode:_sort,MaximumResults:5000),token);
    private void AddRow(string title,IEnumerable<GalleryMediaItem> items){(double width,double height)=_grid switch{GalleryGridSize.Small=>(160,110),GalleryGridSize.Large=>(300,205),_=>(220,150)};GalleryCardViewModel[] cards=items.Select(item=>new GalleryCardViewModel(item,thumbnails,width,height)).ToArray();if(cards.Length>0)_pendingRows.Add(new(title,cards));}
    private static string TimelineLabel(DateTime date,DateTime now){if(date==now)return "Today";if(date==now.AddDays(-1))return "Yesterday";if(date>=now.AddDays(-7))return "This Week";if(date.Year==now.Year&&date.Month==now.Month)return "This Month";return date.Year==now.Year?"Earlier":date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    private void ScheduleState(){_stateDebounce?.Cancel();_stateDebounce?.Dispose();_stateDebounce=new();CancellationToken token=_stateDebounce.Token;_=Task.Run(async()=>{try{await Task.Delay(350,token);await persistence.SaveApplicationStateAsync(new(_view,null,_sourceId,_albumId,_grid,_sort,_search,GalleryZoomMode.Fit,5),token);}catch(OperationCanceledException){}},token);}
    public void Dispose(){_disposed=true;_searchDebounce?.Cancel();_searchDebounce?.Dispose();_stateDebounce?.Cancel();_stateDebounce?.Dispose();}
}
