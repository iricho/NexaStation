using System.Text.RegularExpressions;
using NexaStation.Recovery;
using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Themes;

public sealed partial class ThemeService(ICustomizationStore store,ISettingsService settings):IThemeService
{
    public const string DefaultThemeId="nexa-dark";private string? _previewOrigin;
    private static readonly ThemeDefinition[] BuiltIns=
    [
        Theme("nexa-dark","Nexa Dark","Dark navy surfaces with a clear blue accent.",ThemeType.Dark,"#4F8DFF","#5BE0C5",BackgroundType.Gradient,CardShape.Rounded,.88,true,AnimationIntensity.Balanced,"nexa-soft"),
        Theme("obsidian","Obsidian","Near-black, subtle contrast, and low motion.",ThemeType.Dark,"#9AA7BD","#D3DAE8",BackgroundType.Solid,CardShape.Soft,.96,false,AnimationIntensity.Low,"minimal-click"),
        Theme("aurora","Aurora","An original teal and purple dark gradient.",ThemeType.Dark,"#5BE0C5","#A776FF",BackgroundType.Gradient,CardShape.Rounded,.82,true,AnimationIntensity.Balanced,"nexa-soft"),
        Theme("light-room","Light Room","Light opaque surfaces with dark readable text.",ThemeType.Light,"#2458C6","#167468",BackgroundType.Solid,CardShape.Soft,1,false,AnimationIntensity.Low,"minimal-click"),
        Theme("high-contrast","High Contrast","Strong borders, opaque surfaces, and reduced motion.",ThemeType.HighContrast,"#FFFF00","#00FFFF",BackgroundType.None,CardShape.Square,1,false,AnimationIntensity.None,"silent")
    ];
    public string CurrentThemeId{get;private set;}=DefaultThemeId;
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    public async Task<IReadOnlyList<ThemeDefinition>> GetAvailableAsync(CancellationToken cancellationToken=default){CurrentThemeId=await settings.GetAsync("Interface.ThemeId",DefaultThemeId,cancellationToken);List<ThemeDefinition> result=[..BuiltIns];foreach(ThemeDefinition custom in await store.GetCustomThemesAsync(cancellationToken))try{Validate(custom);result.Add(custom);}catch(ArgumentException){}if(result.All(x=>x.Id!=CurrentThemeId)){CurrentThemeId=DefaultThemeId;await settings.SetAsync("Interface.ThemeId",DefaultThemeId,cancellationToken);}return result;}
    public async Task ApplyAsync(string themeId,CancellationToken cancellationToken=default){ThemeDefinition theme=await FindAsync(themeId,cancellationToken);Validate(theme);await settings.SetAsync("Interface.ThemeId",theme.Id,cancellationToken);CurrentThemeId=theme.Id;_previewOrigin=null;ThemeChanged?.Invoke(this,new(theme,false));}
    public async Task PreviewAsync(string themeId,CancellationToken cancellationToken=default){ThemeDefinition theme=await FindAsync(themeId,cancellationToken);Validate(theme);_previewOrigin??=CurrentThemeId;ThemeChanged?.Invoke(this,new(theme,true));}
    public async Task CancelPreviewAsync(CancellationToken cancellationToken=default){string restore=_previewOrigin??CurrentThemeId;_previewOrigin=null;ThemeDefinition theme=await FindAsync(restore,cancellationToken);ThemeChanged?.Invoke(this,new(theme,false));}
    public Task ResetAsync(CancellationToken cancellationToken=default)=>ApplyAsync(DefaultThemeId,cancellationToken);
    public async Task<ThemeDefinition> SaveCustomAsync(ThemeDefinition theme,CancellationToken cancellationToken=default){Validate(theme);ThemeDefinition saved=theme with{Id=string.IsNullOrWhiteSpace(theme.Id)?Guid.NewGuid().ToString("N"):theme.Id,BuiltIn=false};await store.SaveThemeAsync(saved,cancellationToken);return saved;}
    public static void Validate(ThemeDefinition theme){if(!ColorPattern().IsMatch(theme.AccentColor)||theme.AccentColor.Equals("#00000000",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("The primary accent must be an opaque #RRGGBB color.",nameof(theme));if(theme.SecondaryAccentColor is not null&&!ColorPattern().IsMatch(theme.SecondaryAccentColor))throw new ArgumentException("The secondary accent must be an opaque #RRGGBB color.",nameof(theme));if(theme.Background.Type==BackgroundType.Image&&theme.Background.Value is { } path){if(path.Contains("..",StringComparison.Ordinal)||Uri.TryCreate(path,UriKind.Absolute,out Uri? uri)&&uri.Scheme is "http" or "https")throw new ArgumentException("Theme backgrounds cannot use traversal or external URLs.",nameof(theme));string root=Path.GetFullPath(new NexaStationPaths().Backgrounds)+Path.DirectorySeparatorChar;string full;try{full=Path.GetFullPath(path);}catch(Exception ex) when(ex is ArgumentException or NotSupportedException or PathTooLongException){throw new ArgumentException("The theme background path is invalid.",nameof(theme),ex);}if(!theme.Background.ManagedCopy||!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Image backgrounds must be NexaStation-managed display copies.",nameof(theme));}}
    public static bool HasReadableContrast(string foreground,string background="#090B13")=>ContrastRatio(foreground,background)>=4.5;
    public static string ReadableForeground(string background)=>ContrastRatio("#FFFFFF",background)>=ContrastRatio("#000000",background)?"#FFFFFF":"#000000";
    public static double ContrastRatio(string foreground,string background)
    {
        if(!ColorPattern().IsMatch(foreground)||!ColorPattern().IsMatch(background))return 0;
        double first=RelativeLuminance(foreground),second=RelativeLuminance(background);
        return (Math.Max(first,second)+.05)/(Math.Min(first,second)+.05);
    }
    private static double RelativeLuminance(string color)
    {
        static double Channel(int value){double channel=value/255d;return channel<=.04045?channel/12.92:Math.Pow((channel+.055)/1.055,2.4);}
        double red=Channel(Convert.ToInt32(color.Substring(1,2),16)),green=Channel(Convert.ToInt32(color.Substring(3,2),16)),blue=Channel(Convert.ToInt32(color.Substring(5,2),16));
        return .2126*red+.7152*green+.0722*blue;
    }
    private async Task<ThemeDefinition> FindAsync(string id,CancellationToken token)=>(await GetAvailableAsync(token)).FirstOrDefault(x=>x.Id==id&&x.Enabled)??throw new ArgumentException("The requested theme is unavailable.",nameof(id));
    private static ThemeDefinition Theme(string id,string name,string description,ThemeType type,string accent,string secondary,BackgroundType background,CardShape shape,double opacity,bool blur,AnimationIntensity animation,string sound)=>new(id,name,description,type,accent,secondary,new(background,background==BackgroundType.Gradient?"#090B13,#20304D":type==ThemeType.Light?"#F4F6FA":"#090B13"),shape,opacity,blur,animation,sound,true,"1.0","NexaStation","0.7");
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$",RegexOptions.CultureInvariant)]private static partial Regex ColorPattern();
}
