using NexaStation.Core;
using Xunit;

namespace NexaStation.Core.Tests;

public sealed class ItemContextMenuTests
{
    [Fact]
    public async Task LibraryActionsAreCapabilityFilteredAndRemovalIsTruthful()
    {
        ItemContextMenuService service = new([new DefaultItemActionProvider()]);
        ItemContextRequest request = Request(ItemContextTargetType.EmulatedGame) with
        {
            ProviderId = "steam",
            IsProviderManaged = true,
            Capabilities = Set(ItemActionCapabilities.Launch, ItemActionCapabilities.Details, ItemActionCapabilities.Edit, ItemActionCapabilities.Artwork, ItemActionCapabilities.Collections),
        };

        IReadOnlyList<ItemActionDescriptor> actions = await service.BuildAsync(request);

        Assert.Contains(actions, action => action.Id == "open" && action.Label == "Play");
        Assert.Contains(actions, action => action.Id == "provider.open" && action.Label == "Open in Steam");
        ItemActionDescriptor remove = Assert.Single(actions, action => action.Id == "index.remove");
        Assert.True(remove.RequiresConfirmation);
        Assert.True(remove.IsDestructive);
        Assert.Contains("ROM", remove.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(actions, action => action.Id.Contains("uninstall", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GalleryRemovalNeverClaimsToDeleteSourceFile()
    {
        ItemContextMenuService service = new([new DefaultItemActionProvider()]);
        ItemActionDescriptor remove = Assert.Single(await service.BuildAsync(Request(ItemContextTargetType.GalleryMedia)), action => action.Id == "index.remove");
        Assert.Contains("source file remains", remove.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestDoesNotReceiveMutationOrManagementActions()
    {
        ItemContextMenuService service = new([new DefaultItemActionProvider()]);
        IReadOnlyList<ItemActionDescriptor> actions = await service.BuildAsync(Request(ItemContextTargetType.Application) with { IsGuest = true, IsProviderManaged = true, ProviderId = "windows" });
        Assert.DoesNotContain(actions, action => action.Id is "edit" or "hidden.toggle" or "index.remove" or "installation.manage");
    }

    [Fact]
    public async Task ExecuteRejectsUnknownAndDisabledActions()
    {
        ItemContextMenuService service = new([new DefaultItemActionProvider()]);
        ItemContextRequest request = Request(ItemContextTargetType.LibraryItem) with { IsAvailable = false, Capabilities = Set(ItemActionCapabilities.Launch) };
        Assert.Equal(ItemActionExecutionStatus.NotFound, (await service.ExecuteAsync(new(request, "arbitrary.command"))).Status);
        ItemActionExecutionResult disabled = await service.ExecuteAsync(new(request, "open"));
        Assert.Equal(ItemActionExecutionStatus.Disabled, disabled.Status);
        Assert.False(string.IsNullOrWhiteSpace(disabled.Message));
    }

    [Fact]
    public async Task DuplicateIdsAreDeterministicallyDeduplicated()
    {
        ItemContextMenuService service = new([new DuplicateProvider(1), new DuplicateProvider(2)]);
        Assert.Single(await service.BuildAsync(Request(ItemContextTargetType.LibraryItem)));
    }

    [Fact]
    public async Task MultipleLaunchTargetsCreateOneLevelSafeChildActions()
    {
        ItemContextMenuService service = new([new DefaultItemActionProvider()]);
        ItemContextRequest request=Request(ItemContextTargetType.LibraryItem) with
        {
            Capabilities=Set(ItemActionCapabilities.Launch),
            Options=new Dictionary<string,string>{{"launchTarget.native","Native"},{"launchTarget.streaming","Remote Play"}},
        };
        ItemActionDescriptor playWith=Assert.Single(await service.BuildAsync(request),action=>action.Id=="launch.play-with");
        Assert.Equal(2,playWith.ChildActions.Count);
        Assert.All(playWith.ChildActions,child=>Assert.Empty(child.ChildActions));
        Assert.All(playWith.ChildActions,child=>Assert.True(child.ExecutionData.ContainsKey("launchTargetId")));
    }

    [Fact]
    public async Task MissingProviderTargetHasReadableDisabledReason()
    {
        ItemContextMenuService service=new([new DefaultItemActionProvider()]);
        ItemContextRequest request=Request(ItemContextTargetType.LibraryItem) with{IsProviderManaged=true,ProviderId="steam",IsAvailable=false};
        ItemActionDescriptor provider=Assert.Single(await service.BuildAsync(request),action=>action.Id=="provider.open");
        Assert.False(provider.Enabled);
        Assert.False(string.IsNullOrWhiteSpace(provider.DisabledReason));
    }

    [Fact]
    public async Task ApplicationManagementIsNavigationOnlyAndConfirmed()
    {
        ItemContextMenuService service=new([new DefaultItemActionProvider()]);
        ItemActionDescriptor manage=Assert.Single(await service.BuildAsync(Request(ItemContextTargetType.Application)),action=>action.Id=="installation.manage");
        Assert.True(manage.RequiresConfirmation);
        Assert.False(manage.IsDestructive);
        Assert.Contains("never runs an uninstall",manage.Description,StringComparison.OrdinalIgnoreCase);
    }

    private static ItemContextRequest Request(ItemContextTargetType type) => new()
    {
        TargetType = type,
        TargetId = "item-1",
        Title = "Example",
        Capabilities = Set(ItemActionCapabilities.Details, ItemActionCapabilities.OpenLocation, ItemActionCapabilities.ManageInstallation),
    };

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);

    private sealed class DuplicateProvider(int priority) : IItemActionProvider
    {
        public int Priority => priority;
        public bool CanProvideActions(ItemContextRequest request) => true;
        public Task<IReadOnlyList<ItemActionDescriptor>> GetActionsAsync(ItemContextRequest request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ItemActionDescriptor>>([new() { Id = "same", Label = $"Provider {priority}", Group = ItemActionGroup.Primary }]);
    }
}
