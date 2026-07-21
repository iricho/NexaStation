namespace NexaStation.Core;

public sealed class SelectionStateService : ISelectionStateService
{
    public string? SelectedItemId { get; set; }

    public string? SelectedSectionId { get; set; }
}
