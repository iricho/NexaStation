using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Gallery.Albums;

public sealed class GalleryAlbumService(IGalleryPersistence persistence)
{
    public async Task<GalleryAlbum> CreateAsync(string name,string? description=null,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("An album name is required.",nameof(name));DateTime now=DateTime.UtcNow;GalleryAlbum album=new(Guid.NewGuid().ToString("N"),name.Trim(),description?.Trim(),GalleryAlbumType.Manual,null,null,0,now,now);await persistence.SaveAlbumAsync(album,cancellationToken).ConfigureAwait(false);return album;}
    public async Task RenameAsync(string id,string name,CancellationToken cancellationToken=default){GalleryAlbum album=await persistence.GetAlbumAsync(id,cancellationToken).ConfigureAwait(false)??throw new InvalidOperationException("The album no longer exists.");await persistence.SaveAlbumAsync(album with{Name=name.Trim(),UpdatedAtUtc=DateTime.UtcNow},cancellationToken).ConfigureAwait(false);}
    public Task AddAsync(string albumId,string mediaId,int sortOrder=0,CancellationToken cancellationToken=default)=>persistence.AddAlbumItemAsync(new(albumId,mediaId,sortOrder,DateTime.UtcNow),cancellationToken);
    public Task RemoveAsync(string albumId,string mediaId,CancellationToken cancellationToken=default)=>persistence.RemoveAlbumItemAsync(albumId,mediaId,cancellationToken);
    public Task DeleteAsync(string albumId,CancellationToken cancellationToken=default)=>persistence.DeleteAlbumAsync(albumId,cancellationToken);
    public Task SetCoverAsync(string albumId,string? mediaId,CancellationToken cancellationToken=default)=>persistence.SetAlbumCoverAsync(albumId,mediaId,cancellationToken);
}
