using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;
using Windows.Graphics.Imaging;
using Windows.Storage;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure.Artwork;

public sealed class UnifiedArtworkResolver(ISqliteConnectionFactory connections,ILibraryRepository library,IEmulationPersistence? emulation=null,ISettingsService? settings=null,INexaStationPaths? paths=null,IArtworkAssignmentStore? assignments=null) : IArtworkResolver
{
    private static readonly string[] Supported=[".png",".jpg",".jpeg",".bmp",".gif",".webp"];
    private readonly ConcurrentDictionary<string,Lazy<Task<ArtworkResolutionResult>>> _inflight=new(StringComparer.Ordinal);
    private readonly string _cacheRoot=(paths??new NexaStationPaths()).Cache;

    public async Task<ArtworkResolutionResult> ResolveAsync(ArtworkRequest request,CancellationToken cancellationToken=default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetId);
        cancellationToken.ThrowIfCancellationRequested();
        string requestKey=RequestKey(request);
        Lazy<Task<ArtworkResolutionResult>> work=_inflight.GetOrAdd(requestKey,_=>new(()=>ResolveCoreAsync(request),LazyThreadSafetyMode.ExecutionAndPublication));
        try{return await work.Value.WaitAsync(cancellationToken).ConfigureAwait(false);}
        finally{if(work.IsValueCreated&&work.Value.IsCompleted)_inflight.TryRemove(new KeyValuePair<string,Lazy<Task<ArtworkResolutionResult>>>(requestKey,work));}
    }

    private async Task<ArtworkResolutionResult> ResolveCoreAsync(ArtworkRequest request)
    {
        ArtworkFallbackType fallback=Fallback(request.ContentType,request.ProviderId);
        if(request.IsPrivateContent)return Missing(request,fallback,"Private content cannot supply dashboard artwork.");
        LibraryItem? item=await library.GetByIdAsync(request.TargetId).ConfigureAwait(false);
        if(item is null)return Missing(request,fallback,"The content item no longer exists.");

        if(assignments is not null)
        {
            ArtworkAssignment? assignment=await assignments.GetCurrentAsync(item.Id,Map(request.PreferredAssetType)).ConfigureAwait(false);
            if(assignment is not null)
            {
                ImageInspection inspection=await InspectAsync(assignment.LocalPath).ConfigureAwait(false);
                if(inspection.IsValid)return Resolved(request,assignment.UserSelected?ArtworkSourceType.UserOverride:ArtworkSourceType.ManagedCache,assignment.LocalPath,inspection.Width,inspection.Height,fallback);
            }
        }

        IReadOnlyList<AssetRecord> assets=await ReadAssetsAsync(item.Id).ConfigureAwait(false);
        bool overridden=await HasOverrideAsync(item.Id,request.PreferredAssetType).ConfigureAwait(false);
        IEnumerable<AssetRecord> ordered=assets
            .Where(asset=>Matches(request.PreferredAssetType,asset.Type))
            .OrderByDescending(asset=>asset.Type==request.PreferredAssetType)
            .ThenByDescending(asset=>Score(asset,request));

        foreach(AssetRecord asset in ordered)
        {
            ImageInspection inspection=await InspectAsync(asset.Path).ConfigureAwait(false);
            if(!inspection.IsValid||!overridden&&!Suitable(request.PreferredAssetType,inspection.Width,inspection.Height))continue;
            ArtworkSourceType source=overridden?ArtworkSourceType.UserOverride:ClassifySource(asset.Path,request.ProviderId);
            return Resolved(request,source,asset.Path,inspection.Width,inspection.Height,fallback);
        }

        foreach(string sidecar in await SidecarsAsync(item,request.PreferredAssetType).ConfigureAwait(false))
        {
            ImageInspection inspection=await InspectAsync(sidecar).ConfigureAwait(false);
            if(inspection.IsValid)return Resolved(request,ArtworkSourceType.Sidecar,sidecar,inspection.Width,inspection.Height,fallback);
        }

        return Missing(request,fallback,assets.Count==0?"No artwork asset or supported sidecar was found.":"Artwork files were missing, unsupported, or corrupt.");
    }

    private async Task<IReadOnlyList<AssetRecord>> ReadAssetsAsync(string itemId)
    {
        await using SqliteConnection connection=await connections.OpenAsync().ConfigureAwait(false);
        await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT AssetType,LocalPath,Width,Height FROM Assets WHERE LibraryItemId=$item ORDER BY AddedAtUtc DESC;";command.Parameters.AddWithValue("$item",itemId);
        List<AssetRecord> result=[];await using SqliteDataReader reader=await command.ExecuteReaderAsync().ConfigureAwait(false);while(await reader.ReadAsync().ConfigureAwait(false))result.Add(new((AssetType)reader.GetInt32(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetInt32(2),reader.IsDBNull(3)?null:reader.GetInt32(3)));return result;
    }

    private async Task<bool> HasOverrideAsync(string itemId,AssetType type)
    {
        string field=type switch{AssetType.Background=>"Background",AssetType.Logo=>"Logo",AssetType.Icon=>"Icon",_=>"Cover"};
        await using SqliteConnection connection=await connections.OpenAsync().ConfigureAwait(false);await using SqliteCommand command=connection.CreateCommand();command.CommandText="SELECT 1 FROM MetadataOverrides WHERE LibraryItemId=$item AND FieldName=$field LIMIT 1;";command.Parameters.AddWithValue("$item",itemId);command.Parameters.AddWithValue("$field",field);return await command.ExecuteScalarAsync().ConfigureAwait(false) is not null;
    }

    private async Task<IReadOnlyList<string>> SidecarsAsync(LibraryItem item,AssetType requested)
    {
        string? target=item.LaunchTarget?.ExecutablePath??item.LaunchTarget?.ShortcutPath;
        if(item.LaunchTarget?.EmulatedGameSourceId is { } gameSourceId&&emulation is not null)target=(await emulation.GetEmulatedGameAsync(gameSourceId).ConfigureAwait(false))?.GamePath??target;
        if(string.IsNullOrWhiteSpace(target)||!Path.IsPathFullyQualified(target))return [];
        string? directory=Directory.Exists(target)?target:Path.GetDirectoryName(target);List<string> directories=[];if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))directories.Add(directory);
        if(settings is not null){string configured=await settings.GetAsync("Artwork.Directory",string.Empty).ConfigureAwait(false);if(!string.IsNullOrWhiteSpace(configured)&&Path.IsPathFullyQualified(configured)&&Directory.Exists(configured))directories.Add(Path.GetFullPath(configured));}
        if(directories.Count==0)return [];
        string stem=Path.GetFileNameWithoutExtension(target);string[] names=requested switch
        {
            AssetType.Background=>[$"{stem}-background.jpg",$"{stem}-background.png","background.jpg","background.png"],
            AssetType.Icon=>[$"{stem}-icon.png","icon.png"],
            AssetType.Logo=>[$"{stem}-logo.png","logo.png"],
            _=>[$"{stem}.jpg",$"{stem}.png",$"{stem}-cover.jpg",$"{stem}-cover.png","cover.jpg","cover.png"]
        };
        return directories.Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(root=>names.Select(name=>Path.Combine(root,name))).ToArray();
    }

    private static async Task<ImageInspection> InspectAsync(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))return new(false,0,0,"File missing.");
        if(!Supported.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))return new(false,0,0,"Unsupported image type.");
        try
        {
            StorageFile file=await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
            using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream=await file.OpenReadAsync().AsTask().ConfigureAwait(false);
            BitmapDecoder decoder=await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
            return decoder.PixelWidth>0&&decoder.PixelHeight>0
                ?new(true,(int)decoder.PixelWidth,(int)decoder.PixelHeight,null)
                :new(false,0,0,"Image has no decodable pixels.");
        }
        catch(Exception exception)when(exception is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return new(false,0,0,"Image decode failed.");
        }
    }

    private static bool Matches(AssetType requested,AssetType candidate)=>requested==candidate||requested==AssetType.Cover&&candidate==AssetType.Icon||requested==AssetType.Background&&candidate==AssetType.Cover;
    private static bool Suitable(AssetType requested,int width,int height)=>requested switch{AssetType.Background=>width>=640&&height>=240&&width>height*1.15,AssetType.Cover=>width>=128&&height>=128,AssetType.Icon=>width>=16&&height>=16,_=>width>0&&height>0};
    private static int Score(AssetRecord asset,ArtworkRequest request){int width=asset.Width??0,height=asset.Height??0;int orientation=request.PreferredAssetType==AssetType.Background&&width>height?1000:request.PreferredAssetType is AssetType.Cover&&height>=width?1000:0;return orientation+Math.Min(width*height,20_000_000)/1000;}
    private ArtworkSourceType ClassifySource(string path,string? providerId){if(Path.GetFullPath(path).StartsWith(Path.GetFullPath(_cacheRoot),StringComparison.OrdinalIgnoreCase))return ArtworkSourceType.ManagedCache;if(string.Equals(providerId,"catalogue",StringComparison.OrdinalIgnoreCase))return ArtworkSourceType.Catalogue;return string.IsNullOrWhiteSpace(providerId)?ArtworkSourceType.Sidecar:ArtworkSourceType.ProviderLocal;}
    private static ArtworkFallbackType Fallback(ContentType type,string? provider)=>string.Equals(provider,"catalogue",StringComparison.OrdinalIgnoreCase)?ArtworkFallbackType.StreamingService:type switch{ContentType.Game=>ArtworkFallbackType.Game,ContentType.Application=>ArtworkFallbackType.Application,_=>ArtworkFallbackType.Generic};
    private static ArtworkKind Map(AssetType type)=>type switch{AssetType.Background=>ArtworkKind.HeroBackground,AssetType.Logo=>ArtworkKind.Logo,AssetType.Icon=>ArtworkKind.Icon,_=>ArtworkKind.CoverPortrait};
    private static ArtworkResolutionResult Resolved(ArtworkRequest request,ArtworkSourceType source,string path,int width,int height,ArtworkFallbackType fallback)=>new(source,path,path,width,height,CacheKey(request,path,width,height),false,null,fallback);
    private static ArtworkResolutionResult Missing(ArtworkRequest request,ArtworkFallbackType fallback,string reason)=>new(ArtworkSourceType.GeneratedFallback,null,null,null,null,CacheKey(request,null,0,0),true,reason,fallback);
    private static string RequestKey(ArtworkRequest request)=>$"{request.TargetId}|{request.PreferredAssetType}|{request.TargetWidth}x{request.TargetHeight}|{request.AllowOnlineLookup}|{request.IsPrivateContent}";
    private static string CacheKey(ArtworkRequest request,string? path,int width,int height){byte[] hash=SHA256.HashData(Encoding.UTF8.GetBytes(RequestKey(request)+"|"+(path??"fallback")+"|"+width+"x"+height));return Convert.ToHexString(hash[..12]).ToLowerInvariant();}
    private sealed record ImageInspection(bool IsValid,int Width,int Height,string? FailureReason);
    private sealed record AssetRecord(AssetType Type,string Path,int? Width,int? Height);
}
