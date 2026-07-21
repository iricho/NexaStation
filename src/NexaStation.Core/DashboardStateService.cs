using NexaStation.Domain;

namespace NexaStation.Core;

public sealed class DashboardStateService : IDashboardStateService
{
    private readonly Dictionary<NavigationSection, string?> _selections = [];

    public NavigationSection CurrentSection { get; set; } = NavigationSection.Home;

    public int CurrentRow { get; set; }

    public double ScrollPosition { get; set; }

    public bool IsLaunchInProgress { get; set; }

    public string? GetSelectedItem(NavigationSection section) =>
        _selections.TryGetValue(section, out string? itemId) ? itemId : null;

    public void SetSelectedItem(NavigationSection section, string? itemId) => _selections[section] = itemId;
}
