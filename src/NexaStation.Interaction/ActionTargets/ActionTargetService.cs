using NexaStation.Core;

namespace NexaStation.Interaction.ActionTargets;

public sealed class ActionTargetService : IActionTargetService
{
    private readonly object _gate = new();
    private ActionTargetSnapshot? _current;

    public ActionTargetSnapshot? Current { get { lock (_gate) return _current; } }
    public event EventHandler<ActionTargetChangedEventArgs>? TargetChanged;

    public void SetFocusedTarget(ActionTargetSnapshot target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.PageId) || string.IsNullOrWhiteSpace(target.ScopeId) || string.IsNullOrWhiteSpace(target.ItemId) || string.IsNullOrWhiteSpace(target.ItemType))
            throw new ArgumentException("Action targets require stable page, scope, item ID, and item type values.", nameof(target));
        if (string.Equals(target.VisualFocusState, "PointerHover", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Pointer hover cannot become the action target.", nameof(target));
        ActionTargetSnapshot? previous;
        lock (_gate) { previous = _current; _current = target; }
        if (previous != target) TargetChanged?.Invoke(this, new(previous, target));
    }

    public void ClearFocusedTarget(string scopeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        ActionTargetSnapshot? previous = null;
        lock (_gate)
        {
            if (_current is null || !string.Equals(_current.ScopeId, scopeId, StringComparison.Ordinal)) return;
            previous = _current;
            _current = null;
        }
        TargetChanged?.Invoke(this, new(previous, null));
    }

    public ActionTargetResolution Resolve(InputAction action, ActionTargetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ActionTargetSnapshot? target = Current;
        if (target is null) return new(ActionTargetResolutionStatus.NoTarget, Message: "No actionable item currently has focus.");
        if (!string.Equals(target.PageId, context.PageId, StringComparison.Ordinal) || !string.Equals(target.ScopeId, context.ScopeId, StringComparison.Ordinal))
            return new(ActionTargetResolutionStatus.ScopeMismatch, Message: "The focused target belongs to a different page or scope.");
        if (!target.IsStillAvailable) return new(ActionTargetResolutionStatus.Stale, Message: "The focused item is no longer available.");
        if (context.RequiresSelection) return new(ActionTargetResolutionStatus.SelectionRequired, Message: "This command requires an explicit selection.");
        if (!target.SupportedActions.Contains(action)) return new(ActionTargetResolutionStatus.UnsupportedAction, target, "The focused item does not support this action.");
        return new(ActionTargetResolutionStatus.Resolved, target);
    }
}

public sealed class PrimaryActionRouter : IPrimaryActionRouter, IPrimaryActionRegistration
{
    private readonly Dictionary<string, Func<InputAction, ActionTargetSnapshot, CancellationToken, Task<PrimaryActionResult>>> _handlers = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly HashSet<string> _executing = new(StringComparer.Ordinal);

    public void Register(string itemType, Func<InputAction, ActionTargetSnapshot, CancellationToken, Task<PrimaryActionResult>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemType);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate) _handlers[itemType] = handler;
    }

    public async Task<PrimaryActionResult> ExecuteAsync(InputAction action, ActionTargetSnapshot target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.IsStillAvailable) return new(false, "The focused item is no longer available.");
        if (!target.SupportedActions.Contains(action)) return new(false, "The focused item does not support this action.");
        Func<InputAction, ActionTargetSnapshot, CancellationToken, Task<PrimaryActionResult>>? handler;
        string executionKey = $"{target.ScopeId}\u001f{target.ItemType}\u001f{target.ItemId}\u001f{action}";
        lock (_gate)
        {
            if (!_handlers.TryGetValue(target.ItemType, out handler)) return new(false, "No action handler is registered for the focused item type.");
            if (!_executing.Add(executionKey)) return new(false, "This action is already running.");
        }
        try { return await handler(action, target, cancellationToken).ConfigureAwait(false); }
        finally { lock (_gate) _executing.Remove(executionKey); }
    }
}
