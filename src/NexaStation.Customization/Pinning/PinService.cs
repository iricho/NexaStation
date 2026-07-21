using NexaStation.Customization.Models;
using NexaStation.Customization.Services;

namespace NexaStation.Customization.Pinning;

public sealed class PinService(ICustomizationStore store):IPinService
{
    public Task<IReadOnlyList<PinnedItem>> GetAsync(PinLocation location,CancellationToken cancellationToken=default)=>store.GetPinsAsync(location,cancellationToken);
    public async Task PinAsync(PinRequest request,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(request.TargetId))throw new ArgumentException("A pin target is required.",nameof(request));IReadOnlyList<PinnedItem> existing=await store.GetPinsAsync(request.PinLocation,cancellationToken);if(existing.Any(x=>x.TargetType==request.TargetType&&x.TargetId==request.TargetId))throw new InvalidOperationException("That target is already pinned in this location.");DateTime now=DateTime.UtcNow;await store.SavePinAsync(new(Guid.NewGuid().ToString("N"),request.TargetType,request.TargetId,request.PinLocation,request.GroupName,request.SortOrder??existing.Count,now,now,DisplayName:request.DisplayName),cancellationToken);}
    public Task UnpinAsync(string pinId,CancellationToken cancellationToken=default)=>store.DeletePinAsync(pinId,cancellationToken);
    public async Task ReorderAsync(PinReorderRequest request,CancellationToken cancellationToken=default){IReadOnlyList<PinnedItem> existing=await store.GetPinsAsync(request.Location,cancellationToken);if(request.PinIds.Count!=existing.Count||request.PinIds.Distinct(StringComparer.Ordinal).Count()!=existing.Count||request.PinIds.Any(id=>existing.All(x=>x.Id!=id)))throw new ArgumentException("The reorder request must contain every pin exactly once.",nameof(request));await store.ReplacePinOrderAsync(request.Location,request.PinIds,cancellationToken);}
}
