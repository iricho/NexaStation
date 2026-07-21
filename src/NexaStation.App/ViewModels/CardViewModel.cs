using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Domain;
using System.Globalization;
using Windows.UI;

namespace NexaStation.App.ViewModels;

public sealed class CardViewModel : ObservableObject
{
    private static readonly SemaphoreSlim ArtworkGate = new(6, 6);
    private readonly IArtworkResolver? _artworkResolver;
    private readonly IApplicationVisualResolver? _applicationVisualResolver;
    private CancellationTokenSource? _artworkLoad;
    private BitmapImage? _artwork;
    private BitmapImage? _backgroundArtwork;
    private ArtworkResolutionResult? _artworkResolution;
    private Brush? _fallbackBrush;
    private NexaStation.Core.ContentCardViewModel _presentation;
    private double _displayWidth;
    private double _displayHeight;

    public CardViewModel(LibraryItem item,string glyph,bool isFeatured=false,double width=300,double height=166,CardStyle? presentationStyle=null,CardSize presentationSize=CardSize.Medium,IArtworkResolver? artworkResolver=null,IContentCardPresentationResolver? presentationResolver=null,IApplicationVisualResolver? applicationVisualResolver=null,NavigationSection currentPage=NavigationSection.Home,string? dashboardRow=null,double viewportWidth=1920)
    {
        Item=item;Glyph=glyph;IsFeatured=isFeatured;_artworkResolver=artworkResolver;_applicationVisualResolver=applicationVisualResolver;
        NexaStation.Core.ContentCardPresentation resolved=presentationResolver?.Resolve(new(item.ContentType,currentPage,item.LaunchTarget?.ProviderId,dashboardRow,presentationStyle?.ToString(),viewportWidth,item.CoverAsset is not null,item.ContentType==ContentType.Application&&item.CoverAsset is not null,IsStreamingService:item.ContentType==ContentType.Media))??DefaultPresentation(item,presentationStyle,viewportWidth);
        double sizeScale=presentationSize switch{CardSize.Small=>.82,CardSize.Large=>1.18,_=>1};Width=isFeatured?width:resolved.Width*sizeScale;Height=isFeatured?height:resolved.Height*sizeScale;_displayWidth=Width;_displayHeight=Height;
        _artwork=Image(item.CoverAsset?.LocalPath,(int)Width);
        _backgroundArtwork=Image(item.BackgroundAsset?.LocalPath??item.CoverAsset?.LocalPath,1600);
        _fallbackBrush=CreateFallbackBrush(Item.Id,Item.ContentType);
        PresentationType=resolved.PresentationType;
        _presentation=new(Id,TargetType(item),Title,Subtitle,ProviderLabel,PresentationType,item.CoverAsset?.LocalPath,item.BackgroundAsset?.LocalPath,ColorHex(Item.Id,item.ContentType),"#FFFFFFFF",true,!IsUnavailable,IsUnavailable?"Details":"Open","Options",$"{Title}, {ProviderLabel}");
    }

    public LibraryItem Item{get;}public string Id=>Item.Id;public string Title=>Item.Title;public string Subtitle=>Item.Subtitle??string.Empty;public string Description=>Item.Description??string.Empty;public ContentType ContentType=>Item.ContentType;public bool IsApplication=>Item.ContentType==ContentType.Application;public bool IsGame=>Item.ContentType==ContentType.Game;public bool IsFavorite=>Item.Favorite;
    public string Playtime=>Item.TotalPlaytimeSeconds<60?(Item.ContentType==ContentType.Application?"Not used yet":"Not played yet"):TimeSpan.FromSeconds(Item.TotalPlaytimeSeconds).ToString(@"h\:mm",CultureInfo.InvariantCulture)+(Item.ContentType==ContentType.Application?" used":" played");
    public BitmapImage? Artwork{get=>_artwork;private set{if(SetProperty(ref _artwork,value)){OnPropertyChanged(nameof(HasArtwork));OnPropertyChanged(nameof(ArtworkVisibility));OnPropertyChanged(nameof(FallbackVisibility));}}}
    public BitmapImage? BackgroundArtwork{get=>_backgroundArtwork;private set=>SetProperty(ref _backgroundArtwork,value);}
    public bool HasArtwork=>Artwork is not null||BackgroundArtwork is not null;public Visibility ArtworkVisibility=>HasArtwork?Visibility.Visible:Visibility.Collapsed;public Visibility FallbackVisibility=>HasArtwork?Visibility.Collapsed:Visibility.Visible;
    public string FallbackInitial=>ContentCardInitials.Create(Title);
    public Brush FallbackBrush=>_fallbackBrush??=CreateFallbackBrush(Item.Id,Item.ContentType);
    public bool IsUnavailable=>Item.LaunchTarget is null||LaunchTargetValidator.Validate(Item.LaunchTarget) is not null;public string AvailabilityLabel=>IsUnavailable?"Unavailable":string.Empty;public string ProviderLabel=>Item.LaunchTarget?.ProviderId?.ToUpper(CultureInfo.CurrentCulture)??Item.PrimaryCategoryName??Item.ContentType.ToString();
    public string ActivityLabel=>Item.LastLaunchedAtUtc is null?$"Added {RelativeTime(Item.AddedAtUtc)}":Item.ContentType==ContentType.Game?$"Last played {RelativeTime(Item.LastLaunchedAtUtc.Value)}":$"Last opened {RelativeTime(Item.LastLaunchedAtUtc.Value)}";
    public string ArtworkDiagnostic=>_artworkResolution is null?$"Item={Id}; Type={ContentType}; Resolver=legacy-local; Path={Item.CoverAsset?.LocalPath??"(none)"}; Display={Width:0}x{Height:0}; Fallback={(HasArtwork?"none":"missing-or-unsupported")}":$"Item={Id}; Type={ContentType}; Resolver=unified; Source={_artworkResolution.SourceType}; Path={_artworkResolution.AssetPath??"(none)"}; Cache={_artworkResolution.CacheKey}; Decode={_artworkResolution.Width}x{_artworkResolution.Height}; Display={Width:0}x{Height:0}; Fallback={_artworkResolution.FallbackType}; Reason={_artworkResolution.FailureReason??"none"}";
    public string Glyph{get;}public bool IsFeatured{get;}public double Width{get;}public double Height{get;}public double DisplayWidth{get=>_displayWidth;private set=>SetProperty(ref _displayWidth,value);}public double DisplayHeight{get=>_displayHeight;private set=>SetProperty(ref _displayHeight,value);}public ContentCardPresentationType PresentationType{get;}public NexaStation.Core.ContentCardViewModel Presentation{get=>_presentation;private set=>SetProperty(ref _presentation,value);}

    public void SetDisplaySize(double width,double height){DisplayWidth=width;DisplayHeight=height;}

    public async Task EnsureArtworkAsync(CancellationToken cancellationToken=default)
    {
        _artworkLoad?.Cancel();_artworkLoad?.Dispose();_artworkLoad=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);CancellationToken token=_artworkLoad.Token;
        await ArtworkGate.WaitAsync(token);
        try
        {
            if(IsApplication&&_applicationVisualResolver is not null)
            {
                LaunchTarget? target=Item.LaunchTarget;ApplicationVisualResult visual=await _applicationVisualResolver.ResolveAsync(new(Id,Title,PackagedIconPath:Item.CoverAsset?.LocalPath,ShortcutPath:target?.ShortcutPath,ExecutablePath:target?.ExecutablePath??target?.ExpectedExecutablePath,AppUserModelId:target?.AppUserModelId,ShellParsingPath:target?.ShellParsingPath,Category:Item.PrimaryCategoryName,DesiredSize:256),token);if(token.IsCancellationRequested)return;
                if(!visual.IsFallback&&visual.IconPath is not null){Artwork=Image(visual.IconPath,256);_fallbackBrush=new SolidColorBrush(ParseColor(visual.SuggestedBackgroundColor));OnPropertyChanged(nameof(FallbackBrush));Presentation=Presentation with{PrimaryVisual=visual.IconPath,BackgroundColor=visual.SuggestedBackgroundColor,ForegroundColor=visual.SuggestedForegroundColor};return;}
            }
            if(_artworkResolver is null)return;
            AssetType type=IsApplication?AssetType.Icon:AssetType.Cover;ArtworkResolutionResult result=await _artworkResolver.ResolveAsync(new(Id,ContentType,Item.LaunchTarget?.ProviderId,type,Math.Clamp((int)Width,64,1024),Math.Clamp((int)Height,64,1024)),token);if(token.IsCancellationRequested)return;_artworkResolution=result;Artwork=Image(result.AssetPath,(int)Width);OnPropertyChanged(nameof(ArtworkDiagnostic));
        }
        finally{ArtworkGate.Release();}
    }

    public async Task EnsureBackgroundArtworkAsync(CancellationToken cancellationToken=default)
    {
        if(_artworkResolver is null)return;ArtworkResolutionResult result=await _artworkResolver.ResolveAsync(new(Id,ContentType,Item.LaunchTarget?.ProviderId,AssetType.Background,1600,700),cancellationToken);if(cancellationToken.IsCancellationRequested)return;_artworkResolution=result;BackgroundArtwork=Image(result.AssetPath,1600);OnPropertyChanged(nameof(ArtworkDiagnostic));
    }

    public void CancelArtworkRequest(){_artworkLoad?.Cancel();_artworkLoad?.Dispose();_artworkLoad=null;}
    private static BitmapImage? Image(string? path,int decodeWidth)=>!string.IsNullOrWhiteSpace(path)&&Displayable(path)?new(){DecodePixelWidth=Math.Clamp(decodeWidth,64,1920),UriSource=new Uri(path,UriKind.Absolute)}:null;
    private static string RelativeTime(DateTime value){TimeSpan age=DateTime.UtcNow-value.ToUniversalTime();if(age<TimeSpan.FromMinutes(2))return"just now";if(age<TimeSpan.FromHours(1))return$"{Math.Max(2,(int)age.TotalMinutes)}m ago";if(age<TimeSpan.FromDays(1))return$"{(int)age.TotalHours}h ago";if(age<TimeSpan.FromDays(14))return$"{(int)age.TotalDays}d ago";return value.ToLocalTime().ToString("MMM d",CultureInfo.CurrentCulture);}
    private static bool Displayable(string path){string extension=Path.GetExtension(path);return File.Exists(path)&&extension.ToLowerInvariant() is ".bmp" or ".gif" or ".jpeg" or ".jpg" or ".png" or ".tif" or ".tiff" or ".webp";}
    private static NexaStation.Core.ContentCardPresentation DefaultPresentation(LibraryItem item,CardStyle? style,double viewport)=>new NexaStation.Presentation.CardPresentation.ContentCardPresentationResolver().Resolve(new(item.ContentType,NavigationSection.Home,SelectedCardStyle:style?.ToString(),ViewportWidth:viewport,HasArtwork:item.CoverAsset is not null,HasApplicationIcon:item.ContentType==ContentType.Application&&item.CoverAsset is not null));
    private static ContentCardTargetType TargetType(LibraryItem item)=>item.ContentType switch{ContentType.Game=>ContentCardTargetType.Game,ContentType.Application=>ContentCardTargetType.Application,ContentType.Media=>ContentCardTargetType.StreamingService,_=>ContentCardTargetType.MissingContent};
    private static LinearGradientBrush CreateFallbackBrush(string id,ContentType type){(Color first,Color second)=StableColors(id,type);return new(){StartPoint=new(.05,0),EndPoint=new(.95,1),GradientStops={new(){Color=first,Offset=0},new(){Color=second,Offset=1}}};}
    private static string ColorHex(string id,ContentType type){(Color first,_)=StableColors(id,type);return $"#{first.A:X2}{first.R:X2}{first.G:X2}{first.B:X2}";}
    private static Color ParseColor(string value){string hex=value.TrimStart('#');if(hex.Length==6)hex="FF"+hex;return hex.Length==8?Color.FromArgb(Convert.ToByte(hex[..2],16),Convert.ToByte(hex[2..4],16),Convert.ToByte(hex[4..6],16),Convert.ToByte(hex[6..8],16)):Color.FromArgb(255,42,50,72);}
    private static(Color First,Color Second)StableColors(string id,ContentType type){uint hash=2166136261;foreach(char value in id){hash^=value;hash*=16777619;}byte hue=(byte)(hash%160);Color first=type switch{ContentType.Game=>Color.FromArgb(255,(byte)(34+hue/5),(byte)(50+hue/3),(byte)(92+hue/2)),ContentType.Media=>Color.FromArgb(255,(byte)(45+hue/4),(byte)(58+hue/2),(byte)(82+hue/3)),_=>Color.FromArgb(255,(byte)(43+hue/3),(byte)(54+hue/4),(byte)(88+hue/2))};Color second=Color.FromArgb(255,(byte)(15+hue/8),(byte)(31+hue/5),(byte)(46+hue/6));return(first,second);}
}
