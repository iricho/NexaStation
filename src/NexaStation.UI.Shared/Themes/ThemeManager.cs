using Microsoft.UI.Xaml;
using NexaStation.UI.Navigation;
using NexaStation.UI.Settings;

namespace NexaStation.UI.Themes;

public sealed class ThemeManager : IThemeManager
{
    public const string SafeThemeId = "nexa.default";
    private readonly IReadOnlyDictionary<string, INexaTheme> _themes;
    private readonly IThemeStateStore _store;
    private readonly IThemeNavigationStateService _navigation;
    private readonly IThemeRuntimeContext _runtime;
    private string? _previewOrigin;
    private FrameworkElement? _activeShell;

    public ThemeManager(IEnumerable<INexaTheme> themes, IThemeStateStore store, IThemeNavigationStateService navigation, IThemeRuntimeContext runtime)
    {
        _themes = themes.ToDictionary(theme => theme.Descriptor.Id, StringComparer.Ordinal);
        _store = store; _navigation = navigation; _runtime = runtime;
        if (!_themes.ContainsKey(SafeThemeId)) throw new InvalidOperationException("The built-in safe Nexa theme is required.");
    }

    public string ActiveThemeId { get; private set; } = SafeThemeId;
    public IReadOnlyList<ThemeDescriptor> AvailableThemes => _themes.Values.Select(theme => theme.Descriptor).OrderBy(theme => theme.Id, StringComparer.Ordinal).ToArray();
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    public FrameworkElement CreateSettingsView(SettingsPresentationContext context)=>_themes[ActiveThemeId].CreateSettingsView(context);

    public async Task<ThemeChangeResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        string requested = _runtime.IsSafeMode ? SafeThemeId : await _store.GetActiveThemeIdAsync(cancellationToken).ConfigureAwait(false) ?? SafeThemeId;
        ThemeChangeResult result = await ChangeAsync(requested, false, true, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result : await RestoreSafeThemeAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThemeChangeResult> PreviewAsync(string themeId, CancellationToken cancellationToken = default)
    {
        _previewOrigin ??= ActiveThemeId;
        return await ChangeAsync(themeId, true, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThemeChangeResult> ApplyAsync(string themeId, CancellationToken cancellationToken = default)
    {
        ThemeChangeResult result = await ChangeAsync(themeId, false, true, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded) _previewOrigin = null;
        return result;
    }

    public async Task CancelPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (_previewOrigin is null) return;
        string origin = _previewOrigin; _previewOrigin = null;
        await ChangeAsync(origin, false, false, cancellationToken).ConfigureAwait(false);
    }

    public Task<ThemeChangeResult> RestoreSafeThemeAsync(CancellationToken cancellationToken = default) => ChangeAsync(SafeThemeId, false, true, cancellationToken, true);

    private async Task<ThemeChangeResult> ChangeAsync(string themeId, bool preview, bool persist, CancellationToken cancellationToken, bool fallback = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_runtime.IsSafeMode) themeId = SafeThemeId;
        if (!_themes.TryGetValue(themeId, out INexaTheme? theme)) return themeId == SafeThemeId ? new(false, ActiveThemeId, Error: "The safe theme is unavailable.") : await ChangeAsync(SafeThemeId, false, persist, cancellationToken, true).ConfigureAwait(false);
        ThemeValidationResult validation = theme.Validate();
        if (!validation.IsValid) return themeId == SafeThemeId ? new(false, ActiveThemeId, Error: string.Join(' ', validation.Issues.Select(issue => issue.Message))) : await ChangeAsync(SafeThemeId, false, persist, cancellationToken, true).ConfigureAwait(false);
        ThemeNavigationState state = _navigation.Capture();
        try
        {
            FrameworkElement shell = theme.CreateShell(new(state, false, false, false));
            string previous = ActiveThemeId; FrameworkElement? inactive = _activeShell;
            _activeShell = shell; ActiveThemeId = themeId;
            await _navigation.RestoreAsync(state, cancellationToken).ConfigureAwait(false);
            if (persist) await _store.SaveAsync(themeId, themeId, cancellationToken).ConfigureAwait(false);
            if (inactive is IDisposable disposable) disposable.Dispose();
            ThemeChanged?.Invoke(this, new(previous, themeId, preview, state));
            return new(true, themeId, shell, FellBackToSafeTheme: fallback);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or TypeInitializationException)
        {
            return themeId == SafeThemeId ? new(false, ActiveThemeId, Error: exception.Message) : await ChangeAsync(SafeThemeId, false, persist, cancellationToken, true).ConfigureAwait(false);
        }
    }
}
