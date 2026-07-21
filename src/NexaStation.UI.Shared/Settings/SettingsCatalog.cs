using System.Globalization;

namespace NexaStation.UI.Settings;

public sealed class SettingsCatalog(IEnumerable<SettingsCategoryDescriptor> categories, IEnumerable<SettingDescriptor> settings) : ISettingsCatalog
{
    public IReadOnlyList<SettingsCategoryDescriptor> Categories { get; } = categories.OrderBy(value => value.SortOrder).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<SettingDescriptor> Settings { get; } = settings.OrderBy(value => value.CategoryId, StringComparer.Ordinal).ThenBy(value => value.SortOrder).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
    public SettingDescriptor GetById(string settingId) => Settings.Single(value => value.Id == settingId);
    public IReadOnlyList<SettingDescriptor> GetCategory(string categoryId) => Settings.Where(value => value.CategoryId == categoryId).OrderBy(value => value.SortOrder).ToArray();
    public SettingsCatalogValidationResult Validate()
    {
        List<SettingsValidationIssue> issues = [];
        foreach (IGrouping<string, SettingDescriptor> duplicate in Settings.GroupBy(value => value.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) issues.Add(new("DuplicateSettingId", $"Setting id '{duplicate.Key}' is duplicated.", duplicate.Key));
        HashSet<string> categoryIds = Categories.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        foreach (IGrouping<string, SettingsCategoryDescriptor> duplicate in Categories.GroupBy(value => value.Id, StringComparer.Ordinal).Where(group => group.Count() > 1)) issues.Add(new("DuplicateCategoryId", $"Category id '{duplicate.Key}' is duplicated."));
        foreach (SettingDescriptor setting in Settings)
        {
            if (!categoryIds.Contains(setting.CategoryId)) issues.Add(new("UnknownCategory", $"Setting '{setting.Id}' has no valid category.", setting.Id));
            if (string.IsNullOrWhiteSpace(setting.Title) || string.IsNullOrWhiteSpace(setting.Description)) issues.Add(new("UnlabelledSetting", $"Setting '{setting.Id}' lacks user-facing metadata.", setting.Id));
            if (setting.ControlType is not SettingControlType.Action and not SettingControlType.Navigation and not SettingControlType.Summary && string.IsNullOrWhiteSpace(setting.ValueBinding)) issues.Add(new("DeadSetting", $"Setting '{setting.Id}' has no value binding.", setting.Id));
            if (setting.Dangerous && Categories.FirstOrDefault(value => value.Id == setting.CategoryId)?.Dangerous != true) issues.Add(new("DangerousSettingPlacement", $"Dangerous setting '{setting.Id}' is outside the reset-and-recovery category.", setting.Id));
        }
        return new(issues.Count == 0, issues);
    }
}

public sealed class SettingsSearchService(ISettingsCatalog catalog) : ISettingsSearchService
{
    public Task<IReadOnlyList<SettingsSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); string normalized = query.Trim(); if (normalized.Length == 0) return Task.FromResult<IReadOnlyList<SettingsSearchResult>>([]);
        string[] terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Dictionary<string, SettingsCategoryDescriptor> categories = catalog.Categories.ToDictionary(value => value.Id, StringComparer.Ordinal);
        IReadOnlyList<SettingsSearchResult> result = catalog.Settings.Select(setting => (Setting: setting, Score: Score(setting, categories[setting.CategoryId], terms))).Where(value => value.Score > 0).OrderByDescending(value => value.Score).ThenBy(value => value.Setting.Title, StringComparer.CurrentCultureIgnoreCase).Select(value => new SettingsSearchResult(value.Setting.Id, value.Setting.CategoryId, value.Setting.GroupId, value.Setting.Title, value.Setting.Description, value.Setting.Keywords, $"Settings › {categories[value.Setting.CategoryId].Title} › {value.Setting.Title}", value.Setting.Destination, value.Setting.Experimental, value.Setting.RestartRequired)).ToArray();
        return Task.FromResult(result);
    }

    private static int Score(SettingDescriptor setting, SettingsCategoryDescriptor category, IReadOnlyList<string> terms)
    {
        int score = 0; string title = setting.Title.ToLower(CultureInfo.CurrentCulture); string description = setting.Description.ToLower(CultureInfo.CurrentCulture); string categoryTitle = category.Title.ToLower(CultureInfo.CurrentCulture); string keywords = string.Join(' ', setting.Keywords).ToLower(CultureInfo.CurrentCulture);
        foreach (string raw in terms) { string term = raw.ToLower(CultureInfo.CurrentCulture); if (title.Contains(term, StringComparison.CurrentCulture)) score += 10; if (keywords.Contains(term, StringComparison.CurrentCulture)) score += 7; if (categoryTitle.Contains(term, StringComparison.CurrentCulture)) score += 5; if (description.Contains(term, StringComparison.CurrentCulture)) score += 3; }
        return score;
    }
}
