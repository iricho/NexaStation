namespace NexaStation.SystemIntegration.Models;

public sealed record SystemVolumeState(double Volume,bool Muted,string? EndpointId,string? EndpointName,bool Available,string? FailureReason=null);
public sealed class SystemVolumeChangedEventArgs(SystemVolumeState state):EventArgs{public SystemVolumeState State{get;}=state;}
public sealed record AudioOutputDevice(string Id,string DisplayName,string DeviceState,bool IsDefault,string? DeviceType,bool Available=true);
public sealed record AudioOutputSnapshot(IReadOnlyList<AudioOutputDevice> Devices,string? DefaultDeviceId,bool SwitchingSupported,string? FailureReason=null);
public sealed record AudioOutputChangeResult(bool Succeeded,bool OpenedSettings,string? FailureReason=null);
public sealed class AudioOutputDevicesChangedEventArgs(AudioOutputSnapshot snapshot):EventArgs{public AudioOutputSnapshot Snapshot{get;}=snapshot;}
public sealed record SystemMediaSessionSnapshot(bool Available,string? ApplicationName,string? Title,string? Artist,bool? IsPlaying,string? FailureReason=null);
public sealed record MediaControlResult(bool Succeeded,string? FailureReason=null);
public sealed class SystemMediaSessionChangedEventArgs(SystemMediaSessionSnapshot snapshot):EventArgs{public SystemMediaSessionSnapshot Snapshot{get;}=snapshot;}
public enum ControllerBatteryState{Unknown,Wired,Charging,Low,Medium,Full}
public sealed record ControllerStatus(string SessionId,string DisplayName,string ControllerFamily,bool Connected,bool? Wireless,ControllerBatteryState BatteryState,int? BatteryPercentage,int? PlayerIndex,bool ActiveInputDevice,DateTime? LastInputAtUtc);
public sealed class ControllerStatusChangedEventArgs(IReadOnlyList<ControllerStatus> controllers):EventArgs{public IReadOnlyList<ControllerStatus> Controllers{get;}=controllers;}
public sealed record NetworkStatusSnapshot(bool NetworkAvailable,string InternetAccessLevel,string? ConnectionProfileName,string? ConnectionType,bool? Metered,bool? Roaming,DateTime UpdatedAtUtc,string? FailureReason=null);
public sealed class NetworkStatusChangedEventArgs(NetworkStatusSnapshot snapshot):EventArgs{public NetworkStatusSnapshot Snapshot{get;}=snapshot;}
public sealed record LocalTimeSnapshot(DateTimeOffset LocalTime,string DisplayTime,string DisplayDate);
public sealed class LocalTimeChangedEventArgs(LocalTimeSnapshot snapshot):EventArgs{public LocalTimeSnapshot Snapshot{get;}=snapshot;}
public sealed record CurrentActivitySnapshot(string Title,string? Provider,TimeSpan? Duration,string TrackingConfidence,string? ItemId=null);
public enum SystemPowerAction{Sleep,SignOut,Restart,ShutDown}
public sealed record SystemActionAvailability(bool Available,string? UnavailableReason=null);
public sealed record SystemPowerResult(bool Succeeded,string? FailureReason=null);
