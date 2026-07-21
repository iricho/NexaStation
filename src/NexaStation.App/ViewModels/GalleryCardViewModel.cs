using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using NexaStation.Core;
using NexaStation.Domain;
using System.Globalization;

namespace NexaStation.App.ViewModels;

public sealed class GalleryCardViewModel(GalleryMediaItem item,IGalleryThumbnailService thumbnails,double width=220,double height=150) : ObservableObject
{
    private BitmapImage? _thumbnail;
    private bool _loading;
    private CancellationTokenSource? _thumbnailLoad;
    private double _displayWidth=width;
    private double _displayHeight=height;
    public GalleryMediaItem Item{get;private set;}=item;
    public string Id=>Item.Id;
    public string Title=>Item.SourcePrivacyLevel==GalleryPrivacyLevel.Private&&string.IsNullOrWhiteSpace(Item.FileName)?"Private item":Item.FileName;
    public string DisplayTitle=>Path.GetFileNameWithoutExtension(Title);
    public string Subtitle=>Item.Available?$"{Item.SourceName} · {Item.MediaType}":"Missing file";
    public string MediaLabel=>Item.MediaType==GalleryMediaType.Video
        ? Item.DurationMilliseconds is null?"Video":TimeSpan.FromMilliseconds(Item.DurationMilliseconds.Value).ToString(@"m\:ss",CultureInfo.InvariantCulture)
        : Item.CapturedAtUtc?.ToLocalTime().ToString("MMM d",CultureInfo.CurrentCulture)??"Photo";
    public string AccessibleName=>$"{Item.MediaType}: {Title}";
    public string FallbackGlyph=>Item.MediaType==GalleryMediaType.Video?"▶":"▧";
    public Visibility VideoBadgeVisibility=>IsVideo?Visibility.Visible:Visibility.Collapsed;
    public string Duration=>Item.DurationMilliseconds is null?string.Empty:TimeSpan.FromMilliseconds(Item.DurationMilliseconds.Value).ToString(@"h\:mm\:ss",CultureInfo.InvariantCulture);
    public bool IsVideo=>Item.MediaType==GalleryMediaType.Video;
    public bool IsFavorite=>Item.Favorite;
    public double Width{get;}=width;
    public double Height{get;}=height;
    public double DisplayWidth{get=>_displayWidth;private set=>SetProperty(ref _displayWidth,value);}
    public double DisplayHeight{get=>_displayHeight;private set=>SetProperty(ref _displayHeight,value);}
    public BitmapImage? Thumbnail{get=>_thumbnail;private set=>SetProperty(ref _thumbnail,value);}

    public async Task EnsureThumbnailAsync(CancellationToken cancellationToken=default)
    {
        if(_loading||Thumbnail is not null)return;
        _thumbnailLoad?.Cancel();_thumbnailLoad?.Dispose();_thumbnailLoad=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token=_thumbnailLoad.Token;_loading=true;
        try
        {
            GalleryThumbnailResult result=await thumbnails.GetOrCreateAsync(new(Item,Item.MediaType==GalleryMediaType.Video?GalleryThumbnailType.VideoPreview:GalleryThumbnailType.GridMedium,512),token);
            if(result.Succeeded&&result.LocalCachePath is not null&&File.Exists(result.LocalCachePath))Thumbnail=new BitmapImage{DecodePixelWidth=Math.Clamp((int)Width,64,1024),UriSource=new Uri(result.LocalCachePath,UriKind.Absolute)};
        }
        catch(OperationCanceledException){}
        finally{_loading=false;}
    }

    public void CancelThumbnailRequest()=>_thumbnailLoad?.Cancel();
    public void SetDisplaySize(double width,double height){DisplayWidth=width;DisplayHeight=height;}
    public void SetFavorite(bool favorite){Item=Item with{Favorite=favorite};OnPropertyChanged(nameof(IsFavorite));}
}

public sealed record GalleryRowViewModel(string Title,IReadOnlyList<GalleryCardViewModel> Items);
