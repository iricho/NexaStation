using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Recovery;

namespace NexaStation.Customization.Themes;

public sealed class ThemeAssetService:IThemeAssetService
{
    private static readonly HashSet<string> AllowedExtensions=[".png",".jpg",".jpeg",".webp"];
    private readonly string _root;
    public ThemeAssetService():this(new NexaStationPaths()){}
    public ThemeAssetService(INexaStationPaths paths)=>_root=paths.ThemeBackgrounds;
    public async Task<BackgroundDefinition> ImportImageAsync(string sourcePath,BackgroundStretch stretch,int blurAmount,double darkeningOverlay,double opacity,CancellationToken cancellationToken=default)
    {
        string full=Path.GetFullPath(sourcePath);string extension=Path.GetExtension(full).ToLowerInvariant();if(!File.Exists(full)||!AllowedExtensions.Contains(extension))throw new ArgumentException("Choose an existing PNG, JPEG, or WebP image.",nameof(sourcePath));Directory.CreateDirectory(_root);string managed=Path.Combine(_root,$"custom-background{extension}");string temporary=$"{managed}.{Guid.NewGuid():N}.tmp";await using(FileStream input=new(full,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true))await using(FileStream output=new(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true))await input.CopyToAsync(output,cancellationToken);File.Move(temporary,managed,true);return new(BackgroundType.Image,managed,stretch,Math.Clamp(blurAmount,0,50),Math.Clamp(darkeningOverlay,0,1),Math.Clamp(opacity,0,1),true);
    }
}
