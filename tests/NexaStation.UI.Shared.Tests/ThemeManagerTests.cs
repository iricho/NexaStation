using Microsoft.UI.Xaml;
using NexaStation.UI.Navigation;
using NexaStation.UI.Settings;
using NexaStation.UI.Themes;
using Xunit;

namespace NexaStation.UI.Contracts.Tests;

public sealed class ThemeManagerTests
{
    [Fact]
    public async Task InitializeRestoresPersistedThemeAndNavigation()
    {
        FakeStore store = new() { Active = "other" }; FakeNavigation navigation = new();
        ThemeManager manager = Create(store, navigation, false, new FakeTheme("other"));
        ThemeChangeResult result = await manager.InitializeAsync();
        Assert.True(result.Succeeded); Assert.Equal("other", manager.ActiveThemeId); Assert.Equal(1, navigation.RestoreCount);
    }

    [Fact]
    public async Task UnknownPersistedThemeFallsBackToSafeTheme()
    {
        FakeStore store = new() { Active = "missing" };
        ThemeChangeResult result = await Create(store, new(), false).InitializeAsync();
        Assert.True(result.Succeeded); Assert.True(result.FellBackToSafeTheme); Assert.Equal(ThemeManager.SafeThemeId, result.ActiveThemeId);
    }

    [Fact]
    public async Task InvalidThemeFallsBackToSafeTheme()
    {
        FakeTheme invalid = new("invalid") { IsValid = false };
        ThemeChangeResult result = await Create(new(), new(), false, invalid).ApplyAsync(invalid.Descriptor.Id);
        Assert.True(result.Succeeded); Assert.True(result.FellBackToSafeTheme);
    }

    [Fact]
    public async Task ShellCreationFailureFallsBackToSafeTheme()
    {
        FakeTheme broken = new("broken") { ThrowOnCreate = true };
        ThemeChangeResult result = await Create(new(), new(), false, broken).ApplyAsync(broken.Descriptor.Id);
        Assert.True(result.Succeeded); Assert.True(result.FellBackToSafeTheme);
    }

    [Fact]
    public async Task PreviewDoesNotPersistAndCancelRestoresOrigin()
    {
        FakeStore store = new(); ThemeManager manager = Create(store, new(), false, new FakeTheme("other"));
        await manager.InitializeAsync(); int writes = store.SaveCount;
        await manager.PreviewAsync("other"); await manager.CancelPreviewAsync();
        Assert.Equal(ThemeManager.SafeThemeId, manager.ActiveThemeId); Assert.Equal(writes, store.SaveCount);
    }

    [Fact]
    public async Task ApplyPersistsSelectedHealthyTheme()
    {
        FakeStore store = new(); ThemeManager manager = Create(store, new(), false, new FakeTheme("other"));
        ThemeChangeResult result = await manager.ApplyAsync("other");
        Assert.True(result.Succeeded); Assert.Equal("other", store.Active); Assert.Equal("other", store.LastHealthy);
    }

    [Fact]
    public async Task SafeModeAlwaysForcesSafeTheme()
    {
        FakeStore store = new() { Active = "other" }; ThemeManager manager = Create(store, new(), true, new FakeTheme("other"));
        await manager.InitializeAsync(); await manager.ApplyAsync("other");
        Assert.Equal(ThemeManager.SafeThemeId, manager.ActiveThemeId);
    }

    private static ThemeManager Create(FakeStore store, FakeNavigation navigation, bool safeMode, params INexaTheme[] themes)
        => new([new FakeTheme(ThemeManager.SafeThemeId), .. themes], store, navigation, new FakeRuntime(safeMode));

    private sealed class FakeTheme(string id) : INexaTheme
    {
        public bool IsValid { get; init; } = true; public bool ThrowOnCreate { get; init; }
        public ThemeDescriptor Descriptor { get; } = new(id, id, id, new(1, 0), "test", true, true, true, true, true, true, id, new(0, 1), "test", "test", "test");
        public FrameworkElement CreateShell(ThemeShellContext context) { if (ThrowOnCreate) throw new InvalidOperationException("broken theme"); return null!; }
        public FrameworkElement CreateSettingsView(SettingsPresentationContext context) => null!;
        public IReadOnlyDictionary<NavigationDestination, Type> GetPageMappings() => new Dictionary<NavigationDestination, Type>();
        public ThemeResourceBundle GetResources() => null!;
        public ThemeValidationResult Validate() => IsValid ? ThemeValidationResult.Valid : new(false, [new("invalid", "invalid theme")]);
    }

    private sealed class FakeStore : IThemeStateStore
    {
        public string? Active { get; set; } public string? LastHealthy { get; private set; } public int SaveCount { get; private set; }
        public Task<string?> GetActiveThemeIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);
        public Task<string?> GetLastHealthyThemeIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(LastHealthy);
        public Task SaveAsync(string activeThemeId, string lastHealthyThemeId, CancellationToken cancellationToken = default) { Active = activeThemeId; LastHealthy = lastHealthyThemeId; SaveCount++; return Task.CompletedTask; }
    }

    private sealed class FakeNavigation : IThemeNavigationStateService
    {
        public int RestoreCount { get; private set; } public ThemeNavigationState Capture() => new(NavigationDestination.Games, "installed", "game-1", 42);
        public Task RestoreAsync(ThemeNavigationState state, CancellationToken cancellationToken = default) { RestoreCount++; return Task.CompletedTask; }
    }

    private sealed record FakeRuntime(bool IsSafeMode) : IThemeRuntimeContext;
}
