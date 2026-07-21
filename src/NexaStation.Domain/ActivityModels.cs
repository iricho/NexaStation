namespace NexaStation.Domain;

public enum ActivityEventType { GameStarted, GameEnded, ApplicationOpened, ApplicationEnded, MediaOpened, ScreenshotCaptured, RecordingCreated, RemotePlayStarted, RemotePlayEnded, ArtworkUpdated, MetadataUpdated, AchievementUnlocked, ProviderImportCompleted, GallerySourceAdded, UpdateInstalled, ErrorRecovered }
public enum ActivityTargetType { None, LibraryItem, MediaItem, Capture, RemotePlayProfile, Achievement, Provider, System }
public enum ActivityResult { Succeeded, Failed, Cancelled, Interrupted, Unknown }
public enum ActivityPrivacyLevel { Normal, Private, GuestTemporary, Hidden }
public enum ActivityHistoryMode { Enabled, Disabled, GamesOnly, GamesAndApplications, EverythingExceptPrivateContent }

public sealed record ActivityEvent(string Id, ActivityEventType EventType, ActivityTargetType TargetType, string? TargetId, string? SessionId, string? TitleSnapshot, string? SubtitleSnapshot, string? ProviderId, string? PlatformId, DateTime OccurredAtUtc, DateTime? EndedAtUtc, long? DurationSeconds, ActivityResult Result, ActivityPrivacyLevel PrivacyLevel, string? GuestSessionId = null, string? MetadataJson = null, string? CorrelationId = null, DateTime CreatedAtUtc = default);
public sealed record ActivityQuery(IReadOnlySet<ActivityEventType>? EventTypes = null, string? TargetId = null, string? ProviderId = null, string? PlatformId = null, DateTime? FromUtc = null, DateTime? ToUtc = null, bool IncludePrivate = false, bool IncludeGuest = false, bool IncludeHidden = false, int Offset = 0, int MaximumResults = 100);
public sealed record ActivityClearRequest(string? TargetId = null, ActivityEventType? EventType = null, bool GuestOnly = false, bool IncludePrivate = false, bool ClearAll = false);
public sealed record ActivityPreferences(ActivityHistoryMode HistoryMode = ActivityHistoryMode.GamesAndApplications, int RetentionDays = 365, int MaximumEventCount = 20_000, bool KeepGameSessions = true, bool KeepCaptureEvents = true, bool KeepArtworkChanges = false, DateTime? LastRetentionAtUtc = null);
public sealed record ActivityExportRequest(string Format, string DestinationPath, ActivityQuery Query, bool IncludeTitles = true, bool IncludeProvider = true, bool IncludeDuration = true, bool IncludePrivate = false, bool IncludeLocalPaths = false);
public sealed record ActivityRetentionResult(int Deleted, bool Ran, DateTime CompletedAtUtc);

public enum CaptureSourceMode { SystemPicker, ActiveTrackedWindow, CurrentDisplay, NexaStationWindow, SnippingToolFallback }
public enum CaptureOutputFormat { Png, Jpeg, Video }
public enum CaptureType { Screenshot, Recording }
public enum CaptureSource { NexaStation, SnippingTool, XboxGameBar, ExternalRecorder, ImportedFolder }
public enum CaptureAvailabilityState { Available, Unavailable, PermissionRequired }
public enum CaptureFailureCode { None, Cancelled, ConsentRequired, Unavailable, ProtectedContent, InvalidTarget, InvalidDimensions, InsufficientSpace, EncodingFailed, StorageFailed, ValidationFailed }
public sealed record CaptureAvailability(CaptureAvailabilityState State, string Message, IReadOnlyList<CaptureSourceMode> Modes);
public sealed record ScreenshotCaptureRequest(CaptureSourceMode SourceMode, string? TargetWindowIdentity, string? TargetDisplayId, bool UseSystemPicker, bool IncludeCursor, CaptureOutputFormat OutputFormat, int JpegQuality, string DestinationFolder, string? TargetLibraryItemId, string? ActivitySessionId, ActivityPrivacyLevel PrivacyLevel, bool ImportToGallery, bool DirectUserAction, string UserGestureToken, string? SanitizedTitle = null);
public sealed record ScreenshotCaptureResult(bool Succeeded, bool Cancelled, string? LocalPath, string? CapturedMediaId, int Width, int Height, long FileSize, string? ContentHash, CaptureFailureCode FailureCode, string? Message);
public sealed record CaptureGesture(string Token, string Action, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);
public sealed record EncodedCaptureFrame(byte[] Bytes, int Width, int Height, CaptureOutputFormat Format);
public sealed record CaptureSettings(string? ScreenshotFolder = null, CaptureOutputFormat Format = CaptureOutputFormat.Png, int JpegQuality = 92, bool IncludeCursor = false, bool AutomaticallyAddToGallery = true, bool ShowNotification = true, bool IncludeSanitizedTitle = false, bool GlobalHotkeyEnabled = false, string? GlobalHotkey = null);
public sealed record CapturedMedia(string Id, CaptureType CaptureType, string LocalPath, string NormalizedLocalPath, string? TargetLibraryItemId, string? ActivitySessionId, DateTime CapturedAtUtc, int Width, int Height, long FileSize, CaptureOutputFormat FileFormat, CaptureSource CaptureSource, bool IncludesCursor, bool ImportedToGallery, ActivityPrivacyLevel PrivacyLevel, string ContentHash, DateTime CreatedAtUtc);

public sealed record GlobalHotkeyDefinition(string Id, uint Modifiers, uint VirtualKey, bool Enabled, bool UserApproved);
public sealed record HotkeyRegistrationResult(bool Succeeded, bool Conflict, string Message);
public sealed class GlobalHotkeyPressedEventArgs(string hotkeyId) : EventArgs { public string HotkeyId { get; } = hotkeyId; }

public enum RemotePlayServiceType { SteamLink, Moonlight, Parsec, Custom, Unknown }
public sealed record RemotePlayProfile(string Id, string Name, string ApplicationLibraryItemId, string? LaunchTargetId, RemotePlayServiceType ServiceType, string? TargetLabel, string? HostHint, string? ArgumentsTemplate, string? PreferredDisplayId, bool HideFrontend, bool RestoreFrontend, ApplicationTrackingMode TrackingMode, string? DesktopControlProfileId, bool ShowOnHome, bool ShowInMedia, bool Enabled, bool UserManaged, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record RemotePlaySession(string Id, string ProfileId, DateTime StartedAtUtc, DateTime? EndedAtUtc, long? DurationSeconds, ActivityResult Result, string? FailureCode);
public sealed record RemotePlayValidationResult(bool Valid, string Message, IReadOnlyList<string> RenderedArguments);
public sealed record RemotePlayLaunchResult(bool Succeeded, string? SessionId, string Message);

public enum PresenceAvailabilityState { Available, Unavailable, Disabled, Misconfigured }
public sealed record PresenceAvailability(PresenceAvailabilityState State, string Message);
public sealed record PresencePayload(string Details, string State, string? Platform, string? Provider, DateTime? StartedAtUtc, string? SafeProjectUrl = null);
public sealed record PresenceResult(bool Succeeded, string Message);
public sealed record PresenceSettings(string Provider = "discord", bool Enabled = false, bool BrowsingPresenceEnabled = false, bool GamePresenceEnabled = false, bool IncludeProvider = false, bool IncludePlatform = false, bool IncludeElapsedTime = false, bool IncludeButtons = false, bool ClearOnGuestMode = true, bool ClearOnPrivateContent = true, DateTime UpdatedAtUtc = default);

public enum CapabilityStatus { Supported, Experimental, Limited, NotAvailable, RequiresApproval, Unsafe, Unknown }
public sealed record AchievementProviderCapability(string ProviderId, CapabilityStatus CapabilityStatus, string AuthenticationType, bool CanReadDefinitions, bool CanReadUserProgress, bool CanReadUnlockTimes, bool RequiresPublicProfile, bool RequiresDeveloperRegistration, bool RequiresUserCredential, bool SupportsOfflineCache, DateTime LastInvestigatedAtUtc, string Notes);
public sealed record AchievementProviderStatus(CapabilityStatus Status, string Message);
public sealed record AchievementRequest(string LibraryItemId, string? ProviderGameId = null);
public sealed record GameAchievement(string ProviderAchievementId, string LibraryItemId, string Name, string? Description, string? IconPath, string? LockedIconPath, bool Unlocked, DateTime? UnlockedAtUtc, bool Hidden, double? ProgressCurrent, double? ProgressMaximum, string ProviderId, string Provenance, DateTime RetrievedAtUtc);
public sealed record DownloadProviderCapability(string ProviderId, CapabilityStatus Status, bool CanDetectActive, bool CanIdentifyGame, bool HasTotalBytes, bool HasCompletedBytes, bool HasPercentage, bool HasPauseState, bool HasUpdateState, string SourceReliability, string DataSource, string Notes);
public sealed record EcosystemCapabilitySnapshot(CaptureAvailability ScreenshotCapture, bool GameBarAvailable, IReadOnlyList<string> RecorderApplications, bool MediaSessionsAvailable, PresenceAvailability DiscordPresence, IReadOnlyList<AchievementProviderCapability> Achievements, IReadOnlyList<DownloadProviderCapability> DownloadVisibility, int RemotePlayProfileCount, bool MobileRemotePrototypeEnabled, DateTime RefreshedAtUtc, int Version);
public enum EcosystemCapabilityRefreshReason { UserRequested, VersionChanged, SettingsChanged, Recovery }
