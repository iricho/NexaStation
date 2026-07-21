using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Gallery.Validation;

namespace NexaStation.Providers.Gallery.Scanning;

public sealed class GallerySourceScanner : IGallerySourceScanner
{
    public Task<GallerySourceScanResult> ScanAsync(GallerySource source,CancellationToken cancellationToken=default)=>Task.Run(()=>Scan(source,cancellationToken),cancellationToken);

    private static GallerySourceScanResult Scan(GallerySource source,CancellationToken token)
    {
        DateTime started=DateTime.UtcNow;List<GalleryMediaCandidate> candidates=[];List<string> warnings=[];List<string> errors=[];
        string root;
        try{root=GalleryPathPolicy.NormalizeFolder(source.FolderPath);}catch(Exception exception){errors.Add(exception.Message);return new(source,candidates,warnings,errors,false,false,false,started,DateTime.UtcNow);}
        if(!Directory.Exists(root)){warnings.Add("The Gallery source folder is currently unavailable.");return new(source,candidates,warnings,errors,false,false,false,started,DateTime.UtcNow);}
        Stack<string> pending=new();pending.Push(root);HashSet<string> visited=new(StringComparer.OrdinalIgnoreCase);bool complete=true;
        try
        {
            while(pending.Count>0)
            {
                token.ThrowIfCancellationRequested();string directory=pending.Pop();string normalized;
                try{normalized=GalleryPathPolicy.NormalizeFolder(directory);if(!GalleryPathPolicy.IsInside(normalized,root)){warnings.Add("A directory outside the approved source was ignored.");complete=false;continue;}FileAttributes directoryAttributes=File.GetAttributes(normalized);if((directoryAttributes&FileAttributes.ReparsePoint)!=0&& !string.Equals(normalized,root,StringComparison.OrdinalIgnoreCase)){warnings.Add("A linked directory was not followed.");continue;}if(!visited.Add(normalized)){warnings.Add("A directory loop was ignored.");continue;}}
                catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){warnings.Add($"A Gallery directory could not be read: {exception.GetType().Name}.");complete=false;continue;}
                IEnumerable<string> entries;
                try{entries=Directory.EnumerateFileSystemEntries(normalized);}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){warnings.Add($"A Gallery directory could not be enumerated: {exception.GetType().Name}.");complete=false;continue;}
                try
                {
                    foreach(string entry in entries)
                    {
                        token.ThrowIfCancellationRequested();FileAttributes attributes;
                        try{attributes=File.GetAttributes(entry);}catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){warnings.Add($"A Gallery entry was skipped: {exception.GetType().Name}.");continue;}
                        if((attributes&FileAttributes.Directory)!=0){if(source.IncludeSubfolders&&(attributes&FileAttributes.ReparsePoint)==0)pending.Push(entry);continue;}
                        if(!source.IncludeHiddenFiles&&(attributes&FileAttributes.Hidden)!=0)continue;
                        if((attributes&FileAttributes.ReparsePoint)!=0)continue;
                        if(entry.Length>32767||Path.GetFileName(entry).Length>255){warnings.Add("An overlong Gallery path was skipped.");continue;}
                        string extension=Path.GetExtension(entry).ToLowerInvariant();GalleryMediaType type=GalleryPathPolicy.Classify(extension);
                        if(type==GalleryMediaType.Unknown||(type==GalleryMediaType.Video&&!source.IncludeVideos)||(type!=GalleryMediaType.Video&&!source.IncludeImages))continue;
                        try
                        {
                            string full=Path.GetFullPath(entry);if(!GalleryPathPolicy.IsInside(full,root)){warnings.Add("A media path outside the approved source was ignored.");complete=false;continue;}FileInfo file=new(full);
                            candidates.Add(new(full,full,Path.GetFileName(full),extension,Path.GetDirectoryName(full)??root,file.Length,file.CreationTimeUtc,file.LastWriteTimeUtc,type));
                        }
                        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or ArgumentException){warnings.Add($"A media file was skipped: {exception.GetType().Name}.");}
                    }
                }
                catch(Exception exception) when(exception is IOException or UnauthorizedAccessException){warnings.Add($"A Gallery directory changed while scanning: {exception.GetType().Name}.");complete=false;}
            }
            return new(source,candidates,warnings,errors,complete,false,true,started,DateTime.UtcNow);
        }
        catch(OperationCanceledException){return new(source,candidates,warnings,errors,false,true,true,started,DateTime.UtcNow);}
    }
}
