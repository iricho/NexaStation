using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.SystemIntegration.Services;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using NexaStation.Presentation.Dashboard;
using Microsoft.UI.Xaml.Controls;
using NexaStation.Profiles.Restrictions;
using NexaStation.Profiles.Sessions;

namespace NexaStation.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly ILibraryRepository _library;
    private readonly IApplicationStateService _state;
    private readonly LaunchCoordinator _launcher;
    private readonly ICurrentActivityService _activity;
    private readonly IDashboardStateService _dashboardState;
    private readonly ISelectionStateService _selectionState;
    private readonly IInterfaceLayoutService _layouts;
    private readonly IDashboardConfigurationService _dashboardConfiguration;
    private readonly IPinService _pins;
    private readonly ICollectionService _collections;
    private readonly ISmartCollectionQueryService _smartCollections;
    private readonly IDashboardSemanticService _semantics;
    private readonly IHeroSelectionService _heroSelection;
    private readonly IArtworkResolver _artworkResolver;
    private readonly IContentCardPresentationResolver _cardPresentationResolver;
    private readonly IApplicationVisualResolver _applicationVisualResolver;
    private readonly IGuestModeService? _guestMode;
    private readonly IFrontendProfileStore? _frontendProfiles;
    private readonly IContentRestrictionStore? _contentRestrictions;
    private readonly IContentRestrictionPolicy? _restrictionPolicy;
    private DashboardSectionViewModel _selectedSection;
    private bool _isLaunching;
    private bool _isLoading;
    private string? _lastError;
    private string? _statusMessage;
    private CancellationTokenSource? _searchDebounce;
    private readonly LibraryBrowseState _browse = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IReadOnlyList<LibraryItem> _librarySnapshot = [];
    private CardViewModel[] _currentSectionItems = [];
    private bool _disposed;
    private string? _libraryLoadError;
    private CardViewModel? _launchActivityCard;
    private CardViewModel? _featuredItem;
    private CardViewModel[] _heroItems=[];
    private int _selectedHeroIndex;
    private int _heroSelectionVersion;
    private string _featuredRule="NexaStationFallback";
    private string _continueSectionTitle="Games";
    private string _applicationSectionTitle="Applications";
    private IReadOnlyList<CardViewModel> _continueItems=[];
    private IReadOnlyList<CardViewModel> _recentAppItems=[];
    private IReadOnlyList<CardViewModel> _favoriteItems=[];
    private IReadOnlyList<CardViewModel> _mediaItems=[];
    private IReadOnlyList<CardViewModel> _recentItems=[];
    private IReadOnlyList<CardViewModel> _installedAppItems=[];
    private string _libraryPresentationMode="Adaptive Grid";
    private string _gamesPresentationMode="Wide Grid";
    private readonly SynchronizationContext? _uiContext=SynchronizationContext.Current;
    private CancellationTokenSource? _notificationExpiry;
    private DashboardNotificationViewModel? _currentNotification;
    private CancellationTokenSource? _backgroundPreviewDelay;
    private BitmapImage? _backgroundPreview;

    public DashboardViewModel(ILibraryRepository library,IApplicationStateService state,LaunchCoordinator launcher,IDashboardStateService dashboardState,ISelectionStateService selectionState,ICurrentActivityService activity,IInterfaceLayoutService layouts,IDashboardConfigurationService dashboardConfiguration,IPinService pins,ICollectionService collections,ISmartCollectionQueryService smartCollections,IDashboardSemanticService semantics,IHeroSelectionService heroSelection,IArtworkResolver artworkResolver,IContentCardPresentationResolver cardPresentationResolver,IApplicationVisualResolver applicationVisualResolver,IGuestModeService? guestMode=null,IFrontendProfileStore? frontendProfiles=null,IContentRestrictionStore? contentRestrictions=null,IContentRestrictionPolicy? restrictionPolicy=null)
    {
        _library=library;_state=state;_launcher=launcher;_dashboardState=dashboardState;_selectionState=selectionState;_activity=activity;_layouts=layouts;_dashboardConfiguration=dashboardConfiguration;_pins=pins;_collections=collections;_smartCollections=smartCollections;_semantics=semantics;_heroSelection=heroSelection;_artworkResolver=artworkResolver;_cardPresentationResolver=cardPresentationResolver;_applicationVisualResolver=applicationVisualResolver;_guestMode=guestMode;_frontendProfiles=frontendProfiles;_contentRestrictions=contentRestrictions;_restrictionPolicy=restrictionPolicy;_launcher.StateChanged+=OnLaunchStateChanged;
        Sections=new ObservableCollection<DashboardSectionViewModel>(CreateShell());
        _selectedSection=Sections[0];_selectedSection.IsSelected=true;
    }

    public event EventHandler<string?>? FocusRestoreRequested;
    public ObservableCollection<DashboardSectionViewModel> Sections { get; }
    public DashboardSectionViewModel SelectedSection { get=>_selectedSection;private set{if(SetProperty(ref _selectedSection,value)){OnPropertyChanged(nameof(Rows));OnPropertyChanged(nameof(PrimaryRowTitle));OnPropertyChanged(nameof(PrimaryItems));NotifySectionItemsChanged();OnPropertyChanged(nameof(CurrentSectionTitle));OnPropertyChanged(nameof(SectionDescription));NotifyHomeSurfaceChanged();}} }
    public IReadOnlyList<DashboardRowViewModel> Rows=>SelectedSection.Rows;
    public string PrimaryRowTitle=>Rows.Count>0?Rows[0].Title:"Nothing here yet";
    public IReadOnlyList<CardViewModel> PrimaryItems=>Rows.Count>0?Rows[0].Items:[];
    public IReadOnlyList<CardViewModel> CurrentSectionItems=>_currentSectionItems;
    public string CurrentSectionTitle=>SelectedSection.Title;
    public string SectionDescription=>SelectedSection.Section==NavigationSection.Library?"All your games and apps. Find a favorite or choose what to play next.":"Your games and apps, ready when you are.";
    public CardViewModel? FeaturedItem=>_featuredItem;
    public IReadOnlyList<CardViewModel> HeroItems=>_heroItems;
    public int HeroCount=>HeroItems.Count;
    public int SelectedHeroIndex=>_selectedHeroIndex;
    public string FeaturedRule=>_featuredRule;
    public BitmapImage? FeaturedArtwork=>FeaturedItem?.BackgroundArtwork;
    public BitmapImage? DashboardBackgroundArtwork=>_backgroundPreview??FeaturedArtwork;
    public string FeaturedTitle=>FeaturedItem?.Title??"Your world, ready to play";
    public string FeaturedDescription=>FeaturedItem is null?"Import games and applications to build your personal living-room dashboard.":string.IsNullOrWhiteSpace(FeaturedItem.Description)?FeaturedItem.Subtitle:FeaturedItem.Description;
    public string HeroPrimaryActionText=>FeaturedItem is null?"Browse Games":"Play Now";
    public string HeroSecondaryActionText=>FeaturedItem is null?"Open Library":"Details";
    public string ContinueSectionTitle=>_continueSectionTitle;
    public string ApplicationSectionTitle=>_applicationSectionTitle;
    public IReadOnlyList<CardViewModel> ContinueItems=>_continueItems;
    public IReadOnlyList<CardViewModel> CrossbarRecentItems { get; private set; } = [];
    public IReadOnlyList<CardViewModel> RecentAppItems=>_recentAppItems;
    public IReadOnlyList<CardViewModel> FavoriteItems=>_favoriteItems;
    public IReadOnlyList<CardViewModel> MediaItems=>_mediaItems;
    public IReadOnlyList<CardViewModel> RecentItems=>_recentItems;
    public IReadOnlyList<CardViewModel> InstalledAppItems=>_installedAppItems;
    public Visibility InstalledAppsEmptyVisibility=>InstalledAppItems.Count==0?Visibility.Visible:Visibility.Collapsed;
    public Visibility ContinueSectionVisibility=>ContinueItems.Count>0?Visibility.Visible:Visibility.Collapsed;
    public Visibility ApplicationSectionVisibility=>RecentAppItems.Count>0?Visibility.Visible:Visibility.Collapsed;
    public Visibility MediaSectionVisibility=>MediaItems.Count>0?Visibility.Visible:Visibility.Collapsed;
    public Visibility FavoriteSectionVisibility=>FavoriteItems.Count>0?Visibility.Visible:Visibility.Collapsed;
    public DashboardNotificationViewModel? CurrentNotification=>_currentNotification;
    public Visibility NotificationVisibility=>CurrentNotification is null?Visibility.Collapsed:Visibility.Visible;
    public IReadOnlyList<DashboardRowViewModel> CustomHomeRows=>HomeRows.Where(row=>row.RowKey is not ("pinned" or "continue" or "apps" or "favorites" or "media" or "added" or "games")).ToArray();
    public bool IsLaunching{get=>_isLaunching;private set=>SetProperty(ref _isLaunching,value);}
    public bool IsLoading{get=>_isLoading;private set{if(SetProperty(ref _isLoading,value))NotifyLibraryFeedbackChanged();}}
    public Visibility LibraryLoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SectionEmptyVisibility => !IsLoading && _libraryLoadError is null && CurrentSectionItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SectionItemsVisibility => CurrentSectionItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LibraryErrorVisibility => _libraryLoadError is null ? Visibility.Collapsed : Visibility.Visible;
    public string LibraryLoadError => _libraryLoadError ?? string.Empty;
    public string SectionEmptyTitle => SelectedSection.Section is NavigationSection.Library or NavigationSection.Games && _browse.HasFilters ? "No matches found" : $"Your {CurrentSectionTitle.ToLowerInvariant()} is empty";
    public string SectionEmptyMessage => SelectedSection.Section is NavigationSection.Library or NavigationSection.Games && _browse.HasFilters ? "Try another search or clear the filters to see your library." : "Add games or discover apps from Settings to get started.";
    public string LibraryResultsSummary => IsLoading ? "Loading your library…" : $"{CurrentSectionItems.Count:N0} {(CurrentSectionItems.Count == 1 ? "item" : "items")}" + (_browse.HasFilters ? " · Filtered" : string.Empty);
    public bool CanClearLibraryFilters => _browse.HasFilters || _browse.SortMode != LibrarySortMode.Title;
    public string LibraryView => _browse.View;
    public string LibrarySearchText => _browse.SearchText;

    private static readonly string[] AllPlatforms = ["All Platforms"];
    private static readonly string[] AllGenres = ["All Genres"];
    private static readonly string[] BrowseViews = ["All", "Favorites", "Recently Used", "Recently Added", "Valid targets", "Missing targets"];
    private static readonly string[] AllBrowseViews = ["All", "Games", "Applications", "Favorites", "Recently Added", "Recently Used", "Native", "Emulated", "Hidden", "Valid targets", "Missing targets"];
    public IReadOnlyList<string> LibraryViewOptions { get; } = AllBrowseViews;
    private CardViewModel? _librarySelection;
    public CardViewModel? LibrarySelection
    {
        get => _librarySelection;
        private set
        {
            if(ReferenceEquals(_librarySelection,value))return;
            if(_librarySelection is not null)_librarySelection.IsLibrarySelected=false;
            SetProperty(ref _librarySelection,value);
            if(value is not null)value.IsLibrarySelected=true;
            OnPropertyChanged(nameof(LibrarySelectionVisibility));
        }
    }
    public Visibility LibrarySelectionVisibility => LibrarySelection is null ? Visibility.Collapsed : Visibility.Visible;
    public void SelectLibraryCard(CardViewModel card) => LibrarySelection = card;
    private IEnumerable<LibraryItem> BrowseItems => _librarySnapshot.Where(item => !item.Hidden && (SelectedSection.Section != NavigationSection.Games || item.ContentType == ContentType.Game));
    public IReadOnlyList<string> LibraryPlatforms => AllPlatforms.Concat(BrowseItems.Select(LibraryBrowseState.SourceLabel).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)).ToArray();
    public IReadOnlyList<string> LibraryGenres => AllGenres.Concat(BrowseItems.Select(item => item.PrimaryCategoryName).OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)).ToArray();
    public string LibraryPlatform => _browse.Platform;
    public string LibraryGenre => _browse.Genre;
    public string HomeLibrarySummary => $"{_librarySnapshot.Count(item => !item.Hidden):N0} games and apps";
    public string HomeFavoritesSummary => $"{_librarySnapshot.Count(item => !item.Hidden && item.Favorite):N0} favorites";
    public string HomeResumeTitle => _librarySnapshot.Where(item => !item.Hidden && item.ContentType == ContentType.Game && item.LastLaunchedAtUtc is not null).OrderByDescending(item => item.LastLaunchedAtUtc).FirstOrDefault()?.Title ?? "Choose a game";
    public IReadOnlyList<LibraryFilterViewModel> LibrarySourceFilters => LibraryPlatforms.Select(source => new LibraryFilterViewModel(source == "All Platforms" ? "All" : source, source, source == "All Platforms" ? BrowseItems.Count() : BrowseItems.Count(item => LibraryBrowseState.SourceLabel(item).Equals(source, StringComparison.OrdinalIgnoreCase)), source == _browse.Platform)).ToArray();
    public IReadOnlyList<LibraryFilterViewModel> LibraryGenreFilters => LibraryGenres.Skip(1).Select(genre => new LibraryFilterViewModel(genre, genre, BrowseItems.Count(item => string.Equals(item.PrimaryCategoryName,genre,StringComparison.OrdinalIgnoreCase)), genre == _browse.Genre)).ToArray();
    public IReadOnlyList<LibraryFilterViewModel> LibraryViewFilters => BrowseViews.Select(view => new LibraryFilterViewModel(view switch { "All" => SelectedSection.Section == NavigationSection.Games ? "All Games" : "All Games & Apps", "Recently Used" => "Recently Played", "Valid targets" => "Ready to launch", "Missing targets" => "Needs attention", _ => view }, view, new LibraryBrowseState { View = view }.Apply(BrowseItems.ToArray()).Count, view == _browse.View)).ToArray();
    public void SetLibraryPlatform(string value) { if(_browse.Platform == value)return; _browse.Platform=value; RebuildLibrary(); }
    public void SetLibraryGenre(string value) { if(_browse.Genre == value)return; _browse.Genre=value; RebuildLibrary(); }

    private void NotifyLibraryFeedbackChanged()
    {
        OnPropertyChanged(nameof(LibraryLoadingVisibility));OnPropertyChanged(nameof(SectionEmptyVisibility));
        OnPropertyChanged(nameof(SectionItemsVisibility));
        OnPropertyChanged(nameof(LibraryErrorVisibility));OnPropertyChanged(nameof(LibraryLoadError));
        OnPropertyChanged(nameof(SectionEmptyTitle));OnPropertyChanged(nameof(SectionEmptyMessage));
        OnPropertyChanged(nameof(LibraryResultsSummary));OnPropertyChanged(nameof(CanClearLibraryFilters));
        OnPropertyChanged(nameof(LibraryView));OnPropertyChanged(nameof(LibrarySearchText));
        OnPropertyChanged(nameof(LibraryPlatforms));OnPropertyChanged(nameof(LibraryGenres));
        OnPropertyChanged(nameof(LibraryPlatform));OnPropertyChanged(nameof(LibraryGenre));
        OnPropertyChanged(nameof(LibrarySourceFilters));OnPropertyChanged(nameof(LibraryGenreFilters));OnPropertyChanged(nameof(LibraryViewFilters));
        OnPropertyChanged(nameof(HomeLibrarySummary));OnPropertyChanged(nameof(HomeFavoritesSummary));OnPropertyChanged(nameof(HomeResumeTitle));
    }

    private void NotifySectionItemsChanged()
    {
        _currentSectionItems = Rows.SelectMany(row => row.Items).DistinctBy(card => card.Id).ToArray();
        OnPropertyChanged(nameof(Rows));OnPropertyChanged(nameof(PrimaryItems));OnPropertyChanged(nameof(PrimaryRowTitle));
        LibrarySelection = _currentSectionItems.FirstOrDefault(card => card.Id == LibrarySelection?.Id) ?? (_currentSectionItems.Length>0?_currentSectionItems[0]:null);
        OnPropertyChanged(nameof(CurrentSectionItems));NotifyLibraryFeedbackChanged();
    }
    public string? LastError{get=>_lastError;private set{if(SetProperty(ref _lastError,value))OnPropertyChanged(nameof(IsErrorOpen));}}
    public bool IsErrorOpen=>!string.IsNullOrWhiteSpace(LastError);
    public string? StatusMessage{get=>_statusMessage;private set{if(SetProperty(ref _statusMessage,value))OnPropertyChanged(nameof(IsStatusOpen));}}
    public bool IsStatusOpen=>!string.IsNullOrWhiteSpace(StatusMessage);

    public async Task InitializeAsync()
    {
        string? saved=await _state.GetAsync("CurrentSection");
        NavigationSection target=Enum.TryParse(saved,out NavigationSection parsed)?parsed:NavigationSection.Home;
        await RefreshAsync();
        SelectSection(Sections.FirstOrDefault(s=>s.Section==target)??Sections[0]);
        string? selected=await _state.GetAsync($"SelectedItem.{SelectedSection.Section}");
        if(selected is not null&&Rows.SelectMany(row=>row.Items).Any(card=>card.Id==selected))_dashboardState.SetSelectedItem(SelectedSection.Section,selected);
    }

    public async Task RefreshAsync(string? search=null)
    {
        if (_disposed) return;
        if (search is not null) _browse.SearchText = search;
        await _refreshGate.WaitAsync();
        try
        {
            if (_disposed) return;
            IsLoading=true;
            List<LibraryItem> loaded = [];
            const int pageSize = 1000;
            for (int offset = 0; ; offset += pageSize)
            {
                IReadOnlyList<LibraryItem> page = await _library.QueryAsync(new LibraryQuery(IncludeHidden:true, MaximumResults:pageSize, Offset:offset));
                if (_disposed) return;
                loaded.AddRange(page);
                if (page.Count < pageSize) break;
            }
            IReadOnlyList<LibraryItem> all=await ApplyGuestRestrictionsAsync(loaded.DistinctBy(item => item.Id).ToArray());
            if (_disposed) return;
            _librarySnapshot=all;
            CrossbarRecentItems=all.Where(item=>!item.Hidden && item.ContentType is ContentType.Game or ContentType.Application)
                .OrderByDescending(item=>item.LastLaunchedAtUtc).ThenBy(item=>item.SortTitle,StringComparer.CurrentCultureIgnoreCase)
                .Take(24).Select(item=>CreateCard(item,CardStyle.VerticalCover,NavigationSection.Home,"crossbar-recent")).ToArray();
            DashboardSemanticContent semantic=_semantics.Build(all);
            IReadOnlyList<PinnedItem> homePins=await _pins.GetAsync(PinLocation.Home);
            string? userFeatured=await _state.GetAsync("Dashboard.FeaturedItemId");
            IReadOnlyList<HeroSelectionResult> heroes=_heroSelection.SelectHeroes(new(all,homePins.Where(pin=>pin.TargetType==PinTargetType.LibraryItem).OrderBy(pin=>pin.SortOrder).Select(pin=>pin.TargetId).ToArray(),userFeatured));
            _featuredRule=heroes[0].Rule;
            _heroItems=heroes.Where(result=>result.Item is not null).Select(result=>new CardViewModel(result.Item!,Glyph(result.Item!),isFeatured:true,width:1500,height:306,artworkResolver:_artworkResolver,presentationResolver:_cardPresentationResolver,applicationVisualResolver:_applicationVisualResolver,currentPage:NavigationSection.Home,dashboardRow:"hero")).ToArray();
            _selectedHeroIndex=0;_featuredItem=_heroItems.Length>0?_heroItems[0]:null;
            if(_featuredItem is not null)await _featuredItem.EnsureBackgroundArtworkAsync();
            _continueSectionTitle="Continue Playing";
            _applicationSectionTitle="Recently Used Apps";
            _continueItems=CreateCards(semantic.ContinuePlaying,CardStyle.Wide,8);
            _recentAppItems=CreateCards(semantic.RecentlyUsedApplications,CardStyle.Square,5);
            _favoriteItems=CreateCards(semantic.Favorites,CardStyle.Square,3);
            _mediaItems=CreateCards(semantic.MediaServices,CardStyle.Square,5);
            _recentItems=CreateCards(semantic.RecentlyAdded,CardStyle.Square,3);
            _installedAppItems=all.Where(item=>!item.Hidden&&item.ContentType==ContentType.Application).OrderByDescending(item=>item.LastLaunchedAtUtc).ThenBy(item=>item.Title,StringComparer.CurrentCultureIgnoreCase).Select(item=>CreateCard(item,CardStyle.Square,NavigationSection.Apps,"installed")).ToArray();
            await _layouts.GetAvailableAsync();DashboardConfiguration dashboard=await _dashboardConfiguration.GetOrCreateAsync(_layouts.CurrentLayoutId);IReadOnlyList<DashboardRowConfiguration> configuredRows=await _dashboardConfiguration.GetRowsAsync(dashboard.Id);
            foreach(DashboardSectionViewModel section in Sections)if(section.Section==NavigationSection.Home)await RebuildHomeAsync(section,all,configuredRows);else Rebuild(section,all);
            _libraryLoadError=null;NotifySectionItemsChanged();OnPropertyChanged(nameof(InstalledAppItems));NotifyHomeSurfaceChanged();
        }
        catch(Exception ex){_libraryLoadError=$"The local library could not be loaded: {ex.Message}";NotifyLibraryFeedbackChanged();}
        finally{IsLoading=false;_refreshGate.Release();}
    }

    private async Task<IReadOnlyList<LibraryItem>> ApplyGuestRestrictionsAsync(LibraryItem[] items)
    {
        if (_guestMode?.IsGuest != true || _frontendProfiles is null || _contentRestrictions is null || _restrictionPolicy is null) return items;
        GuestModeSettings settings = await _frontendProfiles.GetGuestSettingsAsync();
        List<LibraryItem> visible = new(items.Length);
        foreach (LibraryItem item in items)
        {
            bool typeAllowed = item.ContentType switch
            {
                ContentType.Game => settings.AllowGames,
                ContentType.Application => settings.AllowApplications,
                ContentType.Media => settings.AllowMedia,
                _ => false,
            };
            if (!typeAllowed) continue;
            ContentRestrictionState restriction = await _contentRestrictions.GetAsync(item.Id, RestrictedContentKind.LibraryItem);
            RestrictedContentDecision decision = _restrictionPolicy.Evaluate(new(item.Id, RestrictedContentKind.LibraryItem, restriction, item.Title, item.CoverAsset?.LocalPath), true);
            if (decision.Visible) visible.Add(item);
        }
        return visible;
    }

    public void Search(string text)
    {
        if (_disposed) return;
        _browse.SearchText=text;
        _searchDebounce?.Cancel();_searchDebounce?.Dispose();_searchDebounce=new CancellationTokenSource();CancellationToken token=_searchDebounce.Token;
        _=DebouncedSearchAsync(token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token){try{await Task.Delay(200,token);if(!_disposed)RebuildLibrary();}catch(OperationCanceledException){}}

    private void RebuildLibrary()
    {
        Rebuild(Sections.First(section=>section.Section==NavigationSection.Library),_librarySnapshot);
        Rebuild(Sections.First(section=>section.Section==NavigationSection.Games),_librarySnapshot);
        NotifySectionItemsChanged();
    }

    public void ClearLibraryFilters()
    {
        _searchDebounce?.Cancel();_browse.Reset();RebuildLibrary();
    }

    public void Dispose(){_disposed=true;_searchDebounce?.Cancel();_searchDebounce?.Dispose();_notificationExpiry?.Cancel();_notificationExpiry?.Dispose();_backgroundPreviewDelay?.Cancel();_backgroundPreviewDelay?.Dispose();_launcher.StateChanged-=OnLaunchStateChanged;}

    public async Task AddOrUpdateAsync(LibraryItem item,bool update=false){if(update)await _library.UpdateAsync(item);else await _library.AddAsync(item);await RefreshAsync();FocusRestoreRequested?.Invoke(this,item.Id);}
    public void SetLibraryView(string view){if(_browse.View==view)return;_browse.View=view;RebuildLibrary();}
    public void SetLibrarySort(string sort){_browse.SortMode=sort switch{"Recently added"=>LibrarySortMode.RecentlyAdded,"Recently launched"=>LibrarySortMode.RecentlyLaunched,"Most played"=>LibrarySortMode.MostPlayed,_=>LibrarySortMode.Title};RebuildLibrary();}
    public void SetLibraryPresentation(string mode){_libraryPresentationMode=mode;RebuildLibrary();}
    public void SetGamesPresentation(string mode){_gamesPresentationMode=mode;Rebuild(Sections.First(section=>section.Section==NavigationSection.Games),_librarySnapshot);NotifySectionItemsChanged();}
    public async Task SelectHeroAsync(int index)
    {
        if(HeroItems.Count==0)return;index=(index%HeroItems.Count+HeroItems.Count)%HeroItems.Count;if(index==_selectedHeroIndex&&_featuredItem is not null)return;
        int version=++_heroSelectionVersion;_selectedHeroIndex=index;_featuredItem=HeroItems[index];await _featuredItem.EnsureBackgroundArtworkAsync();if(version!=_heroSelectionVersion)return;NotifyHeroChanged();
    }
    public Task MoveHeroAsync(int offset)=>SelectHeroAsync(_selectedHeroIndex+offset);
    public async Task ToggleFavoriteAsync(CardViewModel card){await _library.SetFavoriteAsync(card.Id,!card.Item.Favorite);await RefreshAsync();FocusRestoreRequested?.Invoke(this,card.Id);}
    public async Task SetHiddenAsync(CardViewModel card,bool hidden){await _library.SetHiddenAsync(card.Id,hidden);await RefreshAsync();FocusRestoreRequested?.Invoke(this,card.Id);}
    public async Task DeleteAsync(CardViewModel card){await _library.DeleteAsync(card.Id);await RefreshAsync();FocusRestoreRequested?.Invoke(this,Rows.SelectMany(r=>r.Items).FirstOrDefault()?.Id);}

    public void SelectSection(DashboardSectionViewModel section)
    {
        if(ReferenceEquals(section,SelectedSection))return;SelectedSection.IsSelected=false;section.IsSelected=true;SelectedSection=section;_dashboardState.CurrentSection=section.Section;_selectionState.SelectedSectionId=section.Id;_state.SetDebounced("CurrentSection",section.Section.ToString());
    }
    public void MoveSection(int offset){int current=Sections.IndexOf(SelectedSection);SelectSection(Sections[(current+offset+Sections.Count)%Sections.Count]);}
    public void SelectItem(CardViewModel card,int rowIndex){_dashboardState.CurrentRow=rowIndex;_dashboardState.SetSelectedItem(SelectedSection.Section,card.Id);_selectionState.SelectedItemId=card.Id;_state.SetDebounced($"SelectedItem.{SelectedSection.Section}",card.Id);_activity.SetFocusedItem(card.Id,card.Title,card.Item.LaunchTarget?.ProviderId);}
    public async Task PreviewFocusedArtworkAsync(CardViewModel card)
    {
        _backgroundPreviewDelay?.Cancel();_backgroundPreviewDelay?.Dispose();_backgroundPreviewDelay=new();CancellationToken token=_backgroundPreviewDelay.Token;
        try{await Task.Delay(320,token);await card.EnsureBackgroundArtworkAsync(token);if(token.IsCancellationRequested)return;_backgroundPreview=card.BackgroundArtwork;OnPropertyChanged(nameof(DashboardBackgroundArtwork));}catch(OperationCanceledException){}
    }
    public void PauseBackgroundPresentation(){_backgroundPreviewDelay?.Cancel();_backgroundPreview=null;OnPropertyChanged(nameof(DashboardBackgroundArtwork));}

    public async Task RefreshArtworkAsync(IEnumerable<string> libraryItemIds,CancellationToken cancellationToken=default)
    {
        HashSet<string> ids=libraryItemIds.ToHashSet(StringComparer.Ordinal);if(ids.Count==0)return;IEnumerable<CardViewModel> cards=_heroItems.Concat(_continueItems).Concat(_recentAppItems).Concat(_favoriteItems).Concat(_mediaItems).Concat(_recentItems).Concat(_installedAppItems).Concat(Rows.SelectMany(row=>row.Items));CardViewModel[] affected=cards.Where(card=>ids.Contains(card.Id)).Distinct().ToArray();foreach(CardViewModel card in affected){cancellationToken.ThrowIfCancellationRequested();await card.EnsureArtworkAsync(cancellationToken);if(card.IsFeatured)await card.EnsureBackgroundArtworkAsync(cancellationToken);}if(_featuredItem is not null&&ids.Contains(_featuredItem.Id))NotifyHeroChanged();
    }
    public async Task LaunchAsync(CardViewModel card){SelectItem(card,FindRow(card));_launchActivityCard=card;LastError=null;IsLaunching=true;_dashboardState.IsLaunchInProgress=true;try{LaunchResult result=await _launcher.LaunchAsync(card.Item);if(!result.Succeeded&&!result.Canceled){LastError=result.ErrorMessage??"The item could not be launched.";PublishNotification(InfoBarSeverity.Error,$"Couldn’t launch {card.Title}",LastError,"Edit launch target","Open details",expires:false);}await RefreshAsync();}finally{_launchActivityCard=null;_activity.ClearTrackedActivity();_activity.SetFocusedItem(card.Id,card.Title,card.Item.LaunchTarget?.ProviderId);IsLaunching=false;_dashboardState.IsLaunchInProgress=false;FocusRestoreRequested?.Invoke(this,card.Id);}}
    private void OnLaunchStateChanged(object? sender,LaunchStateChangedEventArgs e){if(_launchActivityCard is not { } card)return;if(e.Session.State==LaunchState.Running)_activity.SetTrackedActivity(card.Id,card.Title,card.Item.LaunchTarget?.ProviderId,e.Session.StartedAt.UtcDateTime,e.Session.ProcessId is null?"Requested":"ProcessTracked");else if(e.Session.State is LaunchState.Failed or LaunchState.Completed or LaunchState.Idle)_activity.ClearTrackedActivity();}
    public void CloseError(){LastError=null;DismissNotification();}public void CloseStatus(){StatusMessage=null;DismissNotification();}public void ReportStatus(string message){StatusMessage=message;PublishNotification(InfoBarSeverity.Informational,"NexaStation",message);}public void ReportProviderFailure(){PublishNotification(InfoBarSeverity.Error,"Steam refresh could not complete.","Your existing games were not changed.","Retry","Open Diagnostics",expires:false);}public void ReportUnexpectedError(){LastError="NexaStation hit an unexpected interface error. Navigation remains available.";PublishNotification(InfoBarSeverity.Error,"Interface error",LastError,expires:false);}
    public void DismissNotification(){_notificationExpiry?.Cancel();_currentNotification=null;OnPropertyChanged(nameof(CurrentNotification));OnPropertyChanged(nameof(NotificationVisibility));}
    private void PublishNotification(InfoBarSeverity severity,string title,string message,string? primaryAction=null,string? secondaryAction=null,bool expires=true)
    {
        _notificationExpiry?.Cancel();_notificationExpiry?.Dispose();_notificationExpiry=new();CancellationToken token=_notificationExpiry.Token;DateTime? expiration=expires?DateTime.UtcNow.AddSeconds(6):null;_currentNotification=new(Guid.NewGuid().ToString("N"),severity,title,message,primaryAction,secondaryAction,expiration);OnPropertyChanged(nameof(CurrentNotification));OnPropertyChanged(nameof(NotificationVisibility));
        if(!expires)return;_ = Task.Run(async()=>{try{await Task.Delay(TimeSpan.FromSeconds(6),token);void Clear(){if(_currentNotification?.ExpiresAtUtc==expiration)DismissNotification();}if(_uiContext is null)Clear();else _uiContext.Post(_=>Clear(),null);}catch(OperationCanceledException){}},token);
    }
    private int FindRow(CardViewModel card){for(int i=0;i<Rows.Count;i++)if(Rows[i].Items.Contains(card))return i;return 0;}

    private static IReadOnlyList<DashboardSectionViewModel> CreateShell()=>
    [
        new(NavigationSection.Home,"Home","\uE80F",[]),new(NavigationSection.Games,"Games","\uE7FC",[]),new(NavigationSection.Apps,"Apps","\uECAA",[]),new(NavigationSection.Media,"Media","\uE8B2",[]),new(NavigationSection.Gallery,"Gallery","\uEB9F",[]),new(NavigationSection.Library,"Library","\uE8F1",[]),new(NavigationSection.Collections,"My Stuff","\uE8F1",[]),new(NavigationSection.Settings,"Settings","\uE713",[])
    ];

    private async Task RebuildHomeAsync(DashboardSectionViewModel section,IReadOnlyList<LibraryItem> all,IReadOnlyList<DashboardRowConfiguration> rows)
    {
        section.Rows.Clear();IEnumerable<LibraryItem> visible=all.Where(x=>!x.Hidden);Dictionary<string,LibraryItem> byId=all.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        foreach(DashboardRowConfiguration configuration in rows.Where(x=>x.Enabled).OrderBy(x=>x.SortOrder))
        {
            IEnumerable<LibraryItem> items=configuration.RowType switch
            {
                DashboardRowType.Pinned=>(await _pins.GetAsync(PinLocation.Home)).Where(x=>x.TargetType==PinTargetType.LibraryItem&&byId.ContainsKey(x.TargetId)).Select(x=>byId[x.TargetId]),
                DashboardRowType.ManualCollection=>await ManualCollectionItemsAsync(configuration.SourceDefinitionJson,byId),
                DashboardRowType.SmartCollection=>(await SmartCollectionItemsAsync(configuration.SourceDefinitionJson)).Where(x=>x.LibraryItem is not null).Select(x=>x.LibraryItem!),
                _=>configuration.RowKey switch{"continue"=>visible.Where(x=>x.LastLaunchedAtUtc is not null).OrderByDescending(x=>x.LastLaunchedAtUtc),"apps"=>visible.Where(x=>x.ContentType==ContentType.Application&&x.LastLaunchedAtUtc is not null).OrderByDescending(x=>x.LastLaunchedAtUtc),"favorites"=>visible.Where(x=>x.Favorite),"added"=>visible.OrderByDescending(x=>x.AddedAtUtc),"games"=>visible.Where(x=>x.ContentType==ContentType.Game),"media"=>visible.Where(x=>x.ContentType==ContentType.Media),_=>visible}
            };
            CardViewModel[] cards=HomeLibraryVisibilityPolicy.Resolve(items,byId).Take(configuration.MaximumItems).Select(x=>CreateCard(x,configuration.CardStyle,NavigationSection.Home,configuration.RowKey,configuration.CardSize)).ToArray();if(cards.Length>0||configuration.ShowEmptyState)section.Rows.Add(new(configuration.DisplayName,cards,configuration.RowKey,configuration.ShowTitle));
        }
        if(section.Rows.Count==0)section.Rows.Add(new("Customize Home or open Settings",[],"safe-recovery"));
    }
    private IReadOnlyList<DashboardRowViewModel> HomeRows=>Sections.First(section=>section.Section==NavigationSection.Home).Rows;
    private DashboardRowViewModel? HomeRow(string key)=>HomeRows.FirstOrDefault(row=>string.Equals(row.RowKey,key,StringComparison.Ordinal));
    private CardViewModel[] CreateCards(IEnumerable<LibraryItem> items,CardStyle style,int maximum)=>items.Take(maximum).Select(item=>CreateCard(item,style,NavigationSection.Home)).ToArray();
    private CardViewModel CreateCard(LibraryItem item,CardStyle? style,NavigationSection page,string? row=null,CardSize size=CardSize.Medium)=>new(item,Glyph(item),presentationStyle:style,presentationSize:size,artworkResolver:_artworkResolver,presentationResolver:_cardPresentationResolver,applicationVisualResolver:_applicationVisualResolver,currentPage:page,dashboardRow:row);
    private void NotifyHomeSurfaceChanged()
    {
        NotifyHeroChanged();OnPropertyChanged(nameof(HeroItems));OnPropertyChanged(nameof(HeroCount));OnPropertyChanged(nameof(ContinueSectionTitle));OnPropertyChanged(nameof(ApplicationSectionTitle));OnPropertyChanged(nameof(ContinueItems));OnPropertyChanged(nameof(RecentAppItems));OnPropertyChanged(nameof(FavoriteItems));OnPropertyChanged(nameof(MediaItems));OnPropertyChanged(nameof(RecentItems));OnPropertyChanged(nameof(InstalledAppItems));OnPropertyChanged(nameof(InstalledAppsEmptyVisibility));OnPropertyChanged(nameof(ContinueSectionVisibility));OnPropertyChanged(nameof(ApplicationSectionVisibility));OnPropertyChanged(nameof(MediaSectionVisibility));OnPropertyChanged(nameof(FavoriteSectionVisibility));OnPropertyChanged(nameof(CustomHomeRows));
    }
    private void NotifyHeroChanged(){OnPropertyChanged(nameof(FeaturedItem));OnPropertyChanged(nameof(FeaturedRule));OnPropertyChanged(nameof(FeaturedArtwork));OnPropertyChanged(nameof(DashboardBackgroundArtwork));OnPropertyChanged(nameof(FeaturedTitle));OnPropertyChanged(nameof(FeaturedDescription));OnPropertyChanged(nameof(HeroPrimaryActionText));OnPropertyChanged(nameof(HeroSecondaryActionText));OnPropertyChanged(nameof(SelectedHeroIndex));}
    private async Task<IEnumerable<LibraryItem>> ManualCollectionItemsAsync(string? collectionId,Dictionary<string,LibraryItem> byId){if(string.IsNullOrWhiteSpace(collectionId))return[];return(await _collections.GetItemsAsync(collectionId.Trim('"'))).Where(x=>x.TargetType==CollectionTargetType.LibraryItem&&byId.ContainsKey(x.TargetId)).Select(x=>byId[x.TargetId]);}
    private async Task<IReadOnlyList<CollectionResolvedItem>> SmartCollectionItemsAsync(string? collectionId){if(string.IsNullOrWhiteSpace(collectionId))return[];try{return await _smartCollections.EvaluateAsync(collectionId.Trim('"'),100);}catch(InvalidOperationException){return[];}catch(KeyNotFoundException){return[];}}

    private void Rebuild(DashboardSectionViewModel section,IReadOnlyList<LibraryItem> all)
    {
        IEnumerable<LibraryItem> visible=all.Where(i=>!i.Hidden);section.Rows.Clear();
        IEnumerable<LibraryItem> selected=section.Section switch{NavigationSection.Games=>visible.Where(i=>i.ContentType==ContentType.Game),NavigationSection.Apps=>visible.Where(i=>i.ContentType==ContentType.Application),NavigationSection.Media=>visible.Where(i=>i.ContentType==ContentType.Media),NavigationSection.Library=>all, _=>[]};
        if(section.Section is NavigationSection.Library or NavigationSection.Games)selected=_browse.Apply(section.Section==NavigationSection.Games ? all.Where(item=>item.ContentType==ContentType.Game).ToArray() : all);
        if(section.Section==NavigationSection.Gallery){section.Rows.Add(new("Gallery Preview",[]));return;}if(section.Section==NavigationSection.Collections){section.Rows.Add(new("Personal and Dynamic Shelves",[]));return;}if(section.Section==NavigationSection.Settings){section.Rows.Add(new("Persistent Settings",[]));return;}if(section.Section==NavigationSection.Apps){IGrouping<string,LibraryItem>[] groups=selected.GroupBy(item=>item.PrimaryCategoryName??"Uncategorized").OrderBy(group=>group.Key,StringComparer.OrdinalIgnoreCase).ToArray();if(groups.Length==0)section.Rows.Add(new("Nothing here yet",[]));else foreach(IGrouping<string,LibraryItem> group in groups)Add(section,group.Key,group);return;}Add(section,selected.Any()?"Library":"Nothing here yet",selected);
    }
    private void Add(DashboardSectionViewModel section,string title,IEnumerable<LibraryItem> items)
    {
        CardViewModel[] cards=items.Select(item=>
        {
            CardStyle style=section.Section switch
            {
                NavigationSection.Games when _gamesPresentationMode=="Compact List"=>CardStyle.List,
                NavigationSection.Games when _gamesPresentationMode=="Cover Grid"=>CardStyle.VerticalCover,
                NavigationSection.Games=>CardStyle.Wide,
                NavigationSection.Library when _libraryPresentationMode=="Compact List"=>CardStyle.List,
                NavigationSection.Library when _libraryPresentationMode=="Game Covers"&&item.ContentType==ContentType.Game=>CardStyle.VerticalCover,
                NavigationSection.Library when _libraryPresentationMode=="Application Icons"&&item.ContentType==ContentType.Application=>CardStyle.Icon,
                NavigationSection.Library when item.ContentType==ContentType.Game=>CardStyle.Wide,
                _=>CardStyle.Square,
            };
            return CreateCard(item,style,section.Section,title);
        }).ToArray();if(cards.Length>0||title.Contains("Nothing",StringComparison.Ordinal))section.Rows.Add(new(title,cards));
    }
    private static string Glyph(LibraryItem item)=>item.Favorite?"\uE734":item.ContentType switch{ContentType.Game=>"\uE7FC",ContentType.Media=>"\uE8B2",_=>"\uECAA"};
}

internal static class CollectionExtensions{public static int IndexOf<T>(this IReadOnlyList<T> items,T value){for(int i=0;i<items.Count;i++)if(EqualityComparer<T>.Default.Equals(items[i],value))return i;return-1;}}
