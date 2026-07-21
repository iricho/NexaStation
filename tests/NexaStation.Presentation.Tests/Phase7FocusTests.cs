using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Domain;
using NexaStation.Presentation.CardPresentation;
using NexaStation.Presentation.Focus;
using NexaStation.Presentation.Layouts;
using NexaStation.Presentation.LayoutContracts;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class Phase7FocusTests
{
    public static IEnumerable<object[]> FocusCases()=>Enumerable.Range(1,7).Select(x=>new object[]{x});

    [Theory,MemberData(nameof(FocusCases))]
    public async Task FocusRequirements97Through103(int scenario)
    {
        await using ApplicationStateService state=new(new MemoryStateRepository(),TimeSpan.FromHours(1));SemanticFocusService service=new(state);string selected=scenario>=4?"removed":"selected";await service.SaveAsync(new(NavigationSection.Home,"favorites",CollectionTargetType.LibraryItem,selected,null,"modern-dashboard",1));string[] visible=scenario switch{6=>[],4 or 5=>["first","nearest"],_=>["first","selected","last"]};FocusRestoreResult result=await service.RestoreAsync(new(NavigationSection.Home,scenario==1?"classic-crossbar":"modern-dashboard",visible));
        if(scenario==6){Assert.False(result.Restored);Assert.Null(result.TargetId);}else if(scenario is 4 or 5){Assert.True(result.Restored);Assert.Equal("nearest",result.TargetId);Assert.NotNull(result.Reason);}else{Assert.True(result.Restored);Assert.Equal("selected",result.TargetId);}

        InterfacePresentationService presentation=new();InterfaceLayoutDefinition layout=Layout(scenario==1?InterfaceLayoutType.ClassicCrossbar:InterfaceLayoutType.ModernDashboard);LayoutPresentationState created=presentation.Create(layout,Dashboard(layout.Id),Performance());presentation.Activate(created);Assert.True(presentation.IsInputOwner(layout.Id));Assert.False(presentation.IsInputOwner("inactive-layout"));NexaStation.Customization.Models.CardPresentation card=new CardPresentationService().Resolve(CardStyle.Wide,CardSize.Medium,layout.LayoutType);Assert.True(card.ShowTitle);
    }

    private static InterfaceLayoutDefinition Layout(InterfaceLayoutType type)=>new(type.ToString(),type.ToString(),"Test layout",type,[CardStyle.Wide],true,true,true,true,"nexa-dark","preview");
    private static DashboardConfiguration Dashboard(string layout)=>new("dashboard",layout,"Default",true,true,DashboardCardDensity.Balanced,18,true,true,DateTime.UtcNow,DateTime.UtcNow);
    private static PerformanceSettings Performance()=>new(VisualPerformancePreset.Balanced,true,1,AnimationIntensity.Balanced,true,80,96,2048,true,true,true,18,false);
    private sealed class MemoryStateRepository:IApplicationStateRepository{private readonly Dictionary<string,string> _values=[];public Task<string?> GetAsync(string key,CancellationToken cancellationToken=default)=>Task.FromResult(_values.GetValueOrDefault(key));public Task SetAsync(string key,string value,CancellationToken cancellationToken=default){_values[key]=value;return Task.CompletedTask;}}
}
