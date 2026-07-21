using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexaStation.Core;
using NexaStation.Media.Hub;

namespace NexaStation.App.ViewModels;

public sealed class MediaHubViewModel(IMediaHubService mediaHub,IGalleryThumbnailService thumbnails,IArtworkResolver artwork,IContentCardPresentationResolver presentation,IApplicationVisualResolver applicationVisual):ObservableObject
{
    private bool _isLoading;public ObservableCollection<MediaHubDisplayRowViewModel> Rows{get;}=[];public bool IsLoading{get=>_isLoading;private set=>SetProperty(ref _isLoading,value);}
    public async Task RefreshAsync(CancellationToken cancellationToken=default){IsLoading=true;try{IReadOnlyList<MediaHubRow> rows=await mediaHub.GetRowsAsync(cancellationToken);Rows.Clear();foreach(MediaHubRow row in rows){CardViewModel[] apps=row.LibraryItems.Select(x=>new CardViewModel(x,x.Favorite?"\uE734":"\uE8B2",presentationStyle:NexaStation.Customization.Models.CardStyle.Square,artworkResolver:artwork,presentationResolver:presentation,applicationVisualResolver:applicationVisual,currentPage:NexaStation.Domain.NavigationSection.Media,dashboardRow:row.Title)).ToArray();GalleryCardViewModel[] gallery=row.GalleryItems.Select(x=>new GalleryCardViewModel(x,thumbnails)).ToArray();if(apps.Length>0||gallery.Length>0)Rows.Add(new(row.Title,apps,gallery));}}finally{IsLoading=false;}}
}
public sealed record MediaHubDisplayRowViewModel(string Title,IReadOnlyList<CardViewModel> LibraryItems,IReadOnlyList<GalleryCardViewModel> GalleryItems);
