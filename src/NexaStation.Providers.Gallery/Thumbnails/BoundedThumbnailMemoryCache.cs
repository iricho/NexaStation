namespace NexaStation.Providers.Gallery.Thumbnails;

public sealed class BoundedThumbnailMemoryCache(long maximumBytes)
{
    private readonly Dictionary<string,Entry> _entries=new(StringComparer.Ordinal);private readonly LinkedList<string> _lru=[];private readonly object _gate=new();private long _bytes;
    public long Bytes{get{lock(_gate)return _bytes;}}public int Count{get{lock(_gate)return _entries.Count;}}
    public bool TryGet(string key,out byte[]? value){lock(_gate){if(!_entries.TryGetValue(key,out Entry? entry)){value=null;return false;}_lru.Remove(entry.Node);_lru.AddLast(entry.Node);value=entry.Value;return true;}}
    public void Put(string key,byte[] value){ArgumentNullException.ThrowIfNull(value);lock(_gate){if(_entries.Remove(key,out Entry? old)){_lru.Remove(old.Node);_bytes-=old.Value.LongLength;}if(value.LongLength>maximumBytes)return;LinkedListNode<string> node=_lru.AddLast(key);_entries[key]=new(value,node);_bytes+=value.LongLength;while(_bytes>maximumBytes&&_lru.First is not null){string victim=_lru.First.Value;_lru.RemoveFirst();if(_entries.Remove(victim,out Entry? removed))_bytes-=removed.Value.LongLength;}}}
    public void Clear(){lock(_gate){_entries.Clear();_lru.Clear();_bytes=0;}}
    private sealed record Entry(byte[] Value,LinkedListNode<string> Node);
}
