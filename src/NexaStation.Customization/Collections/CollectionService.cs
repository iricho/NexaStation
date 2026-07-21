using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Collections;

public sealed class CollectionService(ICustomizationStore store):ICollectionService
{
    private const int MaximumDepth=5;
    public Task<IReadOnlyList<CollectionDefinition>> GetAsync(CancellationToken cancellationToken=default)=>store.GetCollectionsAsync(cancellationToken);
    public Task<CollectionDefinition?> GetByIdAsync(string id,CancellationToken cancellationToken=default)=>store.GetCollectionAsync(id,cancellationToken);
    public async Task<CollectionDefinition> SaveAsync(CollectionDefinition collection,CancellationToken cancellationToken=default)
    {
        if(string.IsNullOrWhiteSpace(collection.Name))throw new ArgumentException("Collection name is required.",nameof(collection));if(collection.ParentCollectionId==collection.Id)throw new InvalidOperationException("A collection cannot contain itself.");
        if(collection.ParentCollectionId is not null){HashSet<string> seen=[collection.Id];string? current=collection.ParentCollectionId;int depth=0;while(current is not null){if(!seen.Add(current))throw new InvalidOperationException("Collection nesting would create a cycle.");if(++depth>=MaximumDepth)throw new InvalidOperationException($"Collections may be nested at most {MaximumDepth} levels.");current=(await store.GetCollectionAsync(current,cancellationToken))?.ParentCollectionId;}}
        DateTime now=DateTime.UtcNow;CollectionDefinition saved=collection with{Id=string.IsNullOrWhiteSpace(collection.Id)?Guid.NewGuid().ToString("N"):collection.Id,UpdatedAtUtc=now,CreatedAtUtc=collection.CreatedAtUtc==default?now:collection.CreatedAtUtc};await store.SaveCollectionAsync(saved,cancellationToken);return saved;
    }
    public Task DeleteAsync(string id,CancellationToken cancellationToken=default)=>store.DeleteCollectionAsync(id,cancellationToken);
    public async Task AddItemAsync(CollectionItem item,CancellationToken cancellationToken=default)
    {
        if(item.TargetType==CollectionTargetType.Collection){if(item.CollectionId==item.TargetId)throw new InvalidOperationException("A collection cannot contain itself.");if(await ReachesAsync(item.TargetId,item.CollectionId,0,new HashSet<string>(),cancellationToken))throw new InvalidOperationException("Adding this collection would create a recursive cycle.");}
        IReadOnlyList<CollectionItem> current=await store.GetCollectionItemsAsync(item.CollectionId,cancellationToken);if(current.Any(x=>x.TargetType==item.TargetType&&x.TargetId==item.TargetId))return;await store.SaveCollectionItemAsync(item with{SortOrder=current.Count,AddedAtUtc=item.AddedAtUtc==default?DateTime.UtcNow:item.AddedAtUtc},cancellationToken);
    }
    public Task RemoveItemAsync(string collectionId,CollectionTargetType targetType,string targetId,CancellationToken cancellationToken=default)=>store.DeleteCollectionItemAsync(collectionId,targetType,targetId,cancellationToken);
    public Task<IReadOnlyList<CollectionItem>> GetItemsAsync(string collectionId,CancellationToken cancellationToken=default)=>store.GetCollectionItemsAsync(collectionId,cancellationToken);
    public Task ReorderItemsAsync(string collectionId,IReadOnlyList<(CollectionTargetType Type,string Id)> targets,CancellationToken cancellationToken=default)=>store.ReplaceCollectionItemOrderAsync(collectionId,targets,cancellationToken);
    public Task<IReadOnlyList<SmartCollectionRule>> GetRulesAsync(string collectionId,CancellationToken cancellationToken=default)=>store.GetRulesAsync(collectionId,cancellationToken);
    public Task SetRulesAsync(string collectionId,IReadOnlyList<SmartCollectionRule> rules,CancellationToken cancellationToken=default)=>store.ReplaceRulesAsync(collectionId,rules,cancellationToken);
    private async Task<bool> ReachesAsync(string start,string target,int depth,HashSet<string> visited,CancellationToken token){if(start==target)return true;if(depth>=MaximumDepth||!visited.Add(start))return false;foreach(CollectionItem child in (await store.GetCollectionItemsAsync(start,token)).Where(x=>x.TargetType==CollectionTargetType.Collection))if(await ReachesAsync(child.TargetId,target,depth+1,visited,token))return true;return false;}
}
