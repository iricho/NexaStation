using NexaStation.Core;
using NexaStation.Customization.Services;
using NexaStation.Domain;
using NexaStation.TextInput.Suggestions;

namespace NexaStation.Infrastructure.Search;

public sealed class SqliteLocalSuggestionSource(ILibraryRepository library, ICustomizationStore customization) : ILocalSuggestionSource
{
    public async Task<IReadOnlyList<LocalSuggestionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LibraryItem> items = await library.QueryAsync(new(IncludeHidden: false, MaximumResults: 5000), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<NexaStation.Customization.Models.CollectionDefinition> collections = await customization.GetCollectionsAsync(cancellationToken).ConfigureAwait(false);
        List<LocalSuggestionCandidate> result = new(items.Count * 2 + collections.Count);
        foreach (LibraryItem item in items)
        {
            result.Add(new(item.Title, item.ContentType == ContentType.Application ? LocalSuggestionKind.ApplicationTitle : LocalSuggestionKind.LibraryTitle, StableId: item.Id));
            if (!string.IsNullOrWhiteSpace(item.Subtitle)) result.Add(new(item.Subtitle, LocalSuggestionKind.Platform));
            foreach (string provider in item.LaunchTargets.Select(target => target.ProviderId).OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase)) result.Add(new(provider, LocalSuggestionKind.Provider));
        }
        result.AddRange(collections.Select(collection => new LocalSuggestionCandidate(collection.Name, LocalSuggestionKind.Collection, StableId: collection.Id)));
        return result;
    }
}
