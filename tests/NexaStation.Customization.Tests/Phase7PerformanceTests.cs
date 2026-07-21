using System.Diagnostics;
using System.Text.Json;
using NexaStation.Customization.Collections;
using NexaStation.Customization.Dashboard;
using NexaStation.Customization.Layouts;
using NexaStation.Customization.Models;
using NexaStation.Customization.Pinning;
using NexaStation.Customization.Themes;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.Customization.Tests;

public sealed class Phase7PerformanceTests
{
    [Fact]
    public async Task SyntheticPhase7WorkloadsRemainBoundedAndReportMeasurements()
    {
        MemorySettings settings=new();MemoryCustomizationStore store=new();MemoryLibrary library=new();for(int index=0;index<5000;index++)library.Items.Add(Item(index));InterfaceLayoutService layouts=new(settings);DashboardConfigurationService dashboards=new(store);PinService pins=new(store);CollectionService collections=new(store);SmartCollectionQueryService smart=new(store,library);ThemeService themes=new(store,settings);Dictionary<string,double> timings=[];Stopwatch clock=Stopwatch.StartNew();await layouts.GetAvailableAsync();DashboardConfiguration dashboard=await dashboards.GetOrCreateAsync(InterfaceLayoutService.ModernId);timings["coldLayoutInitializationMs"]=Elapsed(clock);
        clock.Restart();foreach(string id in new[]{InterfaceLayoutService.CrossbarId,InterfaceLayoutService.HandheldId,InterfaceLayoutService.ModernId})await layouts.ApplyAsync(id);timings["warmLayoutSwitchThreeMs"]=Elapsed(clock);clock.Restart();foreach(string id in new[]{"nexa-dark","obsidian","aurora","light-room","high-contrast"})await themes.ApplyAsync(id);timings["themeSwitchFiveMs"]=Elapsed(clock);
        for(int index=0;index<100;index++){CollectionDefinition manual=Collection($"manual-{index}",CollectionType.Manual,index);await collections.SaveAsync(manual);CollectionDefinition definition=Collection($"smart-{index}",CollectionType.Smart,index+100);await collections.SaveAsync(definition);await collections.SetRulesAsync(definition.Id,[new($"rule-{index}",definition.Id,0,SmartRuleField.Favorite,SmartRuleOperator.IsTrue,null,0,DateTime.UtcNow,DateTime.UtcNow)]);}for(int index=0;index<500;index++)await pins.PinAsync(new(PinTargetType.LibraryItem,$"item-{index}",PinLocation.Home));
        List<DashboardRowConfiguration> rows=[];DateTime now=DateTime.UtcNow;for(int index=0;index<50;index++)rows.Add(new($"row-{index}",dashboard.Id,$"row-{index}",$"Row {index}",DashboardRowType.BuiltIn,null,index,true,18,CardStyle.Wide,CardSize.Medium,true,true,true,false,now,now));clock.Restart();await dashboards.SaveRowsAsync(dashboard.Id,rows);await dashboards.MoveRowAsync(dashboard.Id,"row-0",1);timings["saveAndReorderFiftyRowsMs"]=Elapsed(clock);
        foreach(int size in new[]{100,1000,5000}){clock.Restart();LibraryItem[] projected=library.Items.Take(size).Where(x=>!x.Hidden).OrderBy(x=>x.SortTitle,StringComparer.Ordinal).Take(24).ToArray();timings[$"homeProjection{size}ItemsMs"]=Elapsed(clock);Assert.InRange(projected.Length,1,24);}clock.Restart();IReadOnlyList<CollectionResolvedItem> result=await smart.EvaluateAsync("smart-0",500);timings["smartQueryFiveThousandCandidatesMs"]=Elapsed(clock);Assert.InRange(result.Count,1,500);Assert.Equal(500,(await pins.GetAsync(PinLocation.Home)).Count);Assert.Equal(200,(await collections.GetAsync()).Count);Assert.Equal(50,(await dashboards.GetRowsAsync(dashboard.Id)).Count);GC.Collect();timings["managedMemoryMb"]=GC.GetTotalMemory(true)/1024d/1024d;Console.WriteLine("PHASE7_PERF "+JsonSerializer.Serialize(timings));
    }
    private static double Elapsed(Stopwatch clock){clock.Stop();return Math.Round(clock.Elapsed.TotalMilliseconds,3);}
    private static CollectionDefinition Collection(string id,CollectionType type,int order)=>new(id,id,null,type,null,null,null,order,false,SmartRuleMatchMode.MatchAll,DateTime.UtcNow,DateTime.UtcNow);
    private static LibraryItem Item(int index){string id=$"item-{index}";LaunchTarget target=new(LaunchType.Executable,Environment.ProcessPath){Id=$"target-{index}",LibraryItemId=id,ProviderId=index%2==0?"steam":"emulation"};return new(){Id=id,Title=$"Title {index:D5}",SortTitle=$"Title {index:D5}",ContentType=ContentType.Game,Favorite=index%3==0,AddedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow,DefaultLaunchTargetId=target.Id,LaunchTargets=[target]};}
}
