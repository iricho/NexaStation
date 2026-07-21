using NexaStation.UI.Navigation;

namespace NexaStation.UI.Settings;

public enum SettingControlType { Toggle, Choice, Slider, Number, Text, Password, Folder, Action, Navigation, Summary }
public enum SettingSaveBehavior { Immediate, Staged }
public enum SettingVisualState { Normal, Focused, Disabled, Hovered, Modified, RestartRequired, Error, Experimental, Recommended, Dangerous }

public sealed record SettingValidationRule(string Kind, double? Minimum = null, double? Maximum = null, string? Pattern = null, string? Message = null);
public sealed record SettingsCategoryDescriptor(string Id, string Title, string Description, string IconGlyph, int SortOrder, bool Advanced = false, bool Dangerous = false);
public sealed record SettingDescriptor(
    string Id, string CategoryId, string GroupId, string Title, string Description,
    IReadOnlyList<string> Keywords, SettingControlType ControlType, string? ValueBinding,
    object? DefaultValue, SettingValidationRule? Validation, bool RestartRequired,
    bool Experimental, bool Dangerous, bool Advanced, string? AvailableWhen,
    string? HelpDestination, int SortOrder, NavigationDestination Destination = NavigationDestination.Settings,
    SettingSaveBehavior SaveBehavior = SettingSaveBehavior.Immediate, bool Recommended = false);

public sealed record SettingsValidationIssue(string Code, string Message, string? DescriptorId = null);
public sealed record SettingsCatalogValidationResult(bool IsValid, IReadOnlyList<SettingsValidationIssue> Issues);

public interface ISettingsCatalog
{
    IReadOnlyList<SettingsCategoryDescriptor> Categories { get; }
    IReadOnlyList<SettingDescriptor> Settings { get; }
    SettingsCatalogValidationResult Validate();
    SettingDescriptor GetById(string settingId);
    IReadOnlyList<SettingDescriptor> GetCategory(string categoryId);
}

public sealed record SettingsSearchResult(
    string SettingId, string CategoryId, string GroupId, string Title, string Description,
    IReadOnlyList<string> Keywords, string Breadcrumb, NavigationDestination Destination,
    bool Experimental, bool RestartRequired);

public interface ISettingsSearchService
{
    Task<IReadOnlyList<SettingsSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
