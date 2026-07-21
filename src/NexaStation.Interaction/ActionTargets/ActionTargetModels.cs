using NexaStation.Core;

namespace NexaStation.Interaction.ActionTargets;

public sealed record ActionTargetSnapshot(
    string PageId,
    string ScopeId,
    string? SectionId,
    string? RowId,
    string ItemId,
    string ItemType,
    object? DataContext,
    IReadOnlySet<InputAction> SupportedActions,
    DateTimeOffset FocusTimestamp,
    string VisualFocusState,
    bool IsStillAvailable = true);

public sealed record ActionTargetContext(string PageId, string ScopeId, string? SectionId = null, bool RequiresSelection = false);

public enum ActionTargetResolutionStatus { Resolved, NoTarget, ScopeMismatch, Stale, UnsupportedAction, SelectionRequired }

public sealed record ActionTargetResolution(ActionTargetResolutionStatus Status, ActionTargetSnapshot? Target = null, string? Message = null)
{
    public bool Succeeded => Status == ActionTargetResolutionStatus.Resolved && Target is not null;
}

public sealed class ActionTargetChangedEventArgs(ActionTargetSnapshot? previous, ActionTargetSnapshot? current) : EventArgs
{
    public ActionTargetSnapshot? Previous { get; } = previous;
    public ActionTargetSnapshot? Current { get; } = current;
}

public interface IActionTargetService
{
    ActionTargetSnapshot? Current { get; }
    event EventHandler<ActionTargetChangedEventArgs>? TargetChanged;
    void SetFocusedTarget(ActionTargetSnapshot target);
    void ClearFocusedTarget(string scopeId);
    ActionTargetResolution Resolve(InputAction action, ActionTargetContext context);
}

public sealed record PrimaryActionResult(bool Succeeded, string? Message = null);

public interface IPrimaryActionRouter
{
    Task<PrimaryActionResult> ExecuteAsync(InputAction action, ActionTargetSnapshot target, CancellationToken cancellationToken = default);
}

public interface IPrimaryActionRegistration
{
    void Register(string itemType, Func<InputAction, ActionTargetSnapshot, CancellationToken, Task<PrimaryActionResult>> handler);
}
