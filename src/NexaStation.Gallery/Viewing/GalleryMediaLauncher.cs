using System.Diagnostics;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Media.Hub;

namespace NexaStation.Gallery.Viewing;

public sealed class GalleryMediaLauncher(IGalleryPersistence gallery,ILibraryRepository library,IProcessLauncher processes,IAppLogger logger,IMediaAssociationService associations,IRecentMediaService recent) : IGalleryMediaLauncher
{
    public async Task<GalleryMediaLaunchResult> OpenAsync(GalleryMediaLaunchRequest request,CancellationToken cancellationToken=default)
    {
        if(!request.DirectUserAction)return new(false,false,"Opening media requires a direct user action.");GalleryMediaItem item=request.MediaItem;if(!item.Available||!File.Exists(item.FilePath))return new(false,false,"The media file could not be located.");
        GalleryLaunchPreference? preference=null;
        MediaAssociation? association=await associations.ResolveAsync(item,cancellationToken).ConfigureAwait(false);
        if(association is not null&&!string.IsNullOrWhiteSpace(association.PlayerLibraryItemId))preference=new(association.Id,GalleryLaunchPreferenceScope.MediaItem,item.Id,association.PlayerLibraryItemId,association.ArgumentsTemplate??"{MEDIA_PATH}",association.HideFrontend,association.RestoreFrontend,association.TrackingMode);
        if(!string.IsNullOrWhiteSpace(request.PreferredApplicationId))preference=new(Guid.NewGuid().ToString("N"),GalleryLaunchPreferenceScope.MediaItem,item.Id,request.PreferredApplicationId,request.ArgumentsTemplate??"{MEDIA_PATH}");
        preference??=await gallery.GetLaunchPreferenceAsync(GalleryLaunchPreferenceScope.MediaItem,item.Id,cancellationToken).ConfigureAwait(false);
        preference??=await gallery.GetLaunchPreferenceAsync(GalleryLaunchPreferenceScope.Source,item.GallerySourceId,cancellationToken).ConfigureAwait(false);
        if(item.MediaType==GalleryMediaType.Video)preference??=await gallery.GetLaunchPreferenceAsync(GalleryLaunchPreferenceScope.GlobalVideo,null,cancellationToken).ConfigureAwait(false);
        if(preference is not null)
        {
            GalleryMediaLaunchResult preferred=await OpenPreferredAsync(item,preference,cancellationToken).ConfigureAwait(false);if(preferred.Succeeded||!request.AllowDefaultFallback)return preferred;
        }
        try{Process? process=Process.Start(new ProcessStartInfo{FileName=item.FilePath,UseShellExecute=true});if(process is null)return new(false,false,"Windows did not start the associated application.");DateTime opened=DateTime.UtcNow;await gallery.MarkOpenedAsync(item.Id,opened,cancellationToken).ConfigureAwait(false);await recent.RecordOpenedAsync(new(Guid.NewGuid().ToString("N"),item.Id,item.MediaType,opened,MediaLaunchMethod.WindowsDefault,null,MediaTrackingConfidence.Requested),cancellationToken).ConfigureAwait(false);return new(true,false,ProcessId:process.Id);}
        catch(Exception exception) when(exception is InvalidOperationException or System.ComponentModel.Win32Exception){logger.LogError("GalleryDefaultLaunchFailed","Windows could not open a user-selected Gallery item.",exception);return new(false,false,"No suitable Windows file association could open this media.");}
    }
    private async Task<GalleryMediaLaunchResult> OpenPreferredAsync(GalleryMediaItem item,GalleryLaunchPreference preference,CancellationToken token)
    {
        LibraryItem? application=await library.GetByIdAsync(preference.ApplicationLibraryItemId,token).ConfigureAwait(false);LaunchTarget? configured=application?.LaunchTarget;if(configured is null||configured.LaunchType!=LaunchType.Executable||string.IsNullOrWhiteSpace(configured.ExecutablePath)||!File.Exists(configured.ExecutablePath))return new(false,true,"The preferred media application is unavailable.");
        if(IsShell(configured.ExecutablePath))return new(false,true,"Command shells cannot be used as preferred media applications.");IReadOnlyList<string> arguments=MediaArgumentTemplate.Render(preference.ArgumentsTemplate,item.FilePath);LaunchTarget target=configured with{ArgumentList=arguments,Arguments=null,HideFrontendWhileRunning=preference.HideFrontendWhileRunning,RestoreFrontendAfterExit=preference.RestoreFrontendAfterExit,TrackingMode=preference.TrackingMode};
        try{RunningProcess process=await processes.StartAsync(target,token).ConfigureAwait(false);int id=process.ProcessId;await process.DisposeAsync().ConfigureAwait(false);DateTime opened=DateTime.UtcNow;await gallery.MarkOpenedAsync(item.Id,opened,token).ConfigureAwait(false);await recent.RecordOpenedAsync(new(Guid.NewGuid().ToString("N"),item.Id,item.MediaType,opened,MediaLaunchMethod.PreferredPlayer,application!.Id,MediaTrackingConfidence.ProcessTracked),token).ConfigureAwait(false);return new(true,true,ProcessId:id);}catch(Exception exception) when(exception is InvalidOperationException or System.ComponentModel.Win32Exception){logger.LogError("GalleryPreferredLaunchFailed","The preferred Gallery media application failed to start.",exception);return new(false,true,"The preferred media application failed to start.");}
    }
    private static bool IsShell(string path){string name=Path.GetFileName(path);return name.Equals("cmd.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("powershell.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("pwsh.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("wscript.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("cscript.exe",StringComparison.OrdinalIgnoreCase);}
}

public static class MediaArgumentTemplate
{
    public static IReadOnlyList<string> Render(string template,string mediaPath)
    {
        if(string.IsNullOrWhiteSpace(template))template="{MEDIA_PATH}";if(template.Contains('\n')||template.Contains('\r')||template.IndexOfAny(['&','|','>','<',';','`'])>=0)throw new InvalidOperationException("The media argument template contains an unsafe shell operator.");List<string> tokens=Tokenize(template);if(!tokens.Any(value=>value.Contains("{MEDIA_PATH}",StringComparison.Ordinal)))throw new InvalidOperationException("The media argument template must contain {MEDIA_PATH}.");return tokens.Select(value=>value.Replace("{MEDIA_PATH}",mediaPath,StringComparison.Ordinal)).ToArray();
    }
    private static List<string> Tokenize(string value){List<string> result=[];StringBuilder current=new();bool quoted=false;for(int index=0;index<value.Length;index++){char c=value[index];if(c=='\"'){quoted=!quoted;continue;}if(char.IsWhiteSpace(c)&&!quoted){if(current.Length>0){result.Add(current.ToString());current.Clear();}continue;}current.Append(c);}if(quoted)throw new InvalidOperationException("The media argument template contains an unmatched quote.");if(current.Length>0)result.Add(current.ToString());return result;}
}
