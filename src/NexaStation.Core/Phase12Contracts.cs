using NexaStation.Domain;

namespace NexaStation.Core;

public interface IActivityTimelineService
{
    Task RecordAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityEvent>> QueryAsync(ActivityQuery query, CancellationToken cancellationToken = default);
    Task DeleteAsync(string activityEventId, CancellationToken cancellationToken = default);
    Task ClearAsync(ActivityClearRequest request, CancellationToken cancellationToken = default);
    Task<ActivityPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default);
    Task SavePreferencesAsync(ActivityPreferences preferences, CancellationToken cancellationToken = default);
}
public interface IActivityRetentionService { Task<ActivityRetentionResult> ApplyAsync(bool force = false, CancellationToken cancellationToken = default); }
public interface IActivityExportService { Task ExportAsync(ActivityExportRequest request, CancellationToken cancellationToken = default); }
public interface IActivityPrivacyService { Task<ActivityPrivacyLevel> EvaluateAsync(LibraryItem item, CancellationToken cancellationToken = default); }
public interface ICaptureConsentService { CaptureGesture IssueForDirectUserAction(string action); bool Consume(string token, string expectedAction); void RevokeAll(); }
public interface IScreenshotCapturePlatform { Task<EncodedCaptureFrame> CaptureAsync(ScreenshotCaptureRequest request, CancellationToken cancellationToken = default); }
public interface IScreenshotCaptureService { Task<CaptureAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default); Task<ScreenshotCaptureResult> CaptureAsync(ScreenshotCaptureRequest request, CancellationToken cancellationToken = default); }
public interface ICapturedMediaStore { Task SaveAsync(CapturedMedia media, CancellationToken cancellationToken = default); Task<CapturedMedia?> GetByPathAsync(string normalizedPath, CancellationToken cancellationToken = default); }
public interface IGlobalHotkeyService : IDisposable { event EventHandler<GlobalHotkeyPressedEventArgs>? Pressed; Task<HotkeyRegistrationResult> RegisterAsync(GlobalHotkeyDefinition definition, CancellationToken cancellationToken = default); Task UnregisterAsync(string hotkeyId, CancellationToken cancellationToken = default); Task UnregisterAllAsync(CancellationToken cancellationToken = default); }
public interface ISnippingToolService { Task<bool> OpenAsync(bool recording, CancellationToken cancellationToken = default); }
public interface IRecordingFolderMonitor : IAsyncDisposable { Task StartAsync(IReadOnlyList<string> explicitlyApprovedFolders, CancellationToken cancellationToken = default); Task StopAsync(CancellationToken cancellationToken = default); }
public interface IRemotePlayProfileStore { Task<IReadOnlyList<RemotePlayProfile>> GetAsync(CancellationToken cancellationToken = default); Task<RemotePlayProfile?> GetAsync(string id, CancellationToken cancellationToken = default); Task SaveAsync(RemotePlayProfile profile, CancellationToken cancellationToken = default); Task DeleteAsync(string id, CancellationToken cancellationToken = default); }
public interface IRemotePlayLaunchService { Task<RemotePlayValidationResult> ValidateAsync(string profileId, CancellationToken cancellationToken = default); Task<RemotePlayLaunchResult> LaunchAsync(string profileId, CancellationToken cancellationToken = default); }
public interface IPresenceProvider { string Id { get; } Task<PresenceAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default); Task<PresenceResult> SetAsync(PresencePayload payload, CancellationToken cancellationToken = default); Task ClearAsync(CancellationToken cancellationToken = default); }
public interface IPresenceCoordinator { Task SetForLaunchAsync(LibraryItem item, ActivityPrivacyLevel privacy, string? provider, DateTime startedAtUtc, CancellationToken cancellationToken = default); Task ClearAsync(CancellationToken cancellationToken = default); }
public interface IAchievementProvider { string Id { get; } Task<AchievementProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default); Task<IReadOnlyList<GameAchievement>> GetAchievementsAsync(AchievementRequest request, CancellationToken cancellationToken = default); }
public interface IEcosystemCapabilityService { Task<EcosystemCapabilitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default); Task RefreshAsync(EcosystemCapabilityRefreshReason reason, CancellationToken cancellationToken = default); }
