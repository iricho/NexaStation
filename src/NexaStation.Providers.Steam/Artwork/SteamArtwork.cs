using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NexaStation.Recovery;
using NexaStation.Domain;

namespace NexaStation.Providers.Steam.Artwork;

public enum SteamArtworkType { Cover, Background, Logo, Icon, Unknown }
public sealed record SteamArtworkCandidate(string Path,SteamArtworkType Type,int? Width,int? Height,int Priority);

public interface ISteamArtworkLocator
{
    Task<IReadOnlyList<SteamArtworkCandidate>> FindAsync(string steamRoot,string appId,CancellationToken cancellationToken=default);
}

public interface IProviderArtworkCache
{
    Task<IReadOnlyList<ArtworkCandidate>> CacheAsync(string providerId,string providerItemId,IReadOnlyCollection<SteamArtworkCandidate> candidates,CancellationToken cancellationToken=default);
}

public sealed class SteamArtworkLocator : ISteamArtworkLocator
{
    private static readonly HashSet<string> Extensions=new([".png",".jpg",".jpeg",".webp"],StringComparer.OrdinalIgnoreCase);
    public async Task<IReadOnlyList<SteamArtworkCandidate>> FindAsync(string steamRoot,string appId,CancellationToken cancellationToken=default)
    {
        if(!long.TryParse(appId,out long value)||value<=0)return [];
        return await Task.Run(()=>Find(steamRoot,appId,cancellationToken),cancellationToken).ConfigureAwait(false);
    }

    private static SteamArtworkCandidate[] Find(string steamRoot,string appId,CancellationToken token)
    {
        List<(string Path,bool RequireAppPrefix)> roots=[(Path.Combine(steamRoot,"appcache","librarycache"),true),(Path.Combine(steamRoot,"appcache","librarycache",appId),false)];
        string users=Path.Combine(steamRoot,"userdata");
        if(Directory.Exists(users))
            roots.AddRange(Directory.EnumerateDirectories(users).Take(100).Select(path=>(Path.Combine(path,"config","grid"),true)));
        Dictionary<string,SteamArtworkCandidate> found=new(StringComparer.OrdinalIgnoreCase);
        foreach((string root,bool requireAppPrefix) in roots.Where(value=>Directory.Exists(value.Path)))
        {
            token.ThrowIfCancellationRequested();
            IEnumerable<string> files;
            try{files=Directory.EnumerateFiles(root,requireAppPrefix?$"{appId}*":"*",SearchOption.TopDirectoryOnly).Take(250).ToArray();}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){continue;}
            foreach(string file in files)
            {
                token.ThrowIfCancellationRequested();if(!Extensions.Contains(Path.GetExtension(file)))continue;
                if(!ImageHeader.TryRead(file,out int? width,out int? height))continue;
                string name=Path.GetFileNameWithoutExtension(file);
                SteamArtworkType type=Classify(name,width,height);int priority=Priority(name,type,width,height);
                found.TryAdd(Path.GetFullPath(file),new(Path.GetFullPath(file),type,width,height,priority));
            }
        }
        return found.Values.OrderByDescending(value=>value.Priority).ThenBy(value=>value.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static SteamArtworkType Classify(string name,int? width,int? height)
    {
        if(name.Contains("logo",StringComparison.OrdinalIgnoreCase))return SteamArtworkType.Logo;
        if(name.Contains("icon",StringComparison.OrdinalIgnoreCase))return SteamArtworkType.Icon;
        if(name.Contains("hero",StringComparison.OrdinalIgnoreCase)||name.Contains("background",StringComparison.OrdinalIgnoreCase))return SteamArtworkType.Background;
        if(name.Contains("portrait",StringComparison.OrdinalIgnoreCase)||name.Contains("cover",StringComparison.OrdinalIgnoreCase))return SteamArtworkType.Cover;
        if(width is >0&&height is >0)return height>width*1.1?SteamArtworkType.Cover:width>height*1.3?SteamArtworkType.Background:SteamArtworkType.Unknown;
        return SteamArtworkType.Unknown;
    }
    private static bool ExplicitType(string name,SteamArtworkType type)=>type!=SteamArtworkType.Unknown&&(name.Contains("logo",StringComparison.OrdinalIgnoreCase)||name.Contains("icon",StringComparison.OrdinalIgnoreCase)||name.Contains("hero",StringComparison.OrdinalIgnoreCase)||name.Contains("background",StringComparison.OrdinalIgnoreCase)||name.Contains("portrait",StringComparison.OrdinalIgnoreCase)||name.Contains("cover",StringComparison.OrdinalIgnoreCase));
    private static int Priority(string name,SteamArtworkType type,int? width,int? height)
    {
        int w=width??0,h=height??0;int pixels=Math.Min(w*h,16_000_000);int score=ExplicitType(name,type)?1000:300;
        score+=pixels/20_000;
        if(type==SteamArtworkType.Background)score+=w>h*1.3?600:-600;
        if(type==SteamArtworkType.Cover)score+=h>w*1.1?600:-600;
        if(type==SteamArtworkType.Background&&(w<960||h<420))score-=700;
        if(type==SteamArtworkType.Cover&&(w<300||h<420))score-=500;
        return score;
    }
}

public sealed class ProviderArtworkCache(string? cacheRoot=null) : IProviderArtworkCache
{
    private readonly string _root=cacheRoot??new NexaStationPaths().ProviderCache;
    public ProviderArtworkCache(INexaStationPaths paths):this(paths.ProviderCache){}
    public async Task<IReadOnlyList<ArtworkCandidate>> CacheAsync(string providerId,string providerItemId,IReadOnlyCollection<SteamArtworkCandidate> candidates,CancellationToken cancellationToken=default)
    {
        List<ArtworkCandidate> result=[];string destination=Path.Combine(_root,Safe(providerId),Safe(providerItemId));Directory.CreateDirectory(destination);
        foreach(SteamArtworkCandidate candidate in candidates.OrderByDescending(value=>value.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();FileInfo info=new(candidate.Path);if(!info.Exists||info.Length<=0||info.Length>32*1024*1024)continue;
            if(!ImageHeader.TryRead(candidate.Path,out int? width,out int? height)||width>16384||height>16384)continue;
            string extension=Path.GetExtension(candidate.Path).ToLowerInvariant();string hash;
            await using(FileStream source=File.OpenRead(candidate.Path)){hash=Convert.ToHexString(await SHA256.HashDataAsync(source,cancellationToken).ConfigureAwait(false)).ToLowerInvariant()[..16];}
            string type=candidate.Type.ToString().ToLowerInvariant();string cached=Path.Combine(destination,$"{type}-{hash}{extension}");
            if(!File.Exists(cached))File.Copy(candidate.Path,cached,false);
            string metadata=Path.Combine(destination,$"{type}-{hash}.json");
            if(!File.Exists(metadata))await File.WriteAllTextAsync(metadata,JsonSerializer.Serialize(new{sourcePath=candidate.Path,sourceLength=info.Length,cachedAtUtc=DateTime.UtcNow}),cancellationToken).ConfigureAwait(false);
            result.Add(new(cached,Map(candidate.Type),candidate.Priority,width,height));
        }
        return result;
    }
    private static string Safe(string value)=>string.Concat(value.Where(character=>char.IsLetterOrDigit(character)||character is '-' or '_'));
    private static AssetType Map(SteamArtworkType type)=>type switch{SteamArtworkType.Background=>AssetType.Background,SteamArtworkType.Logo=>AssetType.Logo,SteamArtworkType.Icon or SteamArtworkType.Unknown=>AssetType.Icon,_=>AssetType.Cover};
}

internal static class ImageHeader
{
    public static bool TryRead(string path,out int? width,out int? height)
    {
        width=null;height=null;try{using FileStream stream=File.OpenRead(path);Span<byte> header=stackalloc byte[32];int read=stream.Read(header);if(read<12)return false;
            if(header[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})){if(read<24)return false;width=BinaryPrimitives.ReadInt32BigEndian(header[16..20]);height=BinaryPrimitives.ReadInt32BigEndian(header[20..24]);return width>0&&height>0;}
            if(header[0]==0xFF&&header[1]==0xD8)return ReadJpeg(stream,out width,out height);
            if(header[..4].SequenceEqual("RIFF"u8)&&header[8..12].SequenceEqual("WEBP"u8))return true;
            return false;}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){return false;}
    }
    private static bool ReadJpeg(Stream stream,out int? width,out int? height){width=null;height=null;stream.Position=2;while(stream.Position<stream.Length-9){int markerStart=stream.ReadByte();if(markerStart!=0xFF)continue;int marker=stream.ReadByte();int high=stream.ReadByte(),low=stream.ReadByte();if(low<0)return false;int length=(high<<8)+low;if(length<2)return false;if(marker is >=0xC0 and <=0xC3){stream.ReadByte();int h1=stream.ReadByte(),h2=stream.ReadByte(),w1=stream.ReadByte(),w2=stream.ReadByte();height=(h1<<8)+h2;width=(w1<<8)+w2;return width>0&&height>0;}stream.Position+=length-2;}return false;}
}
