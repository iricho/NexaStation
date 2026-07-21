namespace NexaStation.UI.Navigation;

public enum NavigationDestination
{
    Home, Games, Apps, Media, Gallery, Library, Collections, Settings, Search, Details,
    ContextMenu, QuickMenu, Onboarding, Recovery, SafeMode, MediaPlayer, ArtworkSearch,
    MatchReview, EmulatorDiscovery, ActivityTimeline, Updates, About, Privacy,
    DiagnosticsExport, ExitToDesktop
}

public sealed record ThemeNavigationState(
    NavigationDestination Destination,
    string? SectionId = null,
    string? FocusedItemId = null,
    double ScrollPosition = 0,
    string? SecondaryCategoryId = null,
    string? OpenPanelId = null);

public interface IThemeNavigationStateService
{
    ThemeNavigationState Capture();
    Task RestoreAsync(ThemeNavigationState state, CancellationToken cancellationToken = default);
}

public sealed class DefaultThemeNavigationStateService : IThemeNavigationStateService
{
    private ThemeNavigationState _state = new(NavigationDestination.Home);
    public ThemeNavigationState Capture() => _state;
    public Task RestoreAsync(ThemeNavigationState state, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _state = state; return Task.CompletedTask; }
}
