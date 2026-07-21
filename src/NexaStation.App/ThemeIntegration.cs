using NexaStation.Core;
using NexaStation.UI.Themes;

namespace NexaStation.App;

public sealed class SettingsThemeStateStore(ISettingsService settings) : IThemeStateStore
{
    public Task<string?> GetActiveThemeIdAsync(CancellationToken cancellationToken = default)=>settings.GetAsync<string?>("UI.ActiveThemeId",null,cancellationToken);
    public Task<string?> GetLastHealthyThemeIdAsync(CancellationToken cancellationToken = default)=>settings.GetAsync<string?>("UI.LastHealthyThemeId",null,cancellationToken);
    public async Task SaveAsync(string activeThemeId,string lastHealthyThemeId,CancellationToken cancellationToken=default){await settings.SetAsync("UI.ActiveThemeId",activeThemeId,cancellationToken);await settings.SetAsync("UI.LastHealthyThemeId",lastHealthyThemeId,cancellationToken);await settings.SetAsync("UI.ThemeVersion","1.0.0",cancellationToken);}
}

public sealed class AppThemeRuntimeContext : IThemeRuntimeContext { public bool IsSafeMode { get; set; } }
