using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Media.Hub;

public sealed class MediaHubService(ILibraryRepository library,IGalleryPersistence gallery,IRecentMediaService recent,ISettingsService settings):IMediaHubService
{
    private static readonly string[] MediaCategories=["Media","Streaming","Entertainment","Music","Video"];
    public async Task<IReadOnlyList<MediaHubRow>> GetRowsAsync(CancellationToken cancellationToken=default){IReadOnlyList<LibraryItem> items=await library.QueryAsync(new LibraryQuery(IncludeHidden:false,MaximumResults:1000),cancellationToken);IReadOnlyList<GalleryMediaItem> videos=await gallery.QueryAsync(new GalleryQuery(MediaType:GalleryMediaType.Video,IncludePrivate:false,MaximumResults:100),cancellationToken);int recentLimit=await settings.GetAsync("Media.RecentLimit",30,cancellationToken);IReadOnlyList<RecentMediaEntry> recentEntries=await recent.GetRecentAsync(recentLimit,cancellationToken);HashSet<string> recentIds=recentEntries.Where(x=>x.MediaItemId is not null).Select(x=>x.MediaItemId!).ToHashSet(StringComparer.Ordinal);IReadOnlyList<GalleryMediaItem> recentMedia=videos.Where(x=>recentIds.Contains(x.Id)).OrderBy(x=>recentEntries.First(r=>r.MediaItemId==x.Id).OpenedAtUtc).ToArray();IEnumerable<LibraryItem> apps=items.Where(IsMediaApplication);return[
        new("Recently Opened",[],recentMedia),new("Streaming Applications",apps.Where(x=>Category(x,"Streaming")).ToArray(),[]),new("Video Applications",apps.Where(x=>Category(x,"Video")).ToArray(),[]),new("Music Applications",apps.Where(x=>Category(x,"Music")).ToArray(),[]),new("Local Videos",[],videos.Where(x=>x.SourceKind==GallerySourceKind.Videos).ToArray()),new("Gallery Recordings",[],videos.Where(x=>x.SourceKind==GallerySourceKind.Recordings).ToArray()),new("Favorites",items.Where(x=>x.Favorite&&IsMediaApplication(x)).ToArray(),videos.Where(x=>x.Favorite).ToArray()),new("Media Servers",apps.Where(x=>(x.Title+" "+x.PrimaryCategoryName).Contains("server",StringComparison.OrdinalIgnoreCase)).ToArray(),[]),new("All Media Apps",apps.ToArray(),[])];}
    public static bool IsMediaApplication(LibraryItem item)=>!LooksLikeAudioUtility(item)&&(item.ContentType==ContentType.Media||(item.ContentType==ContentType.Application&&MediaCategories.Any(c=>Category(item,c))));
    private static bool LooksLikeAudioUtility(LibraryItem item)
    {
        if(Category(item,"Streaming")||Category(item,"Music")||Category(item,"Video"))return false;
        string value=item.Title+' '+item.PrimaryCategoryName;return value.Contains("audio console",StringComparison.OrdinalIgnoreCase)||value.Contains("audio control",StringComparison.OrdinalIgnoreCase)||value.Contains("sound settings",StringComparison.OrdinalIgnoreCase)||value.Contains("driver",StringComparison.OrdinalIgnoreCase);
    }
    private static bool Category(LibraryItem item,string value)=>string.Equals(item.PrimaryCategoryName,value,StringComparison.OrdinalIgnoreCase);
}
