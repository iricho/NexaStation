namespace NexaStation.Domain;

[Flags]
public enum ArtworkProviderCapabilities
{
    None = 0, TitleSearch = 1 << 0, PlatformSearch = 1 << 1, ExternalIdSearch = 1 << 2,
    GameSerialSearch = 1 << 3, HashSearch = 1 << 4, RegionFiltering = 1 << 5,
    CoverArtwork = 1 << 6, WideArtwork = 1 << 7, HeroArtwork = 1 << 8,
    LogoArtwork = 1 << 9, IconArtwork = 1 << 10, ScreenshotArtwork = 1 << 11,
    Metadata = 1 << 12, MultipleLanguages = 1 << 13, MultipleRegions = 1 << 14,
    UserAuthentication = 1 << 15, QuotaReporting = 1 << 16,
}

public enum ArtworkKind { CoverPortrait, GridWide, HeroBackground, Logo, Icon, Screenshot, Marquee, BoxFront, BoxBack, DiscLabel }
public enum IdentityConfidence { Unknown, FilenameOnly, PlatformAndTitle, VerifiedSerial, VerifiedProviderId, VerifiedHash, ExactExternalId }
public enum MatchDecision { Reject, Review, AutoApply }
public enum MetadataSourceType { User, LocalFile, Launcher, ScreenScraper, SteamGridDb, TheGamesDb, OtherRemote, GeneratedFallback }
public enum ProviderValidationState { Unknown, Valid, Invalid, Expired, Unavailable }
public enum ArtworkJobStatus { Pending, Identifying, Searching, Downloading, AwaitingReview, Paused, RateLimited, Offline, Completed, CompletedWithErrors, Cancelled, Failed }
public enum ArtworkJobItemStatus { Pending, Identifying, Searching, HighConfidenceMatch, AwaitingReview, Downloading, Applied, Skipped, NoMatch, RateLimited, Failed }
public enum ArtworkJobOutcome { None, Running, Succeeded, Partial, NoArtworkFound, NeedsReview, Failed, Cancelled }
public enum ExternalIdentityType { ProviderGameId, SteamAppId, GameSerial, TitleId, RomHash }

public sealed record GameIdentity(
    string LibraryItemId,
    ContentType ContentType,
    string NormalizedTitle,
    string OriginalTitle,
    string? PlatformId = null,
    string? ProviderId = null,
    string? ProviderItemId = null,
    string? SteamAppId = null,
    string? GameSerial = null,
    string? TitleId = null,
    string? Region = null,
    string? Revision = null,
    int? DiscNumber = null,
    string? FileName = null,
    string? FileExtension = null,
    long? FileSize = null,
    IReadOnlyDictionary<string, string>? OptionalHashes = null,
    IdentityConfidence Confidence = IdentityConfidence.Unknown);

public sealed record ArtworkProviderStatus(
    bool Available,
    bool Authenticated,
    string? FailureCode = null,
    string? Message = null,
    int? RemainingQuota = null,
    DateTimeOffset? QuotaResetsAtUtc = null);

public sealed record ArtworkSearchRequest(GameIdentity Identity, string? SearchTitle = null, string? PlatformId = null, string? Region = null, int? ReleaseYear = null, int Limit = 20);
public sealed record ArtworkMatchCandidate(string ProviderId, string ProviderGameId, string Title, string? PlatformId, string? Region, int? ReleaseYear, int Score = 0, MatchDecision Decision = MatchDecision.Review, IReadOnlyList<string>? Reasons = null, IReadOnlyList<string>? Conflicts = null, string? GameSerial = null, string? Revision = null, int? DiscNumber = null, string? Publisher = null, bool IsDlc = false, IReadOnlyDictionary<string, string>? ExternalIds = null, IReadOnlyDictionary<string, string>? Hashes = null);
public sealed record ArtworkAssetRequest(string ProviderGameId, IReadOnlyCollection<ArtworkKind> AssetTypes, string? Language = null, string? Region = null, bool StaticOnly = true);
public sealed record RemoteArtworkAsset(string ProviderId, string ProviderGameId, string AssetId, ArtworkKind Type, Uri DownloadUri, Uri? ThumbnailUri, int? Width, int? Height, long? FileSize, string? MimeType, string? Language = null, string? Region = null, bool Animated = false, IReadOnlyDictionary<string, string>? Attribution = null, string? Licence = null);
public sealed record ArtworkDownloadRequest(string LibraryItemId, string DestinationDirectory, long MaximumBytes = 32L * 1024 * 1024, int MaximumDimension = 16384, int RedirectLimit = 3, TimeSpan? Timeout = null);
public sealed record ArtworkDownloadResult(bool Succeeded, string? LocalPath, string? ContentHash, long FileSize, int? Width, int? Height, string? FailureCode = null, string? Message = null);

public sealed record GameMetadataSearchRequest(GameIdentity Identity, string? SearchTitle = null, int Limit = 20);
public sealed record GameMetadataCandidate(string ProviderId, string ProviderGameId, string Title, string? PlatformId, string? Region, int? ReleaseYear);
public sealed record GameMetadataResult(string ProviderId, string ProviderGameId, IReadOnlyDictionary<string, string?> Fields);

public sealed record ProviderCredential(string ProviderId, string Secret, string? AccountName = null);
public sealed record ProviderResponseMetadata(int StatusCode, DateTimeOffset? RetryAfterUtc = null, int? RemainingQuota = null, DateTimeOffset? QuotaResetsAtUtc = null, bool AuthenticationFailed = false);
public sealed class RateLimitLease(string providerId, DateTimeOffset grantedAtUtc, Action? release = null) : IAsyncDisposable
{
    private Action? _release = release;
    public string ProviderId { get; } = providerId;
    public DateTimeOffset GrantedAtUtc { get; } = grantedAtUtc;
    public ValueTask DisposeAsync() { Interlocked.Exchange(ref _release, null)?.Invoke(); return ValueTask.CompletedTask; }
}

public sealed record ArtworkAssignment(
    string Id, string LibraryItemId, ArtworkKind AssetType, string LocalPath, string ContentHash,
    string? SourceProviderId, string? SourceGameId, string? SourceAssetId, string? SourceUrlHash,
    int? OriginalWidth, int? OriginalHeight, int? DownloadedWidth, int? DownloadedHeight, long FileSize,
    int Confidence, bool UserSelected, bool Locked, IReadOnlyDictionary<string, string>? Attribution,
    string? LicenceMetadata, DateTimeOffset AppliedAtUtc, bool IsCurrent = true, bool HistoryPinned = false);

public sealed record ArtworkJobRequest(string Name, IReadOnlyCollection<string> LibraryItemIds, IReadOnlyCollection<ArtworkKind> AssetTypes, bool FillMissingOnly = true, bool NeverReplaceUserArtwork = true, int AutoApplyThreshold = 900, int MaximumConcurrency = 2);
public sealed record ArtworkJobSnapshot(string Id, string Name, ArtworkJobStatus Status, int TotalItems, int CompletedItems, int MatchedItems, int ReviewItems, int FailedItems, DateTimeOffset CreatedAtUtc, DateTimeOffset? StartedAtUtc, DateTimeOffset? CompletedAtUtc, int AppliedItems = 0, int NoMatchItems = 0, int SkippedItems = 0, int ActiveItems = 0, ArtworkJobOutcome Outcome = ArtworkJobOutcome.None);
public sealed record ArtworkJobItemSnapshot(string Id, string JobId, string LibraryItemId, string Title, ArtworkJobItemStatus Status, string? SelectedProviderId, string? SelectedExternalGameId, int? MatchConfidence, string? FailureCode, string? FailureMessage, DateTimeOffset UpdatedAtUtc);
public sealed record ArtworkProviderReadiness(string ProviderId, string DisplayName, bool Enabled, bool Available, bool Authenticated, ArtworkProviderCapabilities Capabilities, string? FailureCode = null, string? Message = null, int? RemainingQuota = null, DateTimeOffset? QuotaResetsAtUtc = null);
public sealed record ArtworkJobPreflightResult(bool CanStart, int EligibleItems, IReadOnlyList<ArtworkProviderReadiness> Providers, IReadOnlyList<string> BlockingReasons, IReadOnlyList<string> Warnings);
public sealed record ArtworkProviderConnectionTestResult(string ProviderId, ProviderValidationState ValidationState, string Message, int? RemainingQuota = null, DateTimeOffset? QuotaResetsAtUtc = null);
public sealed record ArtworkReviewItem(string JobId, string JobItemId, string LibraryItemId, string LocalTitle, string? PlatformId, string? GameSerial, string? FileName, IReadOnlyList<ArtworkMatchCandidate> Candidates);
public sealed record ExternalGameIdentity(string Id, string LibraryItemId, string ProviderId, string ExternalGameId, ExternalIdentityType IdentityType, IdentityConfidence Confidence, bool ConfirmedByUser, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record MetadataFieldOrigin(string LibraryItemId, string FieldName, MetadataSourceType SourceType, string? SourceProviderId, string? SourceRecordId, DateTimeOffset AppliedAtUtc, int Confidence, bool UserModifiedAfterApply, bool Locked);
