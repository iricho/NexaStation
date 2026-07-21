using System.Net;
using System.Text;
using NexaStation.Release;
using NexaStation.Recovery;
using NexaStation.Updates;
using Xunit;

namespace NexaStation.Updates.Tests;

public sealed class UpdateTests
{
    private static readonly Uri ManifestUri = new("https://updates.invalid/nexastation.json");

    [Fact]
    public async Task DevelopmentChecksAreOffByDefaultAndDoNotTouchNetwork()
    {
        CountingHandler handler = new("{}");
        ManifestUpdateService service = new(new HttpClient(handler), ManifestUri);
        UpdateCheckResult result = await service.CheckAsync(new(0, 8, 0), UpdatePreferences.Default);
        Assert.Equal(UpdateCheckStatus.Disabled, result.Status);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task NewerAlphaVersionIsReportedWithoutInstallingIt()
    {
        CountingHandler handler = new("{\"version\":\"0.8.1\",\"channel\":\"Alpha\",\"installerUri\":\"https://updates.invalid/NexaStation.appinstaller\"}");
        ManifestUpdateService service = new(new HttpClient(handler), ManifestUri);
        UpdateCheckResult result = await service.CheckAsync(new(0, 8, 0), new(true, ReleaseChannel.Alpha));
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new SemanticVersion(0, 8, 1), result.AvailableVersion);
        Assert.NotNull(result.InstallerUri);
    }

    [Fact]
    public async Task WrongChannelAndOlderVersionsAreNotOffered()
    {
        CountingHandler handler = new("{\"version\":\"99.0.0\",\"channel\":\"Development\",\"installerUri\":\"https://updates.invalid/NexaStation.appinstaller\"}");
        UpdateCheckResult result = await new ManifestUpdateService(new HttpClient(handler), ManifestUri).CheckAsync(new(0, 8, 0), new(true, ReleaseChannel.Alpha));
        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.InstallerUri);
    }

    [Fact]
    public async Task NetworkFailuresAreReturnedAsUserVisibleStatus()
    {
        ManifestUpdateService service = new(new HttpClient(new ThrowingHandler()), ManifestUri);
        UpdateCheckResult result = await service.CheckAsync(new(0, 8, 0), new(true, ReleaseChannel.Alpha));
        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task UpdatePreparationIsDeferredDuringAnActiveLaunch()
    {
        FakeBackup backup = new();
        FakeWriteGate writes = new();
        UpdateReliabilityCoordinator coordinator = new(backup, writes, new FakeMarker(), new FakeHealthProbe(true));

        UpdatePreparationResult result = await coordinator.PrepareAsync(new("library.db", "0.9.1", true));

        Assert.Equal(UpdatePreparationStatus.DeferredForActiveLaunch, result.Status);
        Assert.Equal(0, backup.CreateCount);
        Assert.Equal(0, writes.SuspendCount);
    }

    [Fact]
    public async Task UpdatePreparationSuspendsWritesBacksUpAndMarksRestartInOrder()
    {
        List<string> calls = [];
        FakeBackup backup = new(calls);
        FakeWriteGate writes = new(calls);
        FakeMarker marker = new(calls);
        UpdateReliabilityCoordinator coordinator = new(backup, writes, marker, new FakeHealthProbe(true));

        UpdatePreparationResult result = await coordinator.PrepareAsync(new("library.db", "0.9.1", false));

        Assert.Equal(UpdatePreparationStatus.Ready, result.Status);
        Assert.Equal("known-good.nxbackup", result.BackupPath);
        Assert.Equal(["suspend", "backup", "mark", "resume"], calls);
    }

    [Fact]
    public async Task FailedPostUpdateHealthKeepsMarkerAndRecommendsRecovery()
    {
        FakeMarker marker = new() { Current = new(1, "0.9.1", "known-good.nxbackup", DateTimeOffset.UtcNow) };
        UpdateReliabilityCoordinator coordinator = new(new FakeBackup(), new FakeWriteGate(), marker, new FakeHealthProbe(false));

        PostUpdateValidationResult result = await coordinator.ValidateAfterStartupAsync();

        Assert.False(result.Healthy);
        Assert.True(result.RecoveryRecommended);
        Assert.NotNull(marker.Current);
    }

    [Fact]
    public async Task HealthyPostUpdateStartupClearsMarkerButRetainsBackup()
    {
        FakeMarker marker = new() { Current = new(1, "0.9.1", "known-good.nxbackup", DateTimeOffset.UtcNow) };
        UpdateReliabilityCoordinator coordinator = new(new FakeBackup(), new FakeWriteGate(), marker, new FakeHealthProbe(true));

        PostUpdateValidationResult result = await coordinator.ValidateAfterStartupAsync();

        Assert.True(result.Healthy);
        Assert.False(result.RecoveryRecommended);
        Assert.Null(marker.Current);
    }

    private sealed class CountingHandler(string response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new HttpRequestException("offline");
    }

    private sealed class FakeBackup(List<string>? calls = null) : IDatabaseBackupService
    {
        public int CreateCount { get; private set; }
        public Task<BackupResult> CreateAsync(string databasePath, string reason, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            calls?.Add("backup");
            return Task.FromResult(new BackupResult("known-good.nxbackup", new(1, "NexaStation", DateTimeOffset.UtcNow, reason, "data/nexastation.db", "hash", 1)));
        }
        public Task RestoreAsync(string backupPath, string databasePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeWriteGate(List<string>? calls = null) : IProviderWriteGate
    {
        public int SuspendCount { get; private set; }
        public Task<IAsyncDisposable> SuspendWritesAsync(CancellationToken cancellationToken = default)
        {
            SuspendCount++;
            calls?.Add("suspend");
            return Task.FromResult<IAsyncDisposable>(new Lease(calls));
        }
        private sealed class Lease(List<string>? calls) : IAsyncDisposable
        {
            public ValueTask DisposeAsync() { calls?.Add("resume"); return ValueTask.CompletedTask; }
        }
    }

    private sealed class FakeMarker(List<string>? calls = null) : IPlannedRestartMarker
    {
        public PlannedUpdateRestart? Current { get; set; }
        public Task WriteAsync(string targetVersion, string backupPath, CancellationToken cancellationToken = default)
        {
            calls?.Add("mark");
            Current = new(1, targetVersion, backupPath, DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        }
        public Task<PlannedUpdateRestart?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task ClearAsync(CancellationToken cancellationToken = default) { Current = null; return Task.CompletedTask; }
    }

    private sealed class FakeHealthProbe(bool healthy) : IPostUpdateHealthProbe
    {
        public Task<(bool Healthy, string Message)> ValidateAsync(CancellationToken cancellationToken = default) => Task.FromResult((healthy, healthy ? "healthy" : "database check failed"));
    }
}
