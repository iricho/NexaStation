using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Domain;

namespace NexaStation.Customization.Tests;

internal sealed class MemorySettings:ISettingsService
{
    public Dictionary<string,object?> Values{get;}=[];
    public Task<T> GetAsync<T>(string key,T defaultValue,CancellationToken cancellationToken=default)=>Task.FromResult(Values.TryGetValue(key,out object? value)&&value is T typed?typed:defaultValue);
    public Task SetAsync<T>(string key,T value,CancellationToken cancellationToken=default){Values[key]=value;return Task.CompletedTask;}
}

internal sealed class MemoryCustomizationStore:ICustomizationStore
{
    public Dictionary<string,DashboardConfiguration> Dashboards{get;}=[];public List<DashboardRowConfiguration> Rows{get;}=[];public List<PinnedItem> Pins{get;}=[];public List<CollectionDefinition> Collections{get;}=[];public List<CollectionItem> Items{get;}=[];public List<SmartCollectionRule> Rules{get;}=[];public List<ThemeDefinition> Themes{get;}=[];public List<string> Backups{get;}=[];
    public Task<DashboardConfiguration?> GetDashboardAsync(string layoutId,CancellationToken token=default)=>Task.FromResult(Dashboards.Values.FirstOrDefault(x=>x.LayoutId==layoutId));
    public Task SaveDashboardAsync(DashboardConfiguration value,CancellationToken token=default){Dashboards[value.Id]=value;return Task.CompletedTask;}
    public Task<IReadOnlyList<DashboardRowConfiguration>> GetRowsAsync(string dashboardId,CancellationToken token=default)=>Task.FromResult<IReadOnlyList<DashboardRowConfiguration>>(Rows.Where(x=>x.DashboardConfigurationId==dashboardId).OrderBy(x=>x.SortOrder).ToArray());
    public Task ReplaceRowsAsync(string dashboardId,IReadOnlyList<DashboardRowConfiguration> values,CancellationToken token=default){Rows.RemoveAll(x=>x.DashboardConfigurationId==dashboardId);Rows.AddRange(values);return Task.CompletedTask;}
    public Task<IReadOnlyList<PinnedItem>> GetPinsAsync(PinLocation location,CancellationToken token=default)=>Task.FromResult<IReadOnlyList<PinnedItem>>(Pins.Where(x=>x.PinLocation==location).OrderBy(x=>x.SortOrder).ToArray());
    public Task SavePinAsync(PinnedItem value,CancellationToken token=default){Pins.RemoveAll(x=>x.Id==value.Id);Pins.Add(value);return Task.CompletedTask;}
    public Task DeletePinAsync(string id,CancellationToken token=default){Pins.RemoveAll(x=>x.Id==id);return Task.CompletedTask;}
    public Task ReplacePinOrderAsync(PinLocation location,IReadOnlyList<string> ids,CancellationToken token=default){for(int index=0;index<ids.Count;index++){int found=Pins.FindIndex(x=>x.Id==ids[index]&&x.PinLocation==location);Pins[found]=Pins[found] with{SortOrder=index};}return Task.CompletedTask;}
    public Task<IReadOnlyList<CollectionDefinition>> GetCollectionsAsync(CancellationToken token=default)=>Task.FromResult<IReadOnlyList<CollectionDefinition>>(Collections.OrderBy(x=>x.SortOrder).ToArray());
    public Task<CollectionDefinition?> GetCollectionAsync(string id,CancellationToken token=default)=>Task.FromResult(Collections.FirstOrDefault(x=>x.Id==id));
    public Task SaveCollectionAsync(CollectionDefinition value,CancellationToken token=default){Collections.RemoveAll(x=>x.Id==value.Id);Collections.Add(value);return Task.CompletedTask;}
    public Task DeleteCollectionAsync(string id,CancellationToken token=default){Collections.RemoveAll(x=>x.Id==id);Items.RemoveAll(x=>x.CollectionId==id);Rules.RemoveAll(x=>x.CollectionId==id);return Task.CompletedTask;}
    public Task<IReadOnlyList<CollectionItem>> GetCollectionItemsAsync(string id,CancellationToken token=default)=>Task.FromResult<IReadOnlyList<CollectionItem>>(Items.Where(x=>x.CollectionId==id).OrderBy(x=>x.SortOrder).ToArray());
    public Task SaveCollectionItemAsync(CollectionItem value,CancellationToken token=default){Items.RemoveAll(x=>x.CollectionId==value.CollectionId&&x.TargetType==value.TargetType&&x.TargetId==value.TargetId);Items.Add(value);return Task.CompletedTask;}
    public Task DeleteCollectionItemAsync(string collectionId,CollectionTargetType type,string targetId,CancellationToken token=default){Items.RemoveAll(x=>x.CollectionId==collectionId&&x.TargetType==type&&x.TargetId==targetId);return Task.CompletedTask;}
    public Task ReplaceCollectionItemOrderAsync(string collectionId,IReadOnlyList<(CollectionTargetType Type,string Id)> targets,CancellationToken token=default){for(int index=0;index<targets.Count;index++){int found=Items.FindIndex(x=>x.CollectionId==collectionId&&x.TargetType==targets[index].Type&&x.TargetId==targets[index].Id);Items[found]=Items[found] with{SortOrder=index};}return Task.CompletedTask;}
    public Task<IReadOnlyList<SmartCollectionRule>> GetRulesAsync(string collectionId,CancellationToken token=default)=>Task.FromResult<IReadOnlyList<SmartCollectionRule>>(Rules.Where(x=>x.CollectionId==collectionId).OrderBy(x=>x.SortOrder).ToArray());
    public Task ReplaceRulesAsync(string collectionId,IReadOnlyList<SmartCollectionRule> values,CancellationToken token=default){Rules.RemoveAll(x=>x.CollectionId==collectionId);Rules.AddRange(values);return Task.CompletedTask;}
    public Task<IReadOnlyList<ThemeDefinition>> GetCustomThemesAsync(CancellationToken token=default)=>Task.FromResult<IReadOnlyList<ThemeDefinition>>(Themes);
    public Task SaveThemeAsync(ThemeDefinition value,CancellationToken token=default){Themes.RemoveAll(x=>x.Id==value.Id);Themes.Add(value);return Task.CompletedTask;}
    public Task<string> SaveExportBackupAsync(string json,CancellationToken token=default){Backups.Add(json);return Task.FromResult($"backup-{Backups.Count}");}
    public Task ApplySnapshotAsync(InterfaceConfigurationSnapshot snapshot,CancellationToken token=default){Dashboards.Clear();foreach(DashboardConfiguration value in snapshot.Dashboards)Dashboards[value.Id]=value;Rows.Clear();Rows.AddRange(snapshot.Rows);Pins.Clear();Pins.AddRange(snapshot.Pins);Collections.Clear();Collections.AddRange(snapshot.Collections);Items.Clear();Items.AddRange(snapshot.CollectionItems);Rules.Clear();Rules.AddRange(snapshot.SmartRules);return Task.CompletedTask;}
    public Task ClearInterfaceCustomizationAsync(CancellationToken token=default){Dashboards.Clear();Rows.Clear();Pins.Clear();Collections.Clear();Items.Clear();Rules.Clear();Themes.Clear();return Task.CompletedTask;}
}

internal sealed class MemoryLibrary:ILibraryRepository
{
    public List<LibraryItem> Items{get;}=[];
    public Task<IReadOnlyList<LibraryItem>> QueryAsync(LibraryQuery query,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<LibraryItem>>(Items.Take(query.MaximumResults).ToArray());
    public Task<LibraryItem?> GetByIdAsync(string id,CancellationToken cancellationToken=default)=>Task.FromResult(Items.FirstOrDefault(x=>x.Id==id));public Task AddAsync(LibraryItem item,CancellationToken cancellationToken=default){Items.Add(item);return Task.CompletedTask;}public Task UpdateAsync(LibraryItem item,CancellationToken cancellationToken=default){Items.RemoveAll(x=>x.Id==item.Id);Items.Add(item);return Task.CompletedTask;}public Task DeleteAsync(string id,CancellationToken cancellationToken=default){Items.RemoveAll(x=>x.Id==id);return Task.CompletedTask;}public Task SetFavoriteAsync(string id,bool favorite,CancellationToken cancellationToken=default)=>Task.CompletedTask;public Task SetHiddenAsync(string id,bool hidden,CancellationToken cancellationToken=default)=>Task.CompletedTask;public Task<LibraryItem?> FindDuplicateAsync(LaunchTarget target,CancellationToken cancellationToken=default)=>Task.FromResult<LibraryItem?>(null);
}

internal sealed class MemorySoundPlayer:IUiSoundPlayer
{
    public List<string> Played{get;}=[];public bool ThrowMissing{get;set;}
    public Task PlayPackagedAsync(string assetKey,double volume,CancellationToken cancellationToken=default){if(ThrowMissing)throw new FileNotFoundException();Played.Add(assetKey);return Task.CompletedTask;}
}
