using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Capture;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Infrastructure.RemotePlay;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class Phase12InfrastructureTests
{
    [Fact]
    public async Task Phase12MigrationsAreAppliedInOrder()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create();
        Assert.Equal("55,56,57,58,59,60,61,62,63,64", await db.Text("SELECT group_concat(Version,',') FROM (SELECT Version FROM SchemaMigrations WHERE Version BETWEEN 55 AND 64 ORDER BY Version)"));
        Assert.Equal((long)ActivityHistoryMode.GamesAndApplications, await db.Scalar("SELECT HistoryMode FROM ActivityPreferences WHERE Id=1"));
    }

    [Fact]
    public async Task CaptureRejectsMissingDirectActionWithoutTouchingPlatformOrStores()
    {
        SessionCaptureConsentService consent = new();
        RecordingCapturePlatform platform = new();
        RecordingMediaStore media = new();
        RecordingTimeline timeline = new();
        UserInitiatedScreenshotCaptureService service = new(consent, platform, media, timeline, new NullGallery());
        ScreenshotCaptureRequest request = Request(Path.GetTempPath(), false, string.Empty);

        ScreenshotCaptureResult result = await service.CaptureAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(CaptureFailureCode.ConsentRequired, result.FailureCode);
        Assert.Equal(0, platform.Calls);
        Assert.Empty(media.Items);
        Assert.Empty(timeline.Items);
    }

    [Fact]
    public async Task DirectActionTokenIsActionBoundAndSingleUse()
    {
        SessionCaptureConsentService consent = new();
        CaptureGesture wrong = consent.IssueForDirectUserAction("OpenSnippingTool");
        Assert.False(consent.Consume(wrong.Token, "CaptureScreenshot"));
        CaptureGesture valid = consent.IssueForDirectUserAction("CaptureScreenshot");
        Assert.True(consent.Consume(valid.Token, "CaptureScreenshot"));
        Assert.False(consent.Consume(valid.Token, "CaptureScreenshot"));
    }

    [Fact]
    public async Task SuccessfulSyntheticCapturePersistsOnlyAfterValidatedFrame()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NexaStation.Phase12.Capture", Guid.NewGuid().ToString("N"));
        try
        {
            SessionCaptureConsentService consent = new();
            RecordingCapturePlatform platform = new();
            RecordingMediaStore media = new();
            RecordingTimeline timeline = new();
            UserInitiatedScreenshotCaptureService service = new(consent, platform, media, timeline, new NullGallery());
            CaptureGesture gesture = consent.IssueForDirectUserAction("CaptureScreenshot");

            ScreenshotCaptureResult first = await service.CaptureAsync(Request(folder, true, gesture.Token));
            ScreenshotCaptureResult replay = await service.CaptureAsync(Request(folder, true, gesture.Token));

            Assert.True(first.Succeeded);
            Assert.True(File.Exists(first.LocalPath));
            Assert.Single(media.Items);
            Assert.Single(timeline.Items);
            Assert.Equal(ActivityEventType.ScreenshotCaptured, timeline.Items[0].EventType);
            Assert.Equal(CaptureFailureCode.ConsentRequired, replay.FailureCode);
            Assert.Equal(1, platform.Calls);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task TimelineDeduplicatesSessionEventsAndFiltersPrivateAndGuestByDefault()
    {
        await using PersistenceTests.TestDb db = await PersistenceTests.TestDb.Create();
        SqlitePhase12Store store = new(db.Factory);
        DateTime now = DateTime.UtcNow;
        await store.RecordAsync(Event("one", "session", ActivityPrivacyLevel.Normal, now));
        await store.RecordAsync(Event("duplicate", "session", ActivityPrivacyLevel.Normal, now.AddSeconds(1)));
        await store.RecordAsync(Event("private", null, ActivityPrivacyLevel.Private, now.AddSeconds(2)));
        await store.RecordAsync(Event("guest", null, ActivityPrivacyLevel.GuestTemporary, now.AddSeconds(3)));

        IReadOnlyList<NexaStation.Domain.ActivityEvent> visible = await store.QueryAsync(new ActivityQuery());
        IReadOnlyList<NexaStation.Domain.ActivityEvent> all = await store.QueryAsync(new ActivityQuery(IncludePrivate: true, IncludeGuest: true));

        Assert.Single(visible);
        Assert.Equal(3, all.Count);
        Assert.Equal("one", visible[0].Id);
    }

    [Fact]
    public async Task ActivityExportExcludesPrivateRowsAndLocalMetadataByDefault()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NexaStation.Phase12.Export", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "activity.json");
        try
        {
            RecordingTimeline timeline = new();
            timeline.Items.Add(Event("normal", null, ActivityPrivacyLevel.Normal, DateTime.UtcNow) with { MetadataJson = "{\"LocalPath\":\"C:\\\\secret.png\"}" });
            timeline.Items.Add(Event("private", null, ActivityPrivacyLevel.Private, DateTime.UtcNow));
            LocalActivityExportService export = new(timeline);

            await export.ExportAsync(new("json", path, new ActivityQuery(), IncludePrivate: false, IncludeLocalPaths: false));
            string json = await File.ReadAllTextAsync(path);

            Assert.Contains("normal", json, StringComparison.Ordinal);
            Assert.DoesNotContain("private", json, StringComparison.Ordinal);
            Assert.DoesNotContain("secret.png", json, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("--host {HOST_HINT} & calc.exe")]
    [InlineData("--host %COMPUTERNAME%")]
    [InlineData("--host $(whoami)")]
    [InlineData("--host {UNKNOWN}")]
    public void RemotePlayArgumentsRejectShellExpansionAndUnknownVariables(string arguments)
    {
        Assert.Throws<InvalidOperationException>(() => RemotePlayArgumentTemplate.Render(arguments, Profile()));
    }

    [Fact]
    public void RemotePlayArgumentsRenderOnlyAllowlistedVariablesAsArgumentList()
    {
        IReadOnlyList<string> values = RemotePlayArgumentTemplate.Render("--host \"{HOST_HINT}\" --width {DISPLAY_WIDTH}", Profile(), 1920, 1080);
        Assert.Equal(["--host", "living room", "--width", "1920"], values);
    }

    private static ScreenshotCaptureRequest Request(string folder, bool direct, string token) => new(CaptureSourceMode.NexaStationWindow, "1", null, false, false, CaptureOutputFormat.Png, 92, folder, null, null, ActivityPrivacyLevel.Normal, false, direct, token, "Test");
    private static NexaStation.Domain.ActivityEvent Event(string id, string? session, ActivityPrivacyLevel privacy, DateTime at) => new(id, ActivityEventType.GameStarted, ActivityTargetType.LibraryItem, "game", session, "Game", null, "local", "windows", at, null, null, ActivityResult.Succeeded, privacy, CreatedAtUtc: at);
    private static RemotePlayProfile Profile() { DateTime now = DateTime.UtcNow; return new("profile", "Living room", "app", null, RemotePlayServiceType.Moonlight, "PC", "living room", null, null, false, true, ApplicationTrackingMode.LaunchedProcess, null, true, true, true, true, now, now); }

    private sealed class RecordingCapturePlatform : IScreenshotCapturePlatform
    {
        public int Calls { get; private set; }
        public Task<EncodedCaptureFrame> CaptureAsync(ScreenshotCaptureRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130];
            return Task.FromResult(new EncodedCaptureFrame(png, 1, 1, CaptureOutputFormat.Png));
        }
    }

    private sealed class RecordingMediaStore : ICapturedMediaStore
    {
        public List<CapturedMedia> Items { get; } = [];
        public Task SaveAsync(CapturedMedia media, CancellationToken cancellationToken = default) { Items.Add(media); return Task.CompletedTask; }
        public Task<CapturedMedia?> GetByPathAsync(string normalizedPath, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(value => value.NormalizedLocalPath == normalizedPath));
    }

    private sealed class RecordingTimeline : IActivityTimelineService
    {
        public List<NexaStation.Domain.ActivityEvent> Items { get; } = [];
        public Task RecordAsync(NexaStation.Domain.ActivityEvent activityEvent, CancellationToken cancellationToken = default) { Items.Add(activityEvent); return Task.CompletedTask; }
        public Task<IReadOnlyList<NexaStation.Domain.ActivityEvent>> QueryAsync(ActivityQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NexaStation.Domain.ActivityEvent>>(Items.Where(value => (query.IncludePrivate || value.PrivacyLevel != ActivityPrivacyLevel.Private) && (query.IncludeGuest || value.PrivacyLevel != ActivityPrivacyLevel.GuestTemporary) && (query.IncludeHidden || value.PrivacyLevel != ActivityPrivacyLevel.Hidden)).Skip(query.Offset).Take(query.MaximumResults).ToArray());
        public Task DeleteAsync(string activityEventId, CancellationToken cancellationToken = default) { Items.RemoveAll(value => value.Id == activityEventId); return Task.CompletedTask; }
        public Task ClearAsync(ActivityClearRequest request, CancellationToken cancellationToken = default) { Items.Clear(); return Task.CompletedTask; }
        public Task<ActivityPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ActivityPreferences());
        public Task SavePreferencesAsync(ActivityPreferences preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullGallery : IGalleryLibraryService
    {
        public Task<IReadOnlyList<GallerySourceOverlap>> PreviewSourceAsync(string folderPath, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GallerySourceOverlap>>([]);
        public Task<GallerySource> AddSourceAsync(GallerySource source, bool userApproved, bool scanImmediately, CancellationToken cancellationToken = default) => Task.FromResult(source);
        public Task<GalleryRefreshResult> RefreshSourceAsync(string sourceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GalleryMediaItem?> IndexApprovedFileAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult<GalleryMediaItem?>(null);
        public Task RemoveSourceAsync(string sourceId, bool keepCachedThumbnails, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
