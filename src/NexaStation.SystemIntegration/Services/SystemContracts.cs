using NexaStation.SystemIntegration.Models;

namespace NexaStation.SystemIntegration.Services;

public interface ISystemVolumeService{event EventHandler<SystemVolumeChangedEventArgs>? VolumeChanged;Task<SystemVolumeState> GetStateAsync(CancellationToken cancellationToken=default);Task SetVolumeAsync(double normalizedVolume,CancellationToken cancellationToken=default);Task SetMutedAsync(bool muted,CancellationToken cancellationToken=default);}
public interface IAudioOutputDeviceService{event EventHandler<AudioOutputDevicesChangedEventArgs>? DevicesChanged;Task<AudioOutputSnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default);Task<AudioOutputChangeResult> SetDefaultOutputAsync(string deviceId,CancellationToken cancellationToken=default);}
public interface ISystemMediaControlService{event EventHandler<SystemMediaSessionChangedEventArgs>? SessionChanged;Task<SystemMediaSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken=default);Task<MediaControlResult> PlayPauseAsync(CancellationToken cancellationToken=default);Task<MediaControlResult> NextAsync(CancellationToken cancellationToken=default);Task<MediaControlResult> PreviousAsync(CancellationToken cancellationToken=default);}
public interface IControllerStatusService{event EventHandler<ControllerStatusChangedEventArgs>? StatusChanged;Task<IReadOnlyList<ControllerStatus>> GetControllersAsync(CancellationToken cancellationToken=default);}
public interface INetworkStatusService{event EventHandler<NetworkStatusChangedEventArgs>? StatusChanged;Task<NetworkStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default);}
public interface ITimeService{event EventHandler<LocalTimeChangedEventArgs>? TimeChanged;LocalTimeSnapshot Current{get;}void StartUpdates();void StopUpdates();}
public interface ICurrentActivityService{CurrentActivitySnapshot Current{get;}void SetFocusedItem(string? itemId,string? title,string? provider);void SetTrackedActivity(string itemId,string title,string? provider,DateTime startedAtUtc,string confidence);void ClearTrackedActivity();}
public interface INexaStationWindowActions{Task MinimizeAsync();Task ReturnToDesktopAsync();Task RestoreAsync();Task RestartApplicationAsync();}
public interface ISystemPowerService{Task<SystemActionAvailability> GetAvailabilityAsync(SystemPowerAction action,CancellationToken cancellationToken=default);Task<SystemPowerResult> ExecuteAsync(SystemPowerAction action,CancellationToken cancellationToken=default);}
