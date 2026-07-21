using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Persistence;

namespace NexaStation.Infrastructure.Ecosystem;

public sealed class EcosystemCapabilityService(ISqliteConnectionFactory factory,IScreenshotCaptureService capture,IPresenceProvider presence,IRemotePlayProfileStore remotePlay) : IEcosystemCapabilityService
{
    private const int CurrentVersion=1;
    public async Task<EcosystemCapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default)
    {
        await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText="SELECT SnapshotJson,CapabilityVersion FROM EcosystemCapabilityState WHERE CapabilityId='phase12';";await using SqliteDataReader r=await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);if(await r.ReadAsync(cancellationToken).ConfigureAwait(false)&&r.GetInt32(1)==CurrentVersion){EcosystemCapabilitySnapshot? cached=JsonSerializer.Deserialize<EcosystemCapabilitySnapshot>(r.GetString(0));if(cached is not null)return cached;}return await CreateAsync(cancellationToken).ConfigureAwait(false);
    }
    public async Task RefreshAsync(EcosystemCapabilityRefreshReason reason,CancellationToken cancellationToken=default)
    {EcosystemCapabilitySnapshot snapshot=await CreateAsync(cancellationToken).ConfigureAwait(false);string json=JsonSerializer.Serialize(snapshot);await using SqliteConnection c=await factory.OpenAsync(cancellationToken).ConfigureAwait(false);await using SqliteCommand cmd=c.CreateCommand();cmd.CommandText="INSERT INTO EcosystemCapabilityState(CapabilityId,CapabilityVersion,Status,SnapshotJson,LastInvestigatedAtUtc,ExpiresAtUtc,FailureCode,FailureMessage,UpdatedAtUtc) VALUES('phase12',$version,0,$json,$at,$expires,NULL,NULL,$at) ON CONFLICT(CapabilityId) DO UPDATE SET CapabilityVersion=excluded.CapabilityVersion,Status=excluded.Status,SnapshotJson=excluded.SnapshotJson,LastInvestigatedAtUtc=excluded.LastInvestigatedAtUtc,ExpiresAtUtc=excluded.ExpiresAtUtc,FailureCode=NULL,FailureMessage=NULL,UpdatedAtUtc=excluded.UpdatedAtUtc;";cmd.Parameters.AddWithValue("$version",CurrentVersion);cmd.Parameters.AddWithValue("$json",json);cmd.Parameters.AddWithValue("$at",Iso(snapshot.RefreshedAtUtc));cmd.Parameters.AddWithValue("$expires",Iso(snapshot.RefreshedAtUtc.AddDays(30)));await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);}
    private async Task<EcosystemCapabilitySnapshot> CreateAsync(CancellationToken token)
    {
        DateTime now=DateTime.UtcNow;AchievementProviderCapability[] achievements=[
            new("steam",CapabilityStatus.RequiresApproval,"User Web API key and public Steam profile",true,true,true,true,true,true,true,now,"No hidden key ships; adapter remains disabled until explicit user configuration and privacy review."),
            new("epic",CapabilityStatus.NotAvailable,"No suitable public desktop API",false,false,false,false,false,false,false,now,"No supported user-facing achievement API was approved."),
            new("xbox",CapabilityStatus.NotAvailable,"Platform-restricted services",false,false,false,false,true,false,false,now,"No general desktop API suitable for this local frontend was approved."),
            new("gog",CapabilityStatus.NotAvailable,"No suitable public desktop API",false,false,false,false,false,false,false,now,"Launcher databases and webpages are not scraped."),
            new("retroachievements",CapabilityStatus.Experimental,"Explicit user API credential",true,true,true,false,false,true,true,now,"Investigation only; no provider is enabled in Phase 12.")];
        DownloadProviderCapability[] downloads=[
            Limited("steam","Documented local state is insufficient for stable byte progress across all clients."),Unsupported("epic"),Unsupported("gog"),Unsupported("ubisoft"),Unsupported("ea"),Unsupported("battlenet"),Unsupported("xbox")];
        return new(await capture.GetAvailabilityAsync(token).ConfigureAwait(false),OperatingSystem.IsWindows(),[],true,await presence.GetAvailabilityAsync(token).ConfigureAwait(false),achievements,downloads,(await remotePlay.GetAsync(token).ConfigureAwait(false)).Count,false,now,CurrentVersion);
    }
    private static DownloadProviderCapability Limited(string id,string notes)=>new(id,CapabilityStatus.Limited,true,true,false,false,false,false,true,"Limited","Documented launcher state",notes);private static DownloadProviderCapability Unsupported(string id)=>new(id,CapabilityStatus.NotAvailable,false,false,false,false,false,false,false,"Unsupported","None","No reliable supported byte-progress source is used.");private static string Iso(DateTime value)=>value.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
}

public sealed class RecordingFolderMonitor(ICapturedMediaStore media,IActivityTimelineService timeline) : IRecordingFolderMonitor
{
    private static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".mp4",".mkv",".mov",".avi",".wmv",".webm"};private readonly List<FileSystemWatcher> _watchers=[];private readonly ConcurrentDictionary<string,byte> _pending=new(StringComparer.OrdinalIgnoreCase);private CancellationTokenSource? _lifetime;
    public Task StartAsync(IReadOnlyList<string> explicitlyApprovedFolders,CancellationToken cancellationToken=default)
    {if(_lifetime is not null)return Task.CompletedTask;_lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);foreach(string folderValue in explicitlyApprovedFolders.Distinct(StringComparer.OrdinalIgnoreCase)){string folder=Path.GetFullPath(folderValue);if(!Directory.Exists(folder))continue;FileSystemWatcher watcher=new(folder){IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.Size|NotifyFilters.LastWrite,EnableRaisingEvents=true};watcher.Created+=OnCreated;watcher.Renamed+=OnRenamed;_watchers.Add(watcher);}return Task.CompletedTask;}
    public Task StopAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();_lifetime?.Cancel();_lifetime?.Dispose();_lifetime=null;foreach(FileSystemWatcher watcher in _watchers){watcher.EnableRaisingEvents=false;watcher.Created-=OnCreated;watcher.Renamed-=OnRenamed;watcher.Dispose();}_watchers.Clear();_pending.Clear();return Task.CompletedTask;}
    public async ValueTask DisposeAsync(){await StopAsync().ConfigureAwait(false);GC.SuppressFinalize(this);}
    private void OnCreated(object sender,FileSystemEventArgs e)=>Queue(e.FullPath);private void OnRenamed(object sender,RenamedEventArgs e)=>Queue(e.FullPath);
    private void Queue(string path){if(_lifetime is null||!Extensions.Contains(Path.GetExtension(path))||!_pending.TryAdd(path,0))return;_ = ObserveCompletedAsync(path,_lifetime.Token);}
    private async Task ObserveCompletedAsync(string path,CancellationToken token)
    {try{long previous=-1;int stable=0;for(int attempt=0;attempt<30&&stable<3;attempt++){await Task.Delay(TimeSpan.FromSeconds(2),token).ConfigureAwait(false);if(!File.Exists(path))return;long length=new FileInfo(path).Length;if(length>0&&length==previous)stable++;else stable=0;previous=length;}if(stable<3)return;string normalized=Path.GetFullPath(path);if(await media.GetByPathAsync(normalized,token).ConfigureAwait(false) is not null)return;FileInfo file=new(normalized);string hash;await using(FileStream stream=new(normalized,FileMode.Open,FileAccess.Read,FileShare.Read)){hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,token).ConfigureAwait(false)).ToLowerInvariant();}DateTime now=DateTime.UtcNow;string id=Guid.NewGuid().ToString("N");await media.SaveAsync(new(id,CaptureType.Recording,normalized,normalized,null,null,file.CreationTimeUtc,0,0,file.Length,CaptureOutputFormat.Video,CaptureSource.ExternalRecorder,false,false,ActivityPrivacyLevel.Private,hash,now),token).ConfigureAwait(false);await timeline.RecordAsync(new(Guid.NewGuid().ToString("N"),ActivityEventType.RecordingCreated,ActivityTargetType.Capture,id,null,"Recording","Created by an external recorder",null,null,now,null,null,ActivityResult.Succeeded,ActivityPrivacyLevel.Private,CreatedAtUtc:now),token).ConfigureAwait(false);}catch(OperationCanceledException){}catch(IOException){}finally{_pending.TryRemove(path,out _);}}
}
