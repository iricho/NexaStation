using Microsoft.UI.Xaml;
using NexaStation.UI.Navigation;
using NexaStation.UI.Settings;

namespace NexaStation.UI.Themes;

public sealed record ThemeDescriptor(
    string Id, string DisplayName, string Description, Version Version, string Author,
    bool BuiltIn, bool SupportsReducedMotion, bool SupportsHighContrast, bool SupportsTouch,
    bool SupportsCompactLayout, bool SupportsCrossbarNavigation, string PreviewAssetKey,
    Version MinimumNexaStationVersion, string NavigationStyle, string RecommendedInput,
    string RelativePerformance);

public sealed record ThemeResourceBundle(IReadOnlyList<Func<ResourceDictionary>> DictionaryFactories, IReadOnlyList<string> AssetKeys)
{
    public IReadOnlyList<ResourceDictionary> CreateDictionaries() => DictionaryFactories.Select(factory => factory()).ToArray();
}
public sealed record ThemeValidationIssue(string Code, string Message, bool IsError = true);
public sealed record ThemeValidationResult(bool IsValid, IReadOnlyList<ThemeValidationIssue> Issues)
{
    public static ThemeValidationResult Valid { get; } = new(true, []);
}

public sealed record ThemeShellContext(
    ThemeNavigationState NavigationState,
    bool ReducedMotion,
    bool HighContrast,
    bool CompactLayout,
    Action<NavigationDestination>? Navigate = null,
    Action? OpenQuickMenu = null,
    Action? Search = null);

public sealed record SettingsPresentationContext(
    ISettingsCatalog Catalog,
    ISettingsSearchService Search,
    Func<string, Task> OpenSettingAsync,
    Func<string, Task> ResetCategoryAsync,
    Func<Task> CloseAsync,
    string? InitialSettingId = null);

public interface INexaTheme
{
    ThemeDescriptor Descriptor { get; }
    FrameworkElement CreateShell(ThemeShellContext context);
    FrameworkElement CreateSettingsView(SettingsPresentationContext context);
    IReadOnlyDictionary<NavigationDestination, Type> GetPageMappings();
    ThemeResourceBundle GetResources();
    ThemeValidationResult Validate();
}

public interface IThemePageRegistry
{
    Type Resolve(string themeId, NavigationDestination destination);
    bool TryResolve(string themeId, NavigationDestination destination, out Type? pageType);
    IReadOnlyDictionary<NavigationDestination, Type> GetMappings(string themeId);
}

public sealed class ThemePageRegistry(IEnumerable<INexaTheme> themes) : IThemePageRegistry
{
    private readonly Dictionary<string, IReadOnlyDictionary<NavigationDestination, Type>> _mappings = themes.ToDictionary(theme => theme.Descriptor.Id, theme => theme.GetPageMappings(), StringComparer.Ordinal);
    public Type Resolve(string themeId, NavigationDestination destination) => TryResolve(themeId, destination, out Type? type) ? type! : throw new KeyNotFoundException($"Theme '{themeId}' does not map destination '{destination}'.");
    public bool TryResolve(string themeId, NavigationDestination destination, out Type? pageType) { pageType = null; return _mappings.TryGetValue(themeId, out IReadOnlyDictionary<NavigationDestination, Type>? pages) && pages.TryGetValue(destination, out pageType); }
    public IReadOnlyDictionary<NavigationDestination, Type> GetMappings(string themeId) => _mappings.TryGetValue(themeId, out IReadOnlyDictionary<NavigationDestination, Type>? value) ? value : new Dictionary<NavigationDestination, Type>();
}

public sealed class ThemeChangedEventArgs(string previousThemeId, string themeId, bool isPreview, ThemeNavigationState restoredState) : EventArgs
{
    public string PreviousThemeId { get; }=previousThemeId;
    public string ThemeId { get; }=themeId;
    public bool IsPreview { get; }=isPreview;
    public ThemeNavigationState RestoredState { get; }=restoredState;
}
public sealed record ThemeChangeResult(bool Succeeded, string ActiveThemeId, FrameworkElement? Shell = null, string? Error = null, bool FellBackToSafeTheme = false);

public interface IThemeStateStore
{
    Task<string?> GetActiveThemeIdAsync(CancellationToken cancellationToken = default);
    Task<string?> GetLastHealthyThemeIdAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(string activeThemeId, string lastHealthyThemeId, CancellationToken cancellationToken = default);
}

public interface IThemeRuntimeContext { bool IsSafeMode { get; } }
public sealed class DefaultThemeRuntimeContext : IThemeRuntimeContext { public bool IsSafeMode { get; init; } }

public interface IThemeManager
{
    string ActiveThemeId { get; }
    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    FrameworkElement CreateSettingsView(SettingsPresentationContext context);
    Task<ThemeChangeResult> InitializeAsync(CancellationToken cancellationToken = default);
    Task<ThemeChangeResult> PreviewAsync(string themeId, CancellationToken cancellationToken = default);
    Task<ThemeChangeResult> ApplyAsync(string themeId, CancellationToken cancellationToken = default);
    Task CancelPreviewAsync(CancellationToken cancellationToken = default);
    Task<ThemeChangeResult> RestoreSafeThemeAsync(CancellationToken cancellationToken = default);
}
