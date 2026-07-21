using NexaStation.Domain;

namespace NexaStation.Gallery.Navigation;

public static class GalleryNavigator
{
    public static GalleryMediaItem? Move(IReadOnlyList<GalleryMediaItem> items,string? currentId,int offset,bool imagesOnly=true)
    {
        if(items.Count==0||offset==0)return null;GalleryMediaItem[] eligible=items.Where(item=>item.Available&&!item.Hidden&&(!imagesOnly||item.MediaType is GalleryMediaType.Image or GalleryMediaType.AnimatedImage)).ToArray();if(eligible.Length==0)return null;
        int current=Array.FindIndex(eligible,item=>string.Equals(item.Id,currentId,StringComparison.Ordinal));if(current<0)return offset>0?eligible[0]:eligible[^1];int next=(current+Math.Sign(offset)+eligible.Length)%eligible.Length;return eligible[next];
    }
}
