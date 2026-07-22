using NexaStation.Domain;

namespace NexaStation.Core;

public sealed class DefaultItemActionProvider : IItemActionProvider
{
    public int Priority => 100;
    public bool CanProvideActions(ItemContextRequest request) => request is not null;

    public Task<IReadOnlyList<ItemActionDescriptor>> GetActionsAsync(ItemContextRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ItemActionDescriptor> actions = [];
        void Add(string id, string label, ItemActionGroup group, int order, string icon, string? description = null, bool enabled = true, string? disabled = null, bool confirm = false, bool destructive = false, InputAction? adaptive = null) =>
            actions.Add(new() { Id = id, Label = label, Group = group, SortOrder = order, IconKey = icon, Description = description, Enabled = enabled, DisabledReason = disabled, RequiresConfirmation = confirm, IsDestructive = destructive, AdaptiveInputAction = adaptive });

        bool library = request.TargetType is ItemContextTargetType.LibraryItem or ItemContextTargetType.EmulatedGame or ItemContextTargetType.Application;
        bool gallery = request.TargetType == ItemContextTargetType.GalleryMedia;
        bool collection = request.TargetType == ItemContextTargetType.Collection;
        bool discover = request.TargetType is ItemContextTargetType.DiscoverApplication or ItemContextTargetType.StreamingService;

        if (request.Capabilities.Contains(ItemActionCapabilities.Launch)) Add("open", request.TargetType is ItemContextTargetType.LibraryItem or ItemContextTargetType.EmulatedGame ? "Play" : "Open", ItemActionGroup.Primary, 0, "Play", enabled: request.IsAvailable, disabled: request.IsAvailable ? null : "The target is unavailable.", adaptive: InputAction.Confirm);
        if (request.Capabilities.Contains(ItemActionCapabilities.Details)) Add("details", "Details", ItemActionGroup.Primary, 10, "Details", adaptive: InputAction.ToggleDetails);
        if (request.Capabilities.Contains(ItemActionCapabilities.Edit) && !request.IsGuest) Add("edit", "Edit item", ItemActionGroup.Manage, 0, "Edit");

        if (library || gallery)
        {
            Add("favorite.toggle", request.IsFavorite ? "Remove from favorites" : "Add to favorites", ItemActionGroup.Organization, 0, "Favorite", adaptive: InputAction.Favorite);
            if (!request.IsGuest) Add("hidden.toggle", request.IsHidden ? "Unhide" : "Hide from library", ItemActionGroup.Privacy, 0, "Hide", request.IsHidden ? "Return this item to normal library views." : "Keep the item indexed but hide it from normal views.");
        }

        if (library && !request.IsGuest) Add("pin.home", "Pin to Home", ItemActionGroup.Organization, 0, "Pin");
        if (library && request.Capabilities.Contains(ItemActionCapabilities.Collections) && !request.IsGuest) Add("collection.add", "Add to Shelf", ItemActionGroup.Organization, 10, "Collection");
        if ((library || gallery) && request.Capabilities.Contains(ItemActionCapabilities.LifecycleStatus) && !request.IsGuest)
        {
            string backlog = gallery ? "Watch Later" : "Backlog";
            string playing = gallery ? "Watching" : "Playing";
            string completed = gallery ? "Watched" : "Completed";
            actions.Add(new()
            {
                Id = "lifecycle",
                Label = gallery ? "Video status" : "Game status",
                Group = ItemActionGroup.Organization,
                SortOrder = 20,
                IconKey = "Details",
                ChildActions =
                [
                    Status("lifecycle.unassigned", "Unassigned", ContentLifecycleStatus.Unassigned),
                    Status("lifecycle.backlog", backlog, ContentLifecycleStatus.Backlog),
                    Status("lifecycle.playing", playing, ContentLifecycleStatus.Playing),
                    Status("lifecycle.completed", completed, ContentLifecycleStatus.Completed),
                    Status("lifecycle.dropped", "Dropped", ContentLifecycleStatus.Dropped),
                ],
            });
        }
        KeyValuePair<string,string>[] launchTargets=request.Options.Where(option=>option.Key.StartsWith("launchTarget.",StringComparison.Ordinal)).ToArray();
        if(library&&launchTargets.Length>1)
        {
            actions.Add(new(){Id="launch.play-with",Label="Play With",Group=ItemActionGroup.Launch,SortOrder=0,IconKey="Play",ChildActions=launchTargets.Select((target,index)=>new ItemActionDescriptor{Id="launch.play."+target.Key[13..],Label=target.Value,Group=ItemActionGroup.Launch,SortOrder=index,IconKey="Play",ExecutionData=new Dictionary<string,string>{{"launchTargetId",target.Key[13..]}}}).ToArray()});
            if(!request.IsGuest)actions.Add(new(){Id="launch.default",Label="Set default launch target",Group=ItemActionGroup.Launch,SortOrder=10,IconKey="Settings",ChildActions=launchTargets.Select((target,index)=>new ItemActionDescriptor{Id="launch.default."+target.Key[13..],Label=target.Value,Group=ItemActionGroup.Launch,SortOrder=index,IconKey="Settings",ExecutionData=new Dictionary<string,string>{{"launchTargetId",target.Key[13..]}}}).ToArray()});
        }
        if (library && request.Capabilities.Contains(ItemActionCapabilities.Artwork) && !request.IsGuest)
        {
            actions.Add(new()
            {
                Id = "artwork",
                Label = "Artwork",
                Group = ItemActionGroup.Artwork,
                SortOrder = 0,
                IconKey = "Artwork",
                ChildActions =
                [
                    new() { Id = "artwork.find", Label = "Find artwork", Group = ItemActionGroup.Artwork, SortOrder = 0, IconKey = "Search" },
                    new() { Id = "artwork.choose", Label = "Choose local cover", Group = ItemActionGroup.Artwork, SortOrder = 10, IconKey = "Folder" },
                    new() { Id = "artwork.review", Label = "Review artwork matches", Group = ItemActionGroup.Artwork, SortOrder = 20, IconKey = "Details" },
                    new() { Id = "artwork.undo", Label = "Undo latest artwork change", Group = ItemActionGroup.Artwork, SortOrder = 30, IconKey = "Reset", RequiresConfirmation = true },
                ],
            });
        }
        if (library && request.Capabilities.Contains(ItemActionCapabilities.Compatibility)) Add("compatibility.record", "Record compatibility", ItemActionGroup.Compatibility, 0, "Compatibility");
        if(request.TargetType==ItemContextTargetType.EmulatedGame&&request.Capabilities.Contains(ItemActionCapabilities.EmulatorConfiguration)&&!request.IsGuest){Add("emulator.configure","Change emulator",ItemActionGroup.Launch,20,"Settings");Add("emulator.relink","Relink ROM",ItemActionGroup.Manage,20,"Folder");}
        if(request.TargetType==ItemContextTargetType.Application&&!request.IsGuest){Add("application.card-style","Change card style",ItemActionGroup.Manage,20,"Artwork");Add("application.category","Change category",ItemActionGroup.Organization,20,"Collection");Add("application.desktop-control","Configure desktop control",ItemActionGroup.Manage,30,"Settings");}
        if (gallery && request.Capabilities.Contains(ItemActionCapabilities.SetBackground) && !request.IsGuest) Add("gallery.background", "Set as background", ItemActionGroup.Manage, 10, "Artwork");
        if (gallery && request.Capabilities.Contains(ItemActionCapabilities.PreferredPlayer)) Add("gallery.player", "Preferred player", ItemActionGroup.Manage, 20, "Play");
        if (gallery && request.Capabilities.Contains(ItemActionCapabilities.NativeMediaPlayer))
        {
            Add("media.play.native", "Play in NexaStation", ItemActionGroup.Launch, 0, "Play");
            Add("media.resume.native", "Resume in NexaStation", ItemActionGroup.Launch, 5, "Play");
            Add("media.start.native", "Start from beginning", ItemActionGroup.Launch, 10, "Play");
        }
        if (gallery && request.Capabilities.Contains(ItemActionCapabilities.OpenWith)) Add("media.open-with", "Open With", ItemActionGroup.Launch, 20, "Play");
        if (gallery && request.Capabilities.Contains(ItemActionCapabilities.PlaybackProgress)) Add("media.progress.forget", "Forget playback progress", ItemActionGroup.Manage, 25, "Reset");
        if (request.Capabilities.Contains(ItemActionCapabilities.OpenLocation)) Add("location.open", gallery ? "Open file location" : "Open installation folder", ItemActionGroup.FileSystem, 0, "Folder", enabled: !string.IsNullOrWhiteSpace(request.SourcePath), disabled: "No available local location was found.");
        if (request.IsProviderManaged && !string.IsNullOrWhiteSpace(request.ProviderId)) Add("provider.open", $"Open in {ProviderName(request.ProviderId)}", ItemActionGroup.Provider, 0, "Provider",enabled:request.IsAvailable,disabled:request.IsAvailable?null:"The provider target is unavailable.");
        if (request.Capabilities.Contains(ItemActionCapabilities.ManageInstallation) && !request.IsGuest) Add("installation.manage", "Manage installation", ItemActionGroup.Provider, 10, "Settings", "Opens the provider or Windows Installed Apps. NexaStation never runs an uninstall command.", confirm: true);

        if (collection && !request.IsGuest)
        {
            Add("collection.pin", "Pin to Home", ItemActionGroup.Organization, 0, "Pin");
            Add("collection.home-row", "Add as Home row", ItemActionGroup.Organization, 10, "Collection");
            Add("collection.rename", "Rename Shelf", ItemActionGroup.Manage, 0, "Edit");
            Add("collection.delete", "Delete Shelf", ItemActionGroup.Destructive, 0, "Delete", "Deletes only this Shelf. Games, apps, media, albums, and files remain untouched.", confirm: true, destructive: true);
        }
        if (discover)
        {
            Add("discover.primary", request.TargetType == ItemContextTargetType.StreamingService ? "Open service" : "View in Store", ItemActionGroup.Primary, 0, "Play", adaptive: InputAction.Confirm);
            Add("discover.details", "Details", ItemActionGroup.Primary, 10, "Details");
            if (!request.IsGuest) Add("discover.hide", "Hide recommendation", ItemActionGroup.Privacy, 0, "Hide");
        }
        if ((library || gallery) && !request.IsGuest) Add("index.remove", gallery ? "Remove from Gallery index" : "Remove from NexaStation", ItemActionGroup.Destructive, 0, "Delete", gallery ? "Removes only the Gallery index entry. The source file remains on disk." : "Removes only the NexaStation library entry. The installed program, ROM, and original files remain.", confirm: true, destructive: true);
        return Task.FromResult<IReadOnlyList<ItemActionDescriptor>>(actions);
    }

    private static string ProviderName(string providerId) => providerId.ToLowerInvariant() switch { "steam" => "Steam", "epic" => "Epic Games", "gog" => "GOG", "ea" => "EA app", "ubisoft" => "Ubisoft Connect", "battlenet" => "Battle.net", "windows" or "windowsapps" => "Windows", _ => "provider" };

    private static ItemActionDescriptor Status(string id, string label, ContentLifecycleStatus status) => new()
    {
        Id = id,
        Label = label,
        Group = ItemActionGroup.Organization,
        IconKey = "Details",
        ExecutionData = new Dictionary<string, string> { ["status"] = ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture) },
    };
}
