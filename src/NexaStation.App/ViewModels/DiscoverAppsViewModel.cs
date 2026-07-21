using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using NexaStation.Core;
using NexaStation.Domain;
using Windows.UI;

namespace NexaStation.App.ViewModels;

public sealed class DiscoverAppsViewModel(
    IDiscoverAppCatalogue catalogue,
    IDiscoverAppStateService states,
    IStoreNavigationService store,
    IExternalUriNavigationService externalUris,
    ILibraryRepository library,
    ILaunchCoordinator launcher,
    IWindowsApplicationLibraryService windowsApplications,
    ISettingsService settings,
    IApplicationVisualResolver applicationVisuals) : ObservableObject
{
    private const string AllCategory="Discover";
    private string _selectedCategory=AllCategory;
    private string? _pendingCatalogueId;
    private bool _refreshing;
    private IReadOnlyList<DiscoverAppDefinition> _definitions=[];

    public ObservableCollection<string> Categories{get;}=["Installed",AllCategory,"Streaming","Music","Communication","Browsers","Media Players","Remote Play","Utilities"];
    public ObservableCollection<DiscoverAppCardViewModel> Items{get;}=[];
    public ObservableCollection<DiscoverAppCardViewModel> StreamingItems{get;}=[];
    public string SelectedCategory{get=>_selectedCategory;private set=>SetProperty(ref _selectedCategory,value);}
    public bool HasPendingInstallCheck=>_pendingCatalogueId is not null;
    public bool IsRefreshing{get=>_refreshing;private set{if(SetProperty(ref _refreshing,value))OnPropertyChanged(nameof(RefreshVisibility));}}
    public Visibility RefreshVisibility=>IsRefreshing?Visibility.Visible:Visibility.Collapsed;
    public event Action<string>? StatusChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken=default)
    {
        _definitions=await catalogue.GetAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task SelectCategoryAsync(string category,CancellationToken cancellationToken=default)
    {
        if(!Categories.Contains(category))return;
        SelectedCategory=category;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken=default)
    {
        if(IsRefreshing)return;IsRefreshing=true;
        try
        {
            List<DiscoverAppCardViewModel> resolved=[];
            foreach(DiscoverAppDefinition definition in _definitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DiscoverAppState state=await states.ResolveAsync(definition,cancellationToken);
                LibraryItem? linked=state.LibraryItemId is null?null:await library.GetByIdAsync(state.LibraryItemId,cancellationToken);
                DiscoverAppCardViewModel card=new(definition,state,linked);await card.ResolveVisualAsync(applicationVisuals,cancellationToken);resolved.Add(card);
            }

            Items.Clear();
            foreach(DiscoverAppCardViewModel card in resolved.Where(MatchesSelectedCategory).OrderByDescending(card=>card.IsInstalled).ThenBy(card=>card.Definition.SortOrder).ThenBy(card=>card.DisplayName,StringComparer.CurrentCultureIgnoreCase))Items.Add(card);
            StreamingItems.Clear();
            foreach(DiscoverAppCardViewModel card in resolved.Where(IsHomeMediaService).OrderByDescending(card=>card.IsInstalled).ThenByDescending(card=>card.IsFavorite).ThenByDescending(card=>card.Definition.Featured).ThenBy(card=>card.Definition.SortOrder).Take(6))StreamingItems.Add(card);
        }
        finally{IsRefreshing=false;}
    }

    public async Task ExecutePrimaryAsync(DiscoverAppCardViewModel card,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(card);
        if(card.IsInstalled&&card.State.LibraryItemId is { } itemId)
        {
            LibraryItem? item=await library.GetByIdAsync(itemId,cancellationToken);
            if(item is null){StatusChanged?.Invoke("The linked application is no longer in the library.");return;}
            LaunchResult result=await launcher.LaunchAsync(item,cancellationToken);StatusChanged?.Invoke(result.Succeeded?$"Opened {card.DisplayName}.":result.ErrorMessage??$"{card.DisplayName} could not be opened.");return;
        }
        if(card.State.State==DiscoverAppInstallationState.BrokenInstallation){await RefreshAsync(cancellationToken);return;}
        if(card.Definition.StoreProductId is { } productId)
        {
            StoreNavigationResult result=await store.OpenProductAsync(productId,cancellationToken);StatusChanged?.Invoke(result.Message);
            if(result.Succeeded){_pendingCatalogueId=card.Definition.Id;OnPropertyChanged(nameof(HasPendingInstallCheck));}
            else if(card.Definition.WebFallbackUri is { } webFallback)await OpenWebAsync(card,webFallback,cancellationToken);
            return;
        }
        if(card.Definition.WebFallbackUri is { } web)await OpenWebAsync(card,web,cancellationToken);
        else StatusChanged?.Invoke($"{card.DisplayName} has no safe Store or web destination.");
    }

    public async Task RefreshAfterReturnAsync(CancellationToken cancellationToken=default)
    {
        if(_pendingCatalogueId is null)return;string pendingId=_pendingCatalogueId;_pendingCatalogueId=null;OnPropertyChanged(nameof(HasPendingInstallCheck));
        await Task.Delay(900,cancellationToken);
        try
        {
            DiscoverAppDefinition? definition=_definitions.FirstOrDefault(value=>value.Id.Equals(pendingId,StringComparison.OrdinalIgnoreCase));
            if(definition is null)return;
            WindowsApplicationScanResult scan=await windowsApplications.ScanAllAsync(new(await settings.GetAsync("Providers.WindowsApps.ScanCurrentUser",true,cancellationToken),await settings.GetAsync("Providers.WindowsApps.ScanCommon",true,cancellationToken),true),await settings.GetAsync("Providers.WindowsApps.IncludeAppsFolder",true,cancellationToken),await settings.GetAsync("Providers.WindowsApps.DetectPwas",true,cancellationToken),cancellationToken);
            WindowsApplicationCandidate? exact=scan.Candidates.FirstOrDefault(candidate=>MatchesIdentity(definition,candidate));
            if(exact is not null)
            {
                Dictionary<string,string> providerData=new(exact.ProviderData,StringComparer.OrdinalIgnoreCase){{"catalogueId",definition.Id}};
                exact=exact with{ProviderData=providerData};
                WindowsApplicationImportPreview preview=await windowsApplications.CreatePreviewAsync(scan with{Candidates=[exact]},cancellationToken);
                WindowsApplicationPreviewCandidate candidate=preview.Candidates[0];
                if(candidate.Status==WindowsImportStatus.New)await windowsApplications.ImportAsync([new(candidate,WindowsImportAction.Import)],cancellationToken);
                else if(candidate.Status==WindowsImportStatus.PossibleDuplicate&&candidate.ExistingLibraryItemId is { } existing)await windowsApplications.ImportAsync([new(candidate,WindowsImportAction.LinkToExisting,existing)],cancellationToken);
            }
            await RefreshAsync(cancellationToken);
            DiscoverAppCardViewModel? refreshed=Items.Concat(StreamingItems).FirstOrDefault(value=>value.Definition.Id.Equals(pendingId,StringComparison.OrdinalIgnoreCase));
            StatusChanged?.Invoke(refreshed?.IsInstalled==true?$"{definition.DisplayName} is installed and ready to open.":$"{definition.DisplayName} was not detected yet. You can check again after installation finishes.");
        }
        catch(Exception exception)when(exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            StatusChanged?.Invoke("Installed-app discovery could not complete. Use Refresh Apps to try again.");
        }
    }

    private bool MatchesSelectedCategory(DiscoverAppCardViewModel card)=>SelectedCategory switch{"Installed"=>card.IsInstalled,AllCategory=>true,_=>card.Category.Equals(SelectedCategory,StringComparison.OrdinalIgnoreCase)};
    private static bool IsHomeMediaService(DiscoverAppCardViewModel card)=>card.Category is "Streaming" or "Music";
    private async Task OpenWebAsync(DiscoverAppCardViewModel card,Uri uri,CancellationToken cancellationToken){ExternalUriNavigationResult result=await externalUris.OpenAsync(uri,cancellationToken);StatusChanged?.Invoke(result.Succeeded?$"Opened {card.DisplayName} as a Web App.":result.Message);}
    internal static bool MatchesIdentity(DiscoverAppDefinition definition,WindowsApplicationCandidate candidate)=>
        Known(definition.KnownAppUserModelIds,candidate.AppUserModelId)||
        Known(definition.KnownPackageFamilyNames,candidate.PackageFamilyName)||
        candidate.TargetPath is { } path&&File.Exists(path)&&Known(definition.KnownExecutableNames,Path.GetFileName(path))||
        candidate.SourceType==WindowsApplicationSourceType.ProgressiveWebApplication&&(Known(definition.KnownPwaIdentities,candidate.BrowserAppId)||Known(definition.KnownPwaIdentities,candidate.WebAppUrl?.AbsoluteUri));
    private static bool Known(IReadOnlyList<string> known,string? value)=>!string.IsNullOrWhiteSpace(value)&&known.Contains(value,StringComparer.OrdinalIgnoreCase);
}

public sealed class DiscoverAppCardViewModel : ObservableObject
{
    private ImageSource? _icon;private Brush _tileBrush;private double _displaySize=94;
    public DiscoverAppCardViewModel(DiscoverAppDefinition definition,DiscoverAppState state,LibraryItem? linkedItem)
    {
        Definition=definition;State=state;LinkedItem=linkedItem;
        string? icon=ResolveAssetPath(definition.IconAssetPath)??linkedItem?.CoverAsset?.LocalPath;
        if(icon is { } path&&File.Exists(path))_icon=CreateImageSource(path);
        byte[] hash=SHA256.HashData(Encoding.UTF8.GetBytes(definition.Id));Color color=Color.FromArgb(255,(byte)(42+hash[0]%70),(byte)(52+hash[1]%70),(byte)(78+hash[2]%80));_tileBrush=new SolidColorBrush(color);
    }
    public DiscoverAppDefinition Definition{get;}public DiscoverAppState State{get;}public LibraryItem? LinkedItem{get;}public string DisplayName=>Definition.DisplayName;public string Description=>Definition.Description;public string Category=>Definition.Category;public bool IsInstalled=>State.IsInstalled;public bool IsFavorite=>LinkedItem?.Favorite==true;public string Initial=>ContentCardInitials.Create(DisplayName);public string IconAutomationName=>$"{DisplayName} logo";public ImageSource? Icon{get=>_icon;private set{if(SetProperty(ref _icon,value)){OnPropertyChanged(nameof(InitialVisibility));OnPropertyChanged(nameof(IconVisibility));}}}public Visibility InitialVisibility=>Icon is null?Visibility.Visible:Visibility.Collapsed;public Visibility IconVisibility=>Icon is null?Visibility.Collapsed:Visibility.Visible;public Brush TileBrush{get=>_tileBrush;private set=>SetProperty(ref _tileBrush,value);}public double DisplaySize{get=>_displaySize;private set=>SetProperty(ref _displaySize,value);}
    public void SetDisplaySize(double size)=>DisplaySize=size;
    public string StateBadge=>State.State switch{DiscoverAppInstallationState.Installed=>"Installed",DiscoverAppInstallationState.BrokenInstallation=>"Check installation",DiscoverAppInstallationState.StoreUnavailable=>"Store unavailable",_=>Definition.AppKind is DiscoverAppKind.WebService or DiscoverAppKind.ProgressiveWebApplication?"Web App":"Available"};
    public string PrimaryAction=>IsInstalled?"Open":State.State==DiscoverAppInstallationState.BrokenInstallation?"Check Installation":Definition.StoreProductId is not null?"Open Store":Definition.WebFallbackUri is not null?"Open Web App":"Unavailable";
    public async Task ResolveVisualAsync(IApplicationVisualResolver resolver,CancellationToken token)
    {
        string? catalogueIcon=ResolveAssetPath(Definition.IconAssetPath);
        if(catalogueIcon is { } cataloguePath&&File.Exists(cataloguePath))
        {
            Icon=CreateImageSource(cataloguePath);
            return;
        }
        LaunchTarget? target=LinkedItem?.LaunchTarget;ApplicationVisualResult result=await resolver.ResolveAsync(new(Definition.Id,DisplayName,PackagedIconPath:LinkedItem?.CoverAsset?.LocalPath,ShortcutPath:target?.ShortcutPath,ExecutablePath:target?.ExecutablePath,CatalogueIconPath:catalogueIcon,AppUserModelId:target?.AppUserModelId,ShellParsingPath:target?.ShellParsingPath,Category:Category),token);if(result.IconPath is { } path&&File.Exists(path))Icon=CreateImageSource(path);TileBrush=new SolidColorBrush(Parse(result.SuggestedBackgroundColor));
    }
    private static string? ResolveAssetPath(string? path)=>string.IsNullOrWhiteSpace(path)?null:Path.IsPathFullyQualified(path)?path:Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,path.Replace('/',Path.DirectorySeparatorChar)));
    private static ImageSource CreateImageSource(string path)=>Path.GetExtension(path).Equals(".svg",StringComparison.OrdinalIgnoreCase)?new SvgImageSource{UriSource=new Uri(path,UriKind.Absolute)}:new BitmapImage(new Uri(path,UriKind.Absolute));
    private static Color Parse(string value){string hex=value.TrimStart('#');if(hex.Length==6)hex="FF"+hex;return hex.Length==8?Color.FromArgb(Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..6],16),Convert.ToByte(hex[6..8],16)):Color.FromArgb(255,42,52,78);}
}
