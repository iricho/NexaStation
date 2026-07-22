using NexaStation.Domain;

namespace NexaStation.Core;

public enum ItemContextTargetType
{
    LibraryItem,
    EmulatedGame,
    Application,
    GalleryMedia,
    Collection,
    DiscoverApplication,
    StreamingService,
}

public enum ItemActionGroup
{
    Primary,
    Launch,
    Manage,
    Artwork,
    Organization,
    Compatibility,
    FileSystem,
    Provider,
    Privacy,
    Destructive,
}

public enum ItemActionExecutionStatus { Accepted, Disabled, NotFound, Cancelled, Failed }

public sealed record ItemContextRequest
{
    public required ItemContextTargetType TargetType { get; init; }
    public required string TargetId { get; init; }
    public required string Title { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsHidden { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool IsProviderManaged { get; init; }
    public bool IsGuest { get; init; }
    public string? ProviderId { get; init; }
    public string? SourcePath { get; init; }
    public ContentLifecycleStatus LifecycleStatus { get; init; }
    public IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ItemActionDescriptor
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public string IconKey { get; init; } = "Options";
    public InputAction SemanticAction { get; init; } = InputAction.ContextMenu;
    public ItemActionGroup Group { get; init; }
    public int SortOrder { get; init; }
    public bool Enabled { get; init; } = true;
    public string? DisabledReason { get; init; }
    public bool Visible { get; init; } = true;
    public bool RequiresConfirmation { get; init; }
    public bool RequiresHold { get; init; }
    public bool IsDestructive { get; init; }
    public bool SupportsCancellation { get; init; }
    public string? ShortcutHint { get; init; }
    public InputAction? AdaptiveInputAction { get; init; }
    public IReadOnlyList<ItemActionDescriptor> ChildActions { get; init; } = [];
    public IReadOnlyDictionary<string, string> ExecutionData { get; init; } = new Dictionary<string, string>();
}

public sealed record ItemActionExecutionRequest(
    ItemContextRequest Context,
    string ActionId,
    IReadOnlyDictionary<string, string>? ExecutionData = null);

public sealed record ItemActionExecutionResult(
    ItemActionExecutionStatus Status,
    string ActionId,
    string? Message = null,
    ItemActionDescriptor? Action = null);

public interface IItemActionProvider
{
    int Priority { get; }
    bool CanProvideActions(ItemContextRequest request);
    Task<IReadOnlyList<ItemActionDescriptor>> GetActionsAsync(ItemContextRequest request, CancellationToken cancellationToken = default);
}

public interface IItemContextMenuService
{
    Task<IReadOnlyList<ItemActionDescriptor>> BuildAsync(ItemContextRequest request, CancellationToken cancellationToken = default);
    Task<ItemActionExecutionResult> ExecuteAsync(ItemActionExecutionRequest request, CancellationToken cancellationToken = default);
}

public sealed class ItemContextMenuService(IEnumerable<IItemActionProvider> providers) : IItemContextMenuService
{
    private readonly IItemActionProvider[] _providers = providers.OrderBy(provider => provider.Priority).ToArray();

    public async Task<IReadOnlyList<ItemActionDescriptor>> BuildAsync(ItemContextRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<ItemActionDescriptor> actions = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (IItemActionProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!provider.CanProvideActions(request)) continue;
            foreach (ItemActionDescriptor action in await provider.GetActionsAsync(request, cancellationToken).ConfigureAwait(false))
            {
                Validate(action, child: false);
                if (action.Visible && ids.Add(action.Id)) actions.Add(action);
            }
        }
        return actions.OrderBy(action => action.Group).ThenBy(action => action.SortOrder).ThenBy(action => action.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public async Task<ItemActionExecutionResult> ExecuteAsync(ItemActionExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ItemActionDescriptor? action = Flatten(await BuildAsync(request.Context, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => string.Equals(candidate.Id, request.ActionId, StringComparison.Ordinal));
        if (action is null) return new(ItemActionExecutionStatus.NotFound, request.ActionId, "This action is not available for the selected item.");
        if (!action.Enabled) return new(ItemActionExecutionStatus.Disabled, action.Id, action.DisabledReason ?? "This action is currently unavailable.", action);
        return new(ItemActionExecutionStatus.Accepted, action.Id, Action: action);
    }

    private static IEnumerable<ItemActionDescriptor> Flatten(IEnumerable<ItemActionDescriptor> actions)
    {
        foreach (ItemActionDescriptor action in actions)
        {
            yield return action;
            foreach (ItemActionDescriptor child in action.ChildActions) yield return child;
        }
    }

    private static void Validate(ItemActionDescriptor action, bool child)
    {
        if (string.IsNullOrWhiteSpace(action.Id) || string.IsNullOrWhiteSpace(action.Label)) throw new InvalidOperationException("Item actions require a stable ID and label.");
        if (child && action.ChildActions.Count != 0) throw new InvalidOperationException("Context menus support only one child-action level.");
        if (action.IsDestructive && action.Group != ItemActionGroup.Destructive) throw new InvalidOperationException("Destructive actions must be in the Destructive group.");
        if (!action.Enabled && string.IsNullOrWhiteSpace(action.DisabledReason)) throw new InvalidOperationException("Disabled actions require a reason.");
        foreach (ItemActionDescriptor nested in action.ChildActions) Validate(nested, child: true);
    }
}

public static class ItemActionCapabilities
{
    public const string Launch = "launch";
    public const string Details = "details";
    public const string Edit = "edit";
    public const string Artwork = "artwork";
    public const string OpenLocation = "open-location";
    public const string ManageInstallation = "manage-installation";
    public const string Collections = "collections";
    public const string Compatibility = "compatibility";
    public const string SetBackground = "set-background";
    public const string PreferredPlayer = "preferred-player";
    public const string EmulatorConfiguration = "emulator-configuration";
    public const string NativeMediaPlayer = "native-media-player";
    public const string OpenWith = "open-with";
    public const string PlaybackProgress = "playback-progress";
    public const string LifecycleStatus = "lifecycle-status";
}
