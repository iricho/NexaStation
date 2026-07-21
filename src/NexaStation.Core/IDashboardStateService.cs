using NexaStation.Domain;

namespace NexaStation.Core;

public interface IDashboardStateService
{
    NavigationSection CurrentSection { get; set; }

    int CurrentRow { get; set; }

    double ScrollPosition { get; set; }

    bool IsLaunchInProgress { get; set; }

    string? GetSelectedItem(NavigationSection section);

    void SetSelectedItem(NavigationSection section, string? itemId);
}
