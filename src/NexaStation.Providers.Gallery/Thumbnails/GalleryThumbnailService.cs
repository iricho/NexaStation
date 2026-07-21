using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;
using Windows.Storage;
using Windows.Storage.FileProperties;
using NexaStation.Recovery;

namespace NexaStation.Providers.Gallery.Thumbnails;

public sealed class GalleryThumbnailService(IGalleryPersistence persistence,ISettingsService settings,INexaStationPaths? paths=null) : IGalleryThumbnailService
{
    private readonly ConcurrentDictionary<string,SemaphoreSlim> _locks=new(StringComparer.Ordinal);private readonly BoundedThumbnailMemoryCache _memory=new(128L*1024*1024);
    private static readonly byte[] Placeholder=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    public string CacheRoot{get;}=(paths??new NexaStationPaths()).ThumbnailCache;

    public async Task<GalleryThumbnailResult> GetOrCreateAsync(GalleryThumbnailRequest request,CancellationToken cancellationToken=default)
    {
        GalleryMediaItem item=request.MediaItem;if(!item.Available||!File.Exists(item.FilePath))return new(false,null,0,0,false,true,"The source media is unavailable.");string key=$"{item.Id}:{(int)request.ThumbnailType}:{request.GenerationVersion}";SemaphoreSlim gate=_locks.GetOrAdd(key,_=>new(1,1));await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            GalleryThumbnail? cached=await persistence.GetThumbnailAsync(item.Id,request.ThumbnailType,cancellationToken).ConfigureAwait(false);
            if(cached is not null&&cached.GenerationVersion==request.GenerationVersion&&cached.SourceModifiedAtUtc==item.ModifiedAtUtc&&File.Exists(cached.LocalCachePath)){_=await ReadIntoMemoryAsync(key,cached.LocalCachePath,cancellationToken).ConfigureAwait(false);return new(true,cached.LocalCachePath,cached.Width,cached.Height,true,false);}
            string folder=Path.Combine(CacheRoot,Folder(request.ThumbnailType));Directory.CreateDirectory(folder);string path=Path.Combine(folder,Opaque(key)+".png");string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";bool placeholder=false;int width=1,height=1;
            try
            {
                StorageFile file=await StorageFile.GetFileFromPathAsync(item.FilePath).AsTask(cancellationToken).ConfigureAwait(false);ThumbnailMode mode=item.MediaType==GalleryMediaType.Video?ThumbnailMode.VideosView:ThumbnailMode.PicturesView;
                using StorageItemThumbnail thumbnail=await file.GetThumbnailAsync(mode,(uint)Math.Clamp(request.MaximumDimension,64,2048),ThumbnailOptions.UseCurrentScale).AsTask(cancellationToken).ConfigureAwait(false);
                if(thumbnail.Size>0){width=(int)thumbnail.OriginalWidth;height=(int)thumbnail.OriginalHeight;await using Stream input=thumbnail.AsStreamForRead();await using FileStream output=new(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true);await input.CopyToAsync(output,cancellationToken).ConfigureAwait(false);}else{placeholder=true;await File.WriteAllBytesAsync(temporary,Placeholder,cancellationToken).ConfigureAwait(false);}
            }
            catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or COMException or ArgumentException){placeholder=true;await File.WriteAllBytesAsync(temporary,Placeholder,cancellationToken).ConfigureAwait(false);}
            File.Move(temporary,path,true);FileInfo info=new(path);DateTime now=DateTime.UtcNow;GalleryThumbnail record=new(Opaque(key),item.Id,request.ThumbnailType,path,width,height,info.Length,item.ModifiedAtUtc,now,now,request.GenerationVersion);await persistence.UpsertThumbnailAsync(record,cancellationToken).ConfigureAwait(false);byte[] bytes=await File.ReadAllBytesAsync(path,cancellationToken).ConfigureAwait(false);_memory.Put(key,bytes);await EnforceDiskLimitAsync(cancellationToken).ConfigureAwait(false);return new(true,path,width,height,false,placeholder);
        }
        finally{gate.Release();}
    }

    public async Task InvalidateAsync(string galleryMediaItemId,CancellationToken cancellationToken=default)
    { GalleryThumbnailType[] types=Enum.GetValues<GalleryThumbnailType>();foreach(GalleryThumbnailType type in types){GalleryThumbnail? value=await persistence.GetThumbnailAsync(galleryMediaItemId,type,cancellationToken).ConfigureAwait(false);if(value is not null&&File.Exists(value.LocalCachePath))File.Delete(value.LocalCachePath);}await persistence.RemoveThumbnailsAsync(galleryMediaItemId,cancellationToken).ConfigureAwait(false); }
    public async Task<GalleryThumbnailCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken=default){IReadOnlyList<GalleryThumbnail> all=await persistence.GetThumbnailLruAsync(cancellationToken).ConfigureAwait(false);return new(all.Where(value=>File.Exists(value.LocalCachePath)).Sum(value=>value.FileSize),_memory.Bytes,all.Count,_memory.Count);}
    public async Task ClearCacheAsync(CancellationToken cancellationToken=default){IReadOnlyList<GalleryThumbnail> all=await persistence.GetThumbnailLruAsync(cancellationToken).ConfigureAwait(false);foreach(GalleryThumbnail value in all){cancellationToken.ThrowIfCancellationRequested();if(File.Exists(value.LocalCachePath))File.Delete(value.LocalCachePath);await persistence.RemoveThumbnailsAsync(value.GalleryMediaItemId,cancellationToken).ConfigureAwait(false);}_memory.Clear();}
    private async Task EnforceDiskLimitAsync(CancellationToken token){int mb=await settings.GetAsync("MaximumGalleryThumbnailCacheMb",2048,token).ConfigureAwait(false);long limit=Math.Max(64,mb)*1024L*1024L;IReadOnlyList<GalleryThumbnail> all=await persistence.GetThumbnailLruAsync(token).ConfigureAwait(false);long total=all.Where(value=>File.Exists(value.LocalCachePath)).Sum(value=>value.FileSize);foreach(GalleryThumbnail value in all){if(total<=limit)break;if(File.Exists(value.LocalCachePath)){File.Delete(value.LocalCachePath);total-=value.FileSize;}}}
    private async Task<bool> ReadIntoMemoryAsync(string key,string path,CancellationToken token){if(_memory.TryGet(key,out _))return true;byte[] value=await File.ReadAllBytesAsync(path,token).ConfigureAwait(false);_memory.Put(key,value);return true;}
    private static string Folder(GalleryThumbnailType type)=>type switch{GalleryThumbnailType.GridSmall=>"small",GalleryThumbnailType.GridMedium=>"medium",GalleryThumbnailType.DetailsLarge=>"large",GalleryThumbnailType.VideoPreview=>"video",_=>"albums"};
    private static string Opaque(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
