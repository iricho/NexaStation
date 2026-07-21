using NexaStation.Core;
using NexaStation.Interaction.ActionTargets;
using Xunit;

namespace NexaStation.Interaction.Tests;

public sealed class ActionTargetTests
{
    private static ActionTargetSnapshot Target(string id = "item-1", string type = "Game", bool available = true) =>
        new("Dashboard", "content", "Games", "row", id, type, new object(), new HashSet<InputAction> { InputAction.Confirm, InputAction.ContextMenu, InputAction.Options }, DateTimeOffset.UtcNow, "ControllerFocus", available);

    [Theory]
    [InlineData("Game")]
    [InlineData("Application")]
    [InlineData("GalleryMedia")]
    [InlineData("Video")]
    [InlineData("Collection")]
    [InlineData("DiscoverApplication")]
    public void Focused_card_becomes_action_target(string type)
    {
        ActionTargetService service = new();
        service.SetFocusedTarget(Target(type: type));
        ActionTargetResolution resolution = service.Resolve(InputAction.Confirm, new("Dashboard", "content", "Games"));
        Assert.True(resolution.Succeeded);
        Assert.Equal(type, resolution.Target!.ItemType);
    }

    [Fact] public void Hover_cannot_override_controller_target()
    {
        ActionTargetService service = new(); service.SetFocusedTarget(Target());
        Assert.Throws<ArgumentException>(() => service.SetFocusedTarget(Target("hover") with { VisualFocusState = "PointerHover" }));
        Assert.Equal("item-1", service.Current!.ItemId);
    }

    [Fact] public void Recycled_card_updates_stable_identity()
    {
        ActionTargetService service = new(); service.SetFocusedTarget(Target("old")); service.SetFocusedTarget(Target("new"));
        Assert.Equal("new", service.Current!.ItemId);
    }

    [Fact] public void Removed_card_is_rejected()
    {
        ActionTargetService service = new(); service.SetFocusedTarget(Target(available: false));
        Assert.Equal(ActionTargetResolutionStatus.Stale, service.Resolve(InputAction.Confirm, new("Dashboard", "content")).Status);
    }

    [Fact] public async Task Router_executes_once_while_action_is_in_flight()
    {
        PrimaryActionRouter router = new(); TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously); int calls = 0;
        router.Register("Game", async (_, _, _) => { Interlocked.Increment(ref calls); await release.Task; return new(true); });
        Task<PrimaryActionResult> first = router.ExecuteAsync(InputAction.Confirm, Target());
        PrimaryActionResult second = await router.ExecuteAsync(InputAction.Confirm, Target());
        Assert.False(second.Succeeded); Assert.Equal(1, calls); release.SetResult(); Assert.True((await first).Succeeded);
    }
}
