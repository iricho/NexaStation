namespace NexaStation.Core;

public interface ISelectionStateService
{
    string? SelectedItemId { get; set; }

    string? SelectedSectionId { get; set; }
}
