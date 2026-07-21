namespace NexaStation.Domain;

public sealed record ApplicationSession(
    NavigationSection CurrentSection,
    string? SelectedItemId,
    int CurrentRow,
    double ScrollPosition,
    bool IsLaunchInProgress);
