namespace NexaStation.TextInput.Layouts;

public sealed record TextInputSettings(string PreferredLayoutId = "en-qwerty", bool SuggestionsEnabled = true, bool SearchHistoryEnabled = false, int MaximumSuggestions = 8, DateTimeOffset? UpdatedAtUtc = null);

public interface ITextInputSettingsStore
{
    Task<TextInputSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(TextInputSettings settings, CancellationToken cancellationToken = default);
}
