using NexaStation.Domain;

namespace NexaStation.Presentation.Dashboard;

public sealed class ArtworkPresentationService : IArtworkPresentationService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp",
    };

    public ArtworkPresentation Resolve(LibraryItem item, ArtworkRequestKind requestKind, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(item);
        int width = Math.Clamp(targetWidth, 64, 3840);
        int height = Math.Clamp(targetHeight, 64, 2160);
        Asset? asset = requestKind switch
        {
            ArtworkRequestKind.Hero or ArtworkRequestKind.Background => Valid(item.BackgroundAsset) ? item.BackgroundAsset : null,
            ArtworkRequestKind.ApplicationIcon => Valid(item.CoverAsset) ? item.CoverAsset : null,
            _ => Valid(item.CoverAsset) ? item.CoverAsset : Valid(item.BackgroundAsset) ? item.BackgroundAsset : null,
        };

        if (asset is null)
        {
            string reason = requestKind is ArtworkRequestKind.Hero or ArtworkRequestKind.Background
                ? "No displayable wide background asset is available."
                : "No displayable local artwork asset is available.";
            return new(null, false, false, width, height, reason);
        }

        bool wide = IsWide(asset);
        if (requestKind is ArtworkRequestKind.Hero or ArtworkRequestKind.Background && !wide)
            return new(null, false, false, width, height, "The available artwork is not wide enough for a banner or background.");

        return new(asset.LocalPath, true, wide, width, height);
    }

    private static bool Valid(Asset? asset) =>
        asset is not null
        && !string.IsNullOrWhiteSpace(asset.LocalPath)
        && SupportedExtensions.Contains(Path.GetExtension(asset.LocalPath))
        && File.Exists(asset.LocalPath)
        && (asset.FileSize is null || asset.FileSize > 0);

    private static bool IsWide(Asset asset)
    {
        if (asset.Width is > 0 && asset.Height is > 0)
            return asset.Width.Value >= 640 && asset.Width.Value / (double)asset.Height.Value >= 1.35;
        return asset.AssetType == AssetType.Background;
    }
}
