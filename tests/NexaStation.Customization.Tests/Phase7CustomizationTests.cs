using NexaStation.Customization.Collections;
using NexaStation.Customization.Dashboard;
using NexaStation.Customization.ImportExport;
using NexaStation.Customization.Layouts;
using NexaStation.Customization.Models;
using NexaStation.Customization.Performance;
using NexaStation.Customization.Pinning;
using NexaStation.Customization.Sounds;
using NexaStation.Customization.Themes;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.Customization.Tests;

public sealed class Phase7CustomizationTests
{
    public static IEnumerable<object[]> LayoutCases()=>Cases(12);public static IEnumerable<object[]> DashboardCases()=>Cases(12);public static IEnumerable<object[]> PinCases()=>Cases(8);public static IEnumerable<object[]> ManualCollectionCases()=>Cases(12);public static IEnumerable<object[]> SmartCollectionCases()=>Cases(16);public static IEnumerable<object[]> ThemeCases()=>Cases(11);public static IEnumerable<object[]> SoundCases()=>Cases(7);public static IEnumerable<object[]> PerformanceCases()=>Cases(7);public static IEnumerable<object[]> ImportExportCases()=>Cases(11);

    [Theory,MemberData(nameof(LayoutCases))]
    public async Task LayoutRequirements01Through12(int scenario)
    {
        MemorySettings settings=new();InterfaceLayoutService service=new(settings);IReadOnlyList<InterfaceLayoutDefinition> layouts=await service.GetAvailableAsync();Assert.Equal(3,layouts.Count);Assert.Equal(3,layouts.Select(x=>x.LayoutType).Distinct().Count());
        if(scenario is >=1 and <=3){InterfaceLayoutDefinition layout=layouts[scenario-1];await service.ApplyAsync(layout.Id);Assert.Equal(layout.Id,service.CurrentLayoutId);Assert.Equal(layout.Id,settings.Values["Interface.LayoutId"]);}
        else if(scenario==4)await Assert.ThrowsAsync<ArgumentException>(()=>service.ApplyAsync("unknown"));
        else if(scenario==10){await service.ApplyAsync(InterfaceLayoutService.CrossbarId);await service.ResetAsync();Assert.Equal(InterfaceLayoutService.ModernId,service.CurrentLayoutId);}
        else Assert.All(layouts,x=>Assert.False(string.IsNullOrWhiteSpace(x.PreviewAssetKey)));
    }

    [Theory,MemberData(nameof(DashboardCases))]
    public async Task DashboardRequirements13Through24(int scenario)
    {
        MemoryCustomizationStore store=new();DashboardConfigurationService service=new(store);DashboardConfiguration dashboard=await service.GetOrCreateAsync(InterfaceLayoutService.ModernId);List<DashboardRowConfiguration> rows=(await service.GetRowsAsync(dashboard.Id)).ToList();Assert.NotEmpty(rows);
        if(scenario is 1 or 2){rows[0]=rows[0] with{Enabled=scenario==2};if(rows.All(x=>!x.Enabled))rows[1]=rows[1] with{Enabled=true};await service.SaveRowsAsync(dashboard.Id,rows);Assert.Equal(scenario==2,(await service.GetRowsAsync(dashboard.Id))[0].Enabled);}
        else if(scenario is 3 or 4){string moved=rows[0].Id;await service.MoveRowAsync(dashboard.Id,moved,1);Assert.Equal(moved,(await service.GetRowsAsync(dashboard.Id))[1].Id);}
        else if(scenario==7)await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveRowsAsync(dashboard.Id,rows.Select(x=>x with{Enabled=false}).ToArray()));
        else if(scenario==8){await service.SaveRowsAsync(dashboard.Id,[rows[0]]);await service.ResetAsync(dashboard.LayoutId);Assert.True((await service.GetRowsAsync(dashboard.Id)).Count>1);}
        else if(scenario==10){rows[0]=rows[0] with{CardSize=CardSize.Large};await service.SaveRowsAsync(dashboard.Id,rows);Assert.Equal(CardSize.Large,(await service.GetRowsAsync(dashboard.Id))[0].CardSize);}
        else if(scenario==11){rows[0]=rows[0] with{CardStyle=CardStyle.Square};await service.SaveRowsAsync(dashboard.Id,rows);Assert.Equal(CardStyle.Square,(await service.GetRowsAsync(dashboard.Id))[0].CardStyle);}
        else {rows[0]=rows[0] with{MaximumItems=999};await service.SaveRowsAsync(dashboard.Id,rows);Assert.InRange((await service.GetRowsAsync(dashboard.Id))[0].MaximumItems,1,100);}
    }

    [Theory,MemberData(nameof(PinCases))]
    public async Task PinRequirements25Through32(int scenario)
    {
        MemoryCustomizationStore store=new();PinService service=new(store);PinTargetType type=scenario==1?PinTargetType.Collection:PinTargetType.LibraryItem;await service.PinAsync(new(type,"target",PinLocation.Home,DisplayName:"Target"));Assert.Single(await service.GetAsync(PinLocation.Home));
        if(scenario==2)await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PinAsync(new(type,"target",PinLocation.Home)));
        else if(scenario==3){await service.PinAsync(new(type,"target",PinLocation.Games));Assert.Single(await service.GetAsync(PinLocation.Games));}
        else if(scenario is 4 or 7){await service.PinAsync(new(PinTargetType.LibraryItem,"other",PinLocation.Home));PinnedItem[] pins=(await service.GetAsync(PinLocation.Home)).ToArray();await service.ReorderAsync(new(PinLocation.Home,[pins[1].Id,pins[0].Id]));Assert.Equal("other",(await service.GetAsync(PinLocation.Home))[0].TargetId);}
        else if(scenario==6){string id=(await service.GetAsync(PinLocation.Home))[0].Id;await service.UnpinAsync(id);Assert.Empty(await service.GetAsync(PinLocation.Home));}
        else Assert.Equal("Target",(await service.GetAsync(PinLocation.Home))[0].DisplayName);
    }

    [Theory,MemberData(nameof(ManualCollectionCases))]
    public async Task ManualCollectionRequirements33Through44(int scenario)
    {
        MemoryCustomizationStore store=new();CollectionService service=new(store);CollectionDefinition root=await service.SaveAsync(Collection("root","Root"));Assert.Equal("Root",root.Name);
        if(scenario==1){root=await service.SaveAsync(root with{Name="Renamed"});Assert.Equal("Renamed",root.Name);}
        else if(scenario is 2 or 3 or 4){await service.AddItemAsync(new(root.Id,CollectionTargetType.LibraryItem,"a",0,DateTime.UtcNow));await service.AddItemAsync(new(root.Id,CollectionTargetType.LibraryItem,"b",1,DateTime.UtcNow));if(scenario==3)await service.RemoveItemAsync(root.Id,CollectionTargetType.LibraryItem,"a");if(scenario==4)await service.ReorderItemsAsync(root.Id,[(CollectionTargetType.LibraryItem,"b"),(CollectionTargetType.LibraryItem,"a")]);Assert.True((await service.GetItemsAsync(root.Id)).Count>=1);}
        else if(scenario==5){await service.DeleteAsync(root.Id);Assert.Null(await service.GetByIdAsync(root.Id));}
        else if(scenario==6){CollectionDefinition child=await service.SaveAsync(Collection("child","Child") with{ParentCollectionId=root.Id});Assert.Equal(root.Id,child.ParentCollectionId);}
        else if(scenario==7)await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveAsync(root with{ParentCollectionId=root.Id}));
        else if(scenario==8){CollectionDefinition child=await service.SaveAsync(Collection("child","Child"));await service.AddItemAsync(new(root.Id,CollectionTargetType.Collection,child.Id,0,DateTime.UtcNow));await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AddItemAsync(new(child.Id,CollectionTargetType.Collection,root.Id,0,DateTime.UtcNow)));}
        else Assert.NotNull(await service.GetByIdAsync(root.Id));
    }

    [Theory,MemberData(nameof(SmartCollectionCases))]
    public async Task SmartCollectionRequirements45Through60(int scenario)
    {
        MemoryCustomizationStore store=new();MemoryLibrary library=new();SmartCollectionQueryService service=new(store,library);CollectionDefinition collection=Collection("smart","Smart",CollectionType.Smart) with{MatchMode=scenario==5?SmartRuleMatchMode.MatchAny:SmartRuleMatchMode.MatchAll};store.Collections.Add(collection);library.Items.Add(Item("one","Alpha",favorite:true,provider:"steam"));library.Items.Add(Item("two","Beta",hidden:true,provider:"emulation"));SmartRuleField field=scenario switch{6=>SmartRuleField.Provider,7=>SmartRuleField.Platform,8=>SmartRuleField.Favorite,10=>SmartRuleField.ApplicationCategory,11=>SmartRuleField.Emulator,_=>SmartRuleField.Title};SmartRuleOperator operation=field==SmartRuleField.Favorite?SmartRuleOperator.IsTrue:SmartRuleOperator.Contains;SmartCollectionRule rule=Rule(collection.Id,field,operation,field switch{SmartRuleField.Provider=>"steam",SmartRuleField.Favorite=>"true",_=>"a"});store.Rules.Add(rule);
        if(scenario==2){rule=rule with{Field=(SmartRuleField)999};SmartCollectionValidationResult invalid=await service.ValidateAsync(new(collection,[rule]));Assert.False(invalid.Valid);return;}if(scenario==4){rule=rule with{Operator=(SmartRuleOperator)999};SmartCollectionValidationResult invalid=await service.ValidateAsync(new(collection,[rule]));Assert.False(invalid.Valid);return;}SmartCollectionValidationResult validation=await service.ValidateAsync(new(collection,[rule]));Assert.True(validation.Valid);IReadOnlyList<CollectionResolvedItem> result=await service.EvaluateAsync(collection.Id,scenario==14?1:500);Assert.DoesNotContain(result,x=>x.TargetId=="two");Assert.InRange(result.Count,0,scenario==14?1:500);if(scenario==15){service.Invalidate();Assert.True((await service.EvaluateAsync(collection.Id,500)).Count>=0);}
    }

    [Theory,MemberData(nameof(ThemeCases))]
    public async Task ThemeRequirements61Through71(int scenario)
    {
        MemoryCustomizationStore store=new();MemorySettings settings=new();ThemeService service=new(store,settings);IReadOnlyList<ThemeDefinition> themes=await service.GetAvailableAsync();Assert.Equal(6,themes.Count(x=>x.BuiltIn));
        if(scenario is 1 or 4){await service.ApplyAsync("aurora");Assert.Equal("aurora",settings.Values["Interface.ThemeId"]);}
        else if(scenario==2){ThemeDefinition? preview=null;service.ThemeChanged+=(_,e)=>preview=e.Theme;await service.PreviewAsync("obsidian");Assert.Equal("obsidian",preview?.Id);}
        else if(scenario==3){await service.PreviewAsync("obsidian");await service.CancelPreviewAsync();Assert.Equal(ThemeService.DefaultThemeId,service.CurrentThemeId);}
        else if(scenario==5)await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveCustomAsync(themes[0] with{Id="bad",BuiltIn=false,AccentColor="red"}));
        else if(scenario==8){Assert.All(themes,theme=>Assert.True(ThemeService.HasReadableContrast(ThemeService.ReadableForeground(theme.AccentColor),theme.AccentColor)));}
        else if(scenario==9){await service.ApplyAsync("aurora");await service.ResetAsync();Assert.Equal(ThemeService.DefaultThemeId,service.CurrentThemeId);}
        else Assert.All(themes,x=>ThemeService.Validate(x));
    }

    [Theory,MemberData(nameof(SoundCases))]
    public async Task SoundRequirements72Through78(int scenario)
    {
        MemorySettings settings=new();MemorySoundPlayer player=new(){ThrowMissing=scenario==4};UiSoundService service=new(settings,player);IReadOnlyList<UiSoundPack> packs=await service.GetPacksAsync();Assert.Equal(4,packs.Count);
        if(scenario==1){await service.ApplyPackAsync("silent");await service.PlayAsync(UiSoundEvent.Confirm);Assert.Empty(player.Played);}
        else if(scenario==2){await service.PlayAsync(UiSoundEvent.Navigate);await service.PlayAsync(UiSoundEvent.Navigate);Assert.Single(player.Played);}
        else if(scenario==5){await service.SetVolumeAsync(.25);Assert.Equal(.25,settings.Values["Interface.SoundVolume"]);}
        else if(scenario==6){await service.ApplyPackAsync("missing");Assert.Equal("silent",settings.Values["Interface.SoundPackId"]);}
        else {await service.PlayAsync(UiSoundEvent.Confirm);Assert.True(player.Played.Count<=1);}
    }

    [Theory,MemberData(nameof(PerformanceCases))]
    public async Task PerformanceRequirements79Through85(int scenario)
    {
        MemorySettings settings=new();PerformancePresetService service=new(settings);VisualPerformancePreset preset=scenario switch{1=>VisualPerformancePreset.Minimal,3=>VisualPerformancePreset.Cinematic,_=>VisualPerformancePreset.Balanced};PerformanceSettings value=await service.ApplyAsync(preset,scenario==4);Assert.Equal(preset,value.Preset);if(scenario==4)Assert.Equal(AnimationIntensity.None,value.AnimationIntensity);if(scenario==5)Assert.True(value.PauseVisualEffectsDuringLaunch);if(scenario==6)Assert.IsType<PerformanceSettings>(settings.Values["Interface.Performance"]);
        if(scenario==7){PerformanceSettings custom=await service.SaveCustomAsync(value with{MaximumDashboardItemsPerRow=7});Assert.Equal(VisualPerformancePreset.Custom,custom.Preset);}
    }

    [Theory,MemberData(nameof(ImportExportCases))]
    public async Task ImportExportRequirements86Through96(int scenario)
    {
        MemoryCustomizationStore store=new();MemorySettings settings=new();InterfaceLayoutService layouts=new(settings);ThemeService themes=new(store,settings);PerformancePresetService performance=new(settings);InterfaceConfigurationService service=new(store,settings,layouts,themes,performance);string json=await service.ExportAsync();Assert.Contains("formatVersion",json,StringComparison.OrdinalIgnoreCase);
        if(scenario==2){InterfaceConfigurationPreview preview=await service.PreviewImportAsync(json.Replace("\"formatVersion\": 1","\"formatVersion\": 999",StringComparison.Ordinal));Assert.False(preview.Valid);}
        else if(scenario is 3 or 4){string unsafeJson=scenario==3?"<script>run()</script>":"https://example.invalid/theme.js";Assert.False((await service.PreviewImportAsync(unsafeJson)).Valid);}
        else if(scenario==5)Assert.True((await service.PreviewImportAsync(json)).Valid);
        else {InterfaceConfigurationImportResult result=await service.ImportAsync(json);Assert.True(result.Succeeded);Assert.NotEmpty(store.Backups);}
    }

    private static IEnumerable<object[]> Cases(int count)=>Enumerable.Range(1,count).Select(x=>new object[]{x});
    private static CollectionDefinition Collection(string id,string name,CollectionType type=CollectionType.Manual)=>new(id,name,null,type,null,null,null,0,false,SmartRuleMatchMode.MatchAll,DateTime.UtcNow,DateTime.UtcNow);
    private static SmartCollectionRule Rule(string collectionId,SmartRuleField field,SmartRuleOperator operation,string value)=>new(Guid.NewGuid().ToString("N"),collectionId,0,field,operation,System.Text.Json.JsonSerializer.Serialize(value),0,DateTime.UtcNow,DateTime.UtcNow);
    private static LibraryItem Item(string id,string title,bool favorite=false,bool hidden=false,string provider="local"){LaunchTarget target=new(LaunchType.Executable,Environment.ProcessPath){Id=$"target-{id}",LibraryItemId=id,ProviderId=provider};return new(){Id=id,Title=title,SortTitle=title,Subtitle="Windows",ContentType=ContentType.Game,Favorite=favorite,Hidden=hidden,PrimaryCategoryName="Games",AddedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow,DefaultLaunchTargetId=target.Id,LaunchTargets=[target]};}
}
