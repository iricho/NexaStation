using System.Globalization;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Domain;

namespace NexaStation.Customization.Collections;

public sealed class SmartCollectionQueryService(ICustomizationStore store,ILibraryRepository library,IGalleryPersistence? gallery=null,IContentLifecycleService? lifecycle=null):ISmartCollectionQueryService
{
    private static readonly HashSet<SmartRuleField> SupportedFields = [SmartRuleField.ContentType,SmartRuleField.Provider,SmartRuleField.Platform,SmartRuleField.Favorite,SmartRuleField.Hidden,SmartRuleField.Installed,SmartRuleField.Available,SmartRuleField.RecentlyPlayed,SmartRuleField.RecentlyOpened,SmartRuleField.AddedDate,SmartRuleField.LastActivityDate,SmartRuleField.Playtime,SmartRuleField.ApplicationCategory,SmartRuleField.MediaType,SmartRuleField.GallerySourceKind,SmartRuleField.Emulator,SmartRuleField.Title,SmartRuleField.LifecycleStatus];
    private readonly Dictionary<string,(DateTime Expires,IReadOnlyList<CollectionResolvedItem> Items,int Maximum)> _cache=[];
    public async Task<IReadOnlyList<CollectionResolvedItem>> EvaluateAsync(string collectionId,int maximumResults,CancellationToken cancellationToken=default)
    {
        maximumResults=Math.Clamp(maximumResults,1,500);if(_cache.TryGetValue(collectionId,out var cached)&&cached.Expires>DateTime.UtcNow&&(cached.Items.Count<cached.Maximum||maximumResults<=cached.Maximum))return cached.Items.Take(maximumResults).ToArray();
        CollectionDefinition collection=await store.GetCollectionAsync(collectionId,cancellationToken)??throw new KeyNotFoundException("Dynamic Shelf was not found.");IReadOnlyList<SmartCollectionRule> rules=await store.GetRulesAsync(collectionId,cancellationToken);IReadOnlyList<CollectionResolvedItem> result=await EvaluateDefinitionAsync(new(collection,rules),maximumResults,cancellationToken);_cache[collectionId]=(DateTime.UtcNow.AddSeconds(15),result,maximumResults);return result;
    }
    public Task<IReadOnlyList<CollectionResolvedItem>> PreviewAsync(SmartCollectionDefinition definition,int maximumResults=100,CancellationToken cancellationToken=default)=>EvaluateDefinitionAsync(definition,Math.Clamp(maximumResults,1,100),cancellationToken);
    private async Task<IReadOnlyList<CollectionResolvedItem>> EvaluateDefinitionAsync(SmartCollectionDefinition definition,int maximumResults,CancellationToken cancellationToken)
    {
        CollectionDefinition collection=definition.Collection;IReadOnlyList<SmartCollectionRule> rules=definition.Rules;SmartCollectionValidationResult validation=await ValidateAsync(definition,cancellationToken);if(!validation.Valid)throw new InvalidOperationException(string.Join(" ",validation.Errors));
        bool explicitlyFiltersHidden=rules.Any(x=>x.Field==SmartRuleField.Hidden);List<CollectionResolvedItem> matches=[];Dictionary<string,ContentLifecycleStatus> libraryStatuses=lifecycle is null?[]:(await lifecycle.QueryAsync(new(ContentLifecycleTargetType.LibraryItem,MaximumResults:10000),cancellationToken)).ToDictionary(entry=>entry.Target.TargetId,entry=>entry.Status,StringComparer.Ordinal);Dictionary<string,ContentLifecycleStatus> galleryStatuses=lifecycle is null?[]:(await lifecycle.QueryAsync(new(ContentLifecycleTargetType.GalleryMediaItem,MaximumResults:10000),cancellationToken)).ToDictionary(entry=>entry.Target.TargetId,entry=>entry.Status,StringComparer.Ordinal);
        IReadOnlyList<LibraryItem> candidates=await library.QueryAsync(new(IncludeHidden:true,MaximumResults:Math.Min(5000,maximumResults*20)),cancellationToken);
        foreach(LibraryItem item in explicitlyFiltersHidden?candidates:candidates.Where(x=>!x.Hidden))
        {
            ContentLifecycleStatus status=libraryStatuses.GetValueOrDefault(item.Id,ContentLifecycleStatus.Unassigned);
            if(IsMatch(collection,rules,rule=>Matches(item,status,rule)))matches.Add(new(CollectionTargetType.LibraryItem,item.Id,item.Title,item.LaunchTarget is not null,item));
        }
        if(gallery is not null)
        {
            IReadOnlyList<GalleryMediaItem> media=await gallery.QueryAsync(new(IncludeHidden:true,IncludeUnavailable:true,MaximumResults:Math.Min(5000,maximumResults*20)),cancellationToken);
            foreach(GalleryMediaItem item in explicitlyFiltersHidden?media:media.Where(x=>!x.Hidden))
            {
                ContentLifecycleStatus status=galleryStatuses.GetValueOrDefault(item.Id,ContentLifecycleStatus.Unassigned);
                if(IsMatch(collection,rules,rule=>Matches(item,status,rule)))matches.Add(new(CollectionTargetType.GalleryMediaItem,item.Id,item.FileName,item.Available,GalleryMediaItem:item));
            }
        }
        return matches.OrderBy(x=>x.DisplayName,StringComparer.CurrentCultureIgnoreCase).Take(maximumResults).ToArray();
    }
    public Task<SmartCollectionValidationResult> ValidateAsync(SmartCollectionDefinition definition,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();List<string> errors=[];if(definition.Collection.CollectionType!=CollectionType.Smart)errors.Add("This is not a Dynamic Shelf.");if(definition.Rules.Count==0)errors.Add("Add at least one rule.");foreach(SmartCollectionRule rule in definition.Rules){if(!Enum.IsDefined(rule.Field))errors.Add($"Unknown rule field {rule.Field}.");else if(!SupportedFields.Contains(rule.Field))errors.Add($"{rule.Field} is not supported by Dynamic Shelves yet.");if(!Enum.IsDefined(rule.Operator))errors.Add($"Unknown rule operator {rule.Operator}.");if(rule.Operator is not (SmartRuleOperator.IsTrue or SmartRuleOperator.IsFalse)&&string.IsNullOrWhiteSpace(rule.ValueJson))errors.Add($"Rule {rule.SortOrder+1} requires a value.");}return Task.FromResult(new SmartCollectionValidationResult(errors.Count==0,errors));
    }
    public void Invalidate()=>_cache.Clear();
    public static IReadOnlyList<SmartRuleField> EditableFields => SupportedFields.OrderBy(value=>value.ToString(),StringComparer.Ordinal).ToArray();
    private static bool IsMatch(CollectionDefinition collection,IReadOnlyList<SmartCollectionRule> rules,Func<SmartCollectionRule,bool> match)=>collection.MatchMode==SmartRuleMatchMode.MatchAll?rules.All(match):rules.Any(match);
    private static bool Matches(LibraryItem item,ContentLifecycleStatus status,SmartCollectionRule rule)
    {
        string expected=ReadValue(rule.ValueJson);return rule.Field switch
        {
            SmartRuleField.ContentType=>Text(item.ContentType.ToString(),expected,rule.Operator),
            SmartRuleField.Provider=>item.LaunchTargets.Any(target=>Text(target.ProviderId??string.Empty,expected,rule.Operator)),
            SmartRuleField.Platform=>Text(item.Subtitle??string.Empty,expected,rule.Operator),
            SmartRuleField.Favorite=>Boolean(item.Favorite,rule.Operator,expected),
            SmartRuleField.Hidden=>Boolean(item.Hidden,rule.Operator,expected),
            SmartRuleField.Installed or SmartRuleField.Available=>Boolean(item.LaunchTargets.Any(target=>target.IsEnabled),rule.Operator,expected),
            SmartRuleField.RecentlyPlayed=>Boolean(item.LastLaunchedAtUtc>DateTime.UtcNow.AddDays(-30),rule.Operator,expected),
            SmartRuleField.AddedDate=>Date(item.AddedAtUtc,expected,rule.Operator),
            SmartRuleField.LastActivityDate=>item.LastLaunchedAtUtc is { } last&&Date(last,expected,rule.Operator),
            SmartRuleField.Playtime=>Number(item.TotalPlaytimeSeconds,expected,rule.Operator),
            SmartRuleField.ApplicationCategory=>Text(item.PrimaryCategoryName??string.Empty,expected,rule.Operator),
            SmartRuleField.Emulator=>item.LaunchTargets.Any(target=>Text(target.EmulatorInstallationId??string.Empty,expected,rule.Operator)),
            SmartRuleField.Title=>Text(item.Title,expected,rule.Operator),
            SmartRuleField.LifecycleStatus=>Text(status.ToString(),expected,rule.Operator),
            _=>false
        };
    }
    private static bool Matches(GalleryMediaItem item,ContentLifecycleStatus status,SmartCollectionRule rule)
    {
        string expected=ReadValue(rule.ValueJson);return rule.Field switch
        {
            SmartRuleField.ContentType=>Text(item.MediaType==GalleryMediaType.Video?"Video":"Image",expected,rule.Operator),
            SmartRuleField.Favorite=>Boolean(item.Favorite,rule.Operator,expected),
            SmartRuleField.Hidden=>Boolean(item.Hidden,rule.Operator,expected),
            SmartRuleField.Available or SmartRuleField.Installed=>Boolean(item.Available,rule.Operator,expected),
            SmartRuleField.RecentlyOpened=>Boolean(item.LastOpenedAtUtc>DateTime.UtcNow.AddDays(-30),rule.Operator,expected),
            SmartRuleField.AddedDate=>Date(item.AddedAtUtc,expected,rule.Operator),
            SmartRuleField.LastActivityDate=>item.LastOpenedAtUtc is { } last&&Date(last,expected,rule.Operator),
            SmartRuleField.MediaType=>Text(item.MediaType.ToString(),expected,rule.Operator),
            SmartRuleField.GallerySourceKind=>Text(item.SourceKind.ToString(),expected,rule.Operator),
            SmartRuleField.Title=>Text(item.FileName,expected,rule.Operator),
            SmartRuleField.LifecycleStatus=>Text(status.ToString(),expected,rule.Operator),
            _=>false
        };
    }
    private static string ReadValue(string? json){if(string.IsNullOrWhiteSpace(json))return string.Empty;try{using JsonDocument doc=JsonDocument.Parse(json);return doc.RootElement.ValueKind==JsonValueKind.String?doc.RootElement.GetString()??string.Empty:doc.RootElement.ToString();}catch(JsonException){return json.Trim('"');}}
    private static bool Text(string actual,string expected,SmartRuleOperator op){StringComparison comparison=StringComparison.OrdinalIgnoreCase;string[] values=expected.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);return op switch{SmartRuleOperator.Equals=>actual.Equals(expected,comparison),SmartRuleOperator.NotEquals=>!actual.Equals(expected,comparison),SmartRuleOperator.Contains=>actual.Contains(expected,comparison),SmartRuleOperator.StartsWith=>actual.StartsWith(expected,comparison),SmartRuleOperator.In=>values.Any(x=>actual.Equals(x,comparison)),SmartRuleOperator.NotIn=>values.All(x=>!actual.Equals(x,comparison)),_=>false};}
    private static bool Boolean(bool actual,SmartRuleOperator op,string expected)
    {
        if(op==SmartRuleOperator.IsTrue)return actual;if(op==SmartRuleOperator.IsFalse)return !actual;if(!bool.TryParse(expected,out bool parsed))return false;return op switch{SmartRuleOperator.Equals=>actual==parsed,SmartRuleOperator.NotEquals=>actual!=parsed,_=>false};
    }
    private static bool Number(long actual,string expected,SmartRuleOperator op)=>long.TryParse(expected,NumberStyles.Integer,CultureInfo.InvariantCulture,out long value)&&op switch{SmartRuleOperator.Equals=>actual==value,SmartRuleOperator.NotEquals=>actual!=value,SmartRuleOperator.GreaterThan=>actual>value,SmartRuleOperator.LessThan=>actual<value,_=>false};
    private static bool Date(DateTime actual,string expected,SmartRuleOperator op)=>DateTime.TryParse(expected,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out DateTime value)&&op switch{SmartRuleOperator.Before=>actual<value,SmartRuleOperator.After=>actual>value,SmartRuleOperator.Equals=>actual.Date==value.Date,SmartRuleOperator.NotEquals=>actual.Date!=value.Date,_=>false};
}
