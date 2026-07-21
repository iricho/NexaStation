using NexaStation.Customization.Models;
using NexaStation.Presentation.LayoutContracts;

namespace NexaStation.Presentation.Layouts;

public sealed class InterfacePresentationService:IInterfacePresentationService
{
    public string ActiveLayoutId{get;private set;}="modern-dashboard";public event EventHandler<LayoutPresentationState>? PresentationChanged;
    public LayoutPresentationState Create(InterfaceLayoutDefinition layout,DashboardConfiguration dashboard,PerformanceSettings performance)
    {
        bool handheld=layout.LayoutType==InterfaceLayoutType.CompactHandheld,crossbar=layout.LayoutType==InterfaceLayoutType.ClassicCrossbar;double density=dashboard.CardDensity switch{DashboardCardDensity.Dense=>.82,DashboardCardDensity.Comfortable=>1.12,_=>1};double width=(handheld?188:crossbar?230:270)*density;double height=(handheld?118:crossbar?72:154)*density;return new(layout.Id,layout.LayoutType,false,crossbar?"Crossbar":handheld?"Tabs":"Rows",handheld?22:crossbar?46:60,dashboard.RowSpacing,width,height,dashboard.HeroEnabled&&!handheld&&!crossbar,dashboard.ShowStatusBar,dashboard.ShowFooterHints,handheld||performance.ArtworkDecodeQuality<70,handheld);
    }
    public void Activate(LayoutPresentationState presentation){ActiveLayoutId=presentation.LayoutId;PresentationChanged?.Invoke(this,presentation with{Active=true});}
    public bool IsInputOwner(string layoutId)=>string.Equals(layoutId,ActiveLayoutId,StringComparison.Ordinal);
}
