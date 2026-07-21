using NexaStation.TextInput.Sessions;

namespace NexaStation.TextInput.Suggestions;

public enum LocalSuggestionKind { LibraryTitle, ApplicationTitle, Collection, Platform, Provider, GalleryAlbum, RecentSearch }
public sealed record LocalSuggestionCandidate(string Text, LocalSuggestionKind Kind, bool IsPrivate = false, string? StableId = null);

public interface ILocalSuggestionSource
{
    Task<IReadOnlyList<LocalSuggestionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default);
}

public interface ILocalSuggestionService
{
    Task<IReadOnlyList<string>> SuggestAsync(string query, SuggestionsSource sources, int maximumResults = 8, CancellationToken cancellationToken = default);
}

public sealed class LocalSuggestionService(ILocalSuggestionSource source) : ILocalSuggestionService
{
    public async Task<IReadOnlyList<string>> SuggestAsync(string query, SuggestionsSource sources, int maximumResults = 8, CancellationToken cancellationToken = default)
    {
        if (sources == SuggestionsSource.None || maximumResults <= 0) return [];
        string normalized = (query ?? string.Empty).Trim();
        IReadOnlyList<LocalSuggestionCandidate> candidates = await source.GetCandidatesAsync(cancellationToken).ConfigureAwait(false);
        return candidates.Where(candidate => !candidate.IsPrivate && Included(candidate.Kind, sources) && (normalized.Length == 0 || candidate.Text.Contains(normalized, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(candidate => candidate.Text.StartsWith(normalized, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(candidate => candidate.Text, StringComparer.CurrentCultureIgnoreCase)
            .Select(candidate => candidate.Text).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(Math.Clamp(maximumResults, 1, 20)).ToArray();
    }

    private static bool Included(LocalSuggestionKind kind, SuggestionsSource sources) => kind switch
    {
        LocalSuggestionKind.LibraryTitle => sources.HasFlag(SuggestionsSource.Library), LocalSuggestionKind.ApplicationTitle => sources.HasFlag(SuggestionsSource.Applications),
        LocalSuggestionKind.Collection => sources.HasFlag(SuggestionsSource.Collections), LocalSuggestionKind.Platform => sources.HasFlag(SuggestionsSource.Platforms),
        LocalSuggestionKind.Provider => sources.HasFlag(SuggestionsSource.Providers), LocalSuggestionKind.GalleryAlbum => sources.HasFlag(SuggestionsSource.GalleryAlbums),
        LocalSuggestionKind.RecentSearch => sources.HasFlag(SuggestionsSource.RecentSearches), _ => false,
    };
}

public sealed class EmptyLocalSuggestionSource : ILocalSuggestionSource
{
    public Task<IReadOnlyList<LocalSuggestionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<LocalSuggestionCandidate>>([]); }
}
