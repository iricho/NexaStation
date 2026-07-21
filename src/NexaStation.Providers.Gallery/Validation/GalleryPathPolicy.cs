using NexaStation.Domain;

namespace NexaStation.Providers.Gallery.Validation;

public static class GalleryPathPolicy
{
    public static readonly IReadOnlySet<string> ImageExtensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".png",".webp",".bmp",".gif",".tif",".tiff",".heic",".heif",".avif"};
    public static readonly IReadOnlySet<string> VideoExtensions=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".mp4",".m4v",".mkv",".mov",".avi",".wmv",".webm",".mpg",".mpeg"};

    public static string NormalizeFolder(string path)
    {
        if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("A Gallery folder is required.",nameof(path));
        string full=Path.GetFullPath(path.Trim());
        if(full.Length>32767)throw new ArgumentException("The Gallery folder path is too long.",nameof(path));
        return Path.TrimEndingDirectorySeparator(full);
    }

    public static bool IsInside(string path,string root)
    {
        string full=Path.GetFullPath(path);string normalizedRoot=NormalizeFolder(root);
        return string.Equals(full,normalizedRoot,StringComparison.OrdinalIgnoreCase)||full.StartsWith(normalizedRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }

    public static GalleryMediaType Classify(string extension)
    {
        if(VideoExtensions.Contains(extension))return GalleryMediaType.Video;
        if(string.Equals(extension,".gif",StringComparison.OrdinalIgnoreCase))return GalleryMediaType.AnimatedImage;
        return ImageExtensions.Contains(extension)?GalleryMediaType.Image:GalleryMediaType.Unknown;
    }
}
