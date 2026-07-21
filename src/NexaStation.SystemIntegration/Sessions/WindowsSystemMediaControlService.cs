using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;
using Windows.Media.Control;

namespace NexaStation.SystemIntegration.Sessions;

public sealed class WindowsSystemMediaControlService:ISystemMediaControlService,IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    public event EventHandler<SystemMediaSessionChangedEventArgs>? SessionChanged;
    public async Task<SystemMediaSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken=default){try{GlobalSystemMediaTransportControlsSession? session=await CurrentSessionAsync(cancellationToken);if(session is null)return new(false,null,null,null,null);GlobalSystemMediaTransportControlsSessionMediaProperties properties=await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);GlobalSystemMediaTransportControlsSessionPlaybackInfo playback=session.GetPlaybackInfo();return new(true,session.SourceAppUserModelId,properties.Title,properties.Artist,playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);}catch(Exception ex){return new(false,null,null,null,null,ex.Message);}}
    public Task<MediaControlResult> PlayPauseAsync(CancellationToken cancellationToken=default)=>ControlAsync((session,token)=>session.TryTogglePlayPauseAsync().AsTask(token),cancellationToken);
    public Task<MediaControlResult> NextAsync(CancellationToken cancellationToken=default)=>ControlAsync((session,token)=>session.TrySkipNextAsync().AsTask(token),cancellationToken);
    public Task<MediaControlResult> PreviousAsync(CancellationToken cancellationToken=default)=>ControlAsync((session,token)=>session.TrySkipPreviousAsync().AsTask(token),cancellationToken);
    public void Dispose(){if(_manager is not null)_manager.CurrentSessionChanged-=OnSessionChanged;}
    private async Task<GlobalSystemMediaTransportControlsSession?> CurrentSessionAsync(CancellationToken token){if(_manager is null){_manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);_manager.CurrentSessionChanged+=OnSessionChanged;}return _manager.GetCurrentSession();}
    private async Task<MediaControlResult> ControlAsync(Func<GlobalSystemMediaTransportControlsSession,CancellationToken,Task<bool>> action,CancellationToken token){try{GlobalSystemMediaTransportControlsSession? session=await CurrentSessionAsync(token);return session is null?new(false,"No active system media session."):new(await action(session,token));}catch(Exception ex){return new(false,ex.Message);}}
    private async void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,CurrentSessionChangedEventArgs args)=>SessionChanged?.Invoke(this,new(await GetCurrentAsync()));
}
