using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Layouts;

public sealed class InterfaceLayoutService(ISettingsService settings):IInterfaceLayoutService
{
    public const string ModernId="modern-dashboard";public const string CrossbarId="classic-crossbar";public const string HandheldId="compact-handheld";
    private static readonly InterfaceLayoutDefinition[] Layouts=
    [
        new(ModernId,"Modern Dashboard","Artwork-rich horizontal rows with an optional hero and TV-safe spacing.",InterfaceLayoutType.ModernDashboard,[CardStyle.VerticalCover,CardStyle.Wide,CardStyle.Square,CardStyle.Compact],true,true,false,false,"nexa-dark","Preview.Modern"),
        new(CrossbarId,"Classic Crossbar","Original category-first navigation with efficient static presentation.",InterfaceLayoutType.ClassicCrossbar,[CardStyle.Icon,CardStyle.List,CardStyle.Compact],false,false,true,false,"obsidian","Preview.Crossbar"),
        new(HandheldId,"Compact Handheld","A focused 720p-friendly tab and grid presentation for small Windows screens.",InterfaceLayoutType.CompactHandheld,[CardStyle.Compact,CardStyle.Square,CardStyle.List],false,true,false,true,"nexa-dark","Preview.Handheld")
    ];
    public string CurrentLayoutId{get;private set;}=ModernId;
    public event EventHandler<InterfaceLayoutChangedEventArgs>? LayoutChanged;
    public async Task<IReadOnlyList<InterfaceLayoutDefinition>> GetAvailableAsync(CancellationToken cancellationToken=default){CurrentLayoutId=await settings.GetAsync("Interface.LayoutId",ModernId,cancellationToken);if(Layouts.All(x=>x.Id!=CurrentLayoutId))CurrentLayoutId=ModernId;return Layouts;}
    public async Task ApplyAsync(string layoutId,CancellationToken cancellationToken=default){InterfaceLayoutDefinition layout=Layouts.FirstOrDefault(x=>x.Id==layoutId&&x.Enabled)??throw new ArgumentException("The requested interface layout is not registered.",nameof(layoutId));await settings.SetAsync("Interface.LayoutId",layout.Id,cancellationToken);CurrentLayoutId=layout.Id;LayoutChanged?.Invoke(this,new(layout));}
    public Task ResetAsync(CancellationToken cancellationToken=default)=>ApplyAsync(ModernId,cancellationToken);
}
