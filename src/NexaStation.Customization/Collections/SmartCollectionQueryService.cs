using System.Globalization;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Customization.Models;
using NexaStation.Customization.Services;
using NexaStation.Domain;

namespace NexaStation.Customization.Collections;

public sealed class SmartCollectionQueryService(ICustomizationStore store,ILibraryRepository library):ISmartCollectionQueryService
{
    private readonly Dictionary<string,(DateTime Expires,IReadOnlyList<CollectionResolvedItem> Items)> _cache=[];
    public async Task<IReadOnlyList<CollectionResolvedItem>> EvaluateAsync(string collectionId,int maximumResults,CancellationToken cancellationToken=default)
    {
        maximumResults=Math.Clamp(maximumResults,1,500);if(_cache.TryGetValue(collectionId,out var cached)&&cached.Expires>DateTime.UtcNow)return cached.Items.Take(maximumResults).ToArray();
        CollectionDefinition collection=await store.GetCollectionAsync(collectionId,cancellationToken)??throw new KeyNotFoundException("Smart collection was not found.");IReadOnlyList<SmartCollectionRule> rules=await store.GetRulesAsync(collectionId,cancellationToken);SmartCollectionValidationResult validation=await ValidateAsync(new(collection,rules),cancellationToken);if(!validation.Valid)throw new InvalidOperationException(string.Join(" ",validation.Errors));
        IReadOnlyList<LibraryItem> candidates=await library.QueryAsync(new(IncludeHidden:true,MaximumResults:Math.Min(5000,maximumResults*20)),cancellationToken);bool explicitlyFiltersHidden=rules.Any(x=>x.Field==SmartRuleField.Hidden);IEnumerable<LibraryItem> eligible=explicitlyFiltersHidden?candidates:candidates.Where(x=>!x.Hidden);IEnumerable<LibraryItem> matched=eligible.Where(item=>collection.MatchMode==SmartRuleMatchMode.MatchAll?rules.All(rule=>Matches(item,rule)):rules.Any(rule=>Matches(item,rule)));CollectionResolvedItem[] result=matched.OrderBy(x=>x.SortTitle,StringComparer.CurrentCultureIgnoreCase).Take(maximumResults).Select(x=>new CollectionResolvedItem(CollectionTargetType.LibraryItem,x.Id,x.Title,x.LaunchTarget is not null,x)).ToArray();_cache[collectionId]=(DateTime.UtcNow.AddSeconds(15),result);return result;
    }
    public Task<SmartCollectionValidationResult> ValidateAsync(SmartCollectionDefinition definition,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();List<string> errors=[];if(definition.Collection.CollectionType!=CollectionType.Smart)errors.Add("The collection is not a smart collection.");foreach(SmartCollectionRule rule in definition.Rules){if(!Enum.IsDefined(rule.Field))errors.Add($"Unknown rule field {rule.Field}.");if(!Enum.IsDefined(rule.Operator))errors.Add($"Unknown rule operator {rule.Operator}.");if(rule.Operator is not (SmartRuleOperator.IsTrue or SmartRuleOperator.IsFalse)&&string.IsNullOrWhiteSpace(rule.ValueJson))errors.Add($"Rule {rule.Id} requires a value.");}return Task.FromResult(new SmartCollectionValidationResult(errors.Count==0,errors));
    }
    public void Invalidate()=>_cache.Clear();
    private static bool Matches(LibraryItem item,SmartCollectionRule rule)
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
