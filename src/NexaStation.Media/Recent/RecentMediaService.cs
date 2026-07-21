using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Media.Hub;

namespace NexaStation.Media.Recent;

public sealed class RecentMediaService(IMediaPersistence persistence,IGalleryPersistence gallery):IRecentMediaService
{
    public async Task RecordOpenedAsync(RecentMediaEntry entry,CancellationToken cancellationToken=default){if(entry.MediaItemId is not null){GalleryMediaItem? item=await gallery.GetMediaItemAsync(entry.MediaItemId,cancellationToken);if(item?.SourcePrivacyLevel==GalleryPrivacyLevel.Private)return;}await persistence.AddRecentAsync(entry,cancellationToken);}
    public Task<IReadOnlyList<RecentMediaEntry>> GetRecentAsync(int maximumResults,CancellationToken cancellationToken=default)=>persistence.GetRecentAsync(Math.Clamp(maximumResults,1,100),cancellationToken);
}
