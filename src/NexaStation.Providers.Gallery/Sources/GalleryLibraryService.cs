using System.Collections.Concurrent;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Gallery.Validation;

namespace NexaStation.Providers.Gallery.Sources;

public sealed class GalleryLibraryService(IGalleryPersistence persistence,IGallerySourceScanner scanner,IGalleryMediaInspector inspector,IGalleryThumbnailService thumbnails) : IGalleryLibraryService
{
    public Task<IReadOnlyList<GallerySourceOverlap>> PreviewSourceAsync(string folderPath,CancellationToken cancellationToken=default)=>persistence.FindSourceOverlapsAsync(GalleryPathPolicy.NormalizeFolder(folderPath),cancellationToken);

    public async Task<GallerySource> AddSourceAsync(GallerySource source,bool userApproved,bool scanImmediately,CancellationToken cancellationToken=default)
    {
        if(!userApproved)throw new InvalidOperationException("Gallery folders require explicit user approval.");string normalized=GalleryPathPolicy.NormalizeFolder(source.FolderPath);IReadOnlyList<GallerySourceOverlap> overlaps=await persistence.FindSourceOverlapsAsync(normalized,cancellationToken).ConfigureAwait(false);
        if(overlaps.Any(value=>value.Kind==GallerySourceOverlapKind.Exact))throw new InvalidOperationException("This Gallery folder is already configured.");DateTime now=DateTime.UtcNow;GallerySource saved=source with{Id=string.IsNullOrWhiteSpace(source.Id)?Guid.NewGuid().ToString("N"):source.Id,FolderPath=normalized,NormalizedFolderPath=normalized,CreatedAtUtc=source.CreatedAtUtc==default?now:source.CreatedAtUtc,UpdatedAtUtc=now};await persistence.AddSourceAsync(saved,cancellationToken).ConfigureAwait(false);if(scanImmediately)await RefreshSourceAsync(saved.Id,cancellationToken).ConfigureAwait(false);return saved;
    }

    public async Task<GalleryRefreshResult> RefreshSourceAsync(string sourceId,CancellationToken cancellationToken=default)
    {
        GallerySource source=await persistence.GetSourceAsync(sourceId,cancellationToken).ConfigureAwait(false)??throw new InvalidOperationException("The Gallery source no longer exists.");GallerySourceScanResult scan=await scanner.ScanAsync(source,cancellationToken).ConfigureAwait(false);
        if(!scan.RootAvailable||scan.WasCancelled||!scan.IsComplete||scan.Errors.Count>0)return await persistence.ApplyScanAsync(scan,[],cancellationToken).ConfigureAwait(false);
        ConcurrentBag<GalleryMediaInspectionResult> inspected=[];ParallelOptions options=new(){CancellationToken=cancellationToken,MaxDegreeOfParallelism=4};
        await Parallel.ForEachAsync(scan.Candidates,options,async(candidate,token)=>inspected.Add(await inspector.InspectAsync(new(candidate),token).ConfigureAwait(false))).ConfigureAwait(false);
        GalleryMediaInspectionResult[] ordered=inspected.OrderBy(value=>value.Candidate.NormalizedFilePath,StringComparer.OrdinalIgnoreCase).ToArray();return await persistence.ApplyScanAsync(scan,ordered,cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveSourceAsync(string sourceId,bool keepCachedThumbnails,CancellationToken cancellationToken=default)
    {
        if(!keepCachedThumbnails){IReadOnlyList<GalleryMediaItem> items=await persistence.QueryAsync(new(SourceId:sourceId,IncludeHidden:true,IncludeUnavailable:true,MaximumResults:50000),cancellationToken).ConfigureAwait(false);foreach(GalleryMediaItem item in items)await thumbnails.InvalidateAsync(item.Id,cancellationToken).ConfigureAwait(false);}await persistence.RemoveSourceAsync(sourceId,cancellationToken).ConfigureAwait(false);
    }

    public async Task<GalleryMediaItem?> IndexApprovedFileAsync(string filePath,CancellationToken cancellationToken=default)
    {
        string full=Path.GetFullPath(filePath);if(!File.Exists(full))return null;GallerySource? source=(await persistence.GetSourcesAsync(cancellationToken).ConfigureAwait(false)).Where(value=>value.Enabled&&GalleryPathPolicy.IsInside(full,value.NormalizedFolderPath)).OrderByDescending(value=>value.NormalizedFolderPath.Length).FirstOrDefault();if(source is null)return null;
        FileInfo file=new(full);string extension=file.Extension.ToLowerInvariant();GalleryMediaType type=GalleryPathPolicy.Classify(extension);if(type==GalleryMediaType.Unknown||(type==GalleryMediaType.Video&&!source.IncludeVideos)||(type!=GalleryMediaType.Video&&!source.IncludeImages))return null;
        GalleryMediaCandidate candidate=new(full,full,file.Name,extension,file.DirectoryName??source.FolderPath,file.Length,file.CreationTimeUtc,file.LastWriteTimeUtc,type);GalleryMediaInspectionResult inspected=await inspector.InspectAsync(new(candidate),cancellationToken).ConfigureAwait(false);return await persistence.UpsertMediaAsync(source,inspected,cancellationToken).ConfigureAwait(false);
    }
}
