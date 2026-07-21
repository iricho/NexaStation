using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.Data.Sqlite;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Launchers;
using NexaStation.Infrastructure.Metadata;
using NexaStation.Infrastructure.Persistence;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Infrastructure.Tests;

public sealed class Phase11MetadataInfrastructureTests
{
    [Fact]
    public async Task AppliesImmutableMigrationsThrough54()
    {
        await using Context context = await Context.CreateAsync();
        await using SqliteConnection connection = await context.Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Version,Name FROM SchemaMigrations WHERE Version BETWEEN 45 AND 54 ORDER BY Version;";
        List<(int Version, string Name)> migrations = []; await using SqliteDataReader reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) migrations.Add((reader.GetInt32(0), reader.GetString(1)));
        Assert.Equal(10, migrations.Count); Assert.Equal("045_MetadataProviders", migrations[0].Name); Assert.Equal("054_ProviderDiscoveryState", migrations[^1].Name);
    }

    [Fact] public async Task SeedsOnlyLocalMetadataProviderEnabledByDefault() { await using Context c = await Context.CreateAsync(); await using SqliteConnection connection = await c.Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT group_concat(Id,',') FROM MetadataProviders WHERE Enabled=1;"; Assert.Equal("local", await command.ExecuteScalarAsync()); }
    [Fact] public async Task CredentialReferenceSchemaHasNoSecretColumn() { await using Context c = await Context.CreateAsync(); await using SqliteConnection connection = await c.Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT group_concat(name,',') FROM pragma_table_info('ProviderCredentialReferences');"; string columns = Assert.IsType<string>(await command.ExecuteScalarAsync()); Assert.DoesNotContain("Secret", columns, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("ApiKey", columns, StringComparison.OrdinalIgnoreCase); }

    [Fact]
    public async Task ManagedLocalImportIsAtomicAndContentAddressed()
    {
        await using Context context = await Context.CreateAsync(); string source = context.WritePng("source.png");
        using RemoteArtworkDownloader downloader = new(); ArtworkDownloadRequest request = new("item", Path.Combine(context.Paths.Data, "Artwork"));
        ArtworkDownloadResult first = await downloader.ImportLocalAsync(source, ArtworkKind.CoverPortrait, request); ArtworkDownloadResult second = await downloader.ImportLocalAsync(source, ArtworkKind.CoverPortrait, request);
        Assert.True(first.Succeeded); Assert.Equal(first.LocalPath, second.LocalPath); Assert.True(File.Exists(first.LocalPath)); Assert.Empty(Directory.EnumerateFiles(request.DestinationDirectory, "*.download", SearchOption.AllDirectories));
    }

    [Fact] public async Task NonHttpsRemoteArtworkIsRejectedBeforeRequest() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "image/png", ValidPng()); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(new("http://example.test/image.png")), Request(c)); Assert.False(result.Succeeded); Assert.Equal("non_https", result.FailureCode); Assert.Equal(0, handler.Requests); }
    [Fact] public async Task HtmlMasqueradingAsImageIsRejected() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "image/png", Encoding.UTF8.GetBytes("<html>not an image</html>")); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), Request(c)); Assert.False(result.Succeeded); Assert.Equal("invalid_signature", result.FailureCode); }
    [Fact] public async Task InvalidMimeIsRejected() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "text/html", ValidPng()); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), Request(c)); Assert.False(result.Succeeded); Assert.Equal("invalid_mime", result.FailureCode); }
    [Fact] public async Task OversizedDeclaredFileIsRejected() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "image/png", ValidPng(), 50_000); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), Request(c) with { MaximumBytes = 1024 }); Assert.False(result.Succeeded); Assert.Equal("oversized", result.FailureCode); }
    [Fact] public async Task ExcessiveDimensionsAreRejected() { await using Context c = await Context.CreateAsync(); byte[] png = ValidPng(); WriteBigEndian(png, 16, 20000); CountingHandler handler = new(HttpStatusCode.OK, "image/png", png); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), Request(c)); Assert.False(result.Succeeded); Assert.Equal("excessive_dimensions", result.FailureCode); }
    [Fact] public async Task CorruptPngIsRejectedAndTemporaryFileRemoved() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "image/png", ValidPng()[..24]); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadRequest request = Request(c); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), request); Assert.False(result.Succeeded); Assert.Equal("corrupt_image", result.FailureCode); Assert.Empty(Directory.EnumerateFiles(request.DestinationDirectory, "*.download", SearchOption.AllDirectories)); }
    [Fact] public async Task ValidRemotePngMovesToManagedStore() { await using Context c = await Context.CreateAsync(); CountingHandler handler = new(HttpStatusCode.OK, "image/png", ValidPng()); using RemoteArtworkDownloader downloader = new(new HttpClient(handler)); ArtworkDownloadResult result = await downloader.DownloadAsync(Asset(), Request(c)); Assert.True(result.Succeeded); Assert.True(File.Exists(result.LocalPath)); Assert.Equal(1, handler.Requests); }

    [Fact]
    public async Task UserArtworkWinsAndLockedAssignmentIsVisible()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Game");
        using RemoteArtworkDownloader downloader = new(); ArtworkDownloadResult imported = await downloader.ImportLocalAsync(context.WritePng("user.png"), ArtworkKind.CoverPortrait, Request(context));
        SqliteArtworkAssignmentStore store = new(context.Factory, context.Paths); ArtworkAssignment assignment = Assignment("one", imported, user: true, locked: true);
        await store.ApplyAsync(assignment); ArtworkAssignment current = Assert.IsType<ArtworkAssignment>(await store.GetCurrentAsync("item", ArtworkKind.CoverPortrait));
        Assert.True(current.UserSelected); Assert.True(current.Locked);
    }

    [Fact]
    public async Task UndoRestoresPreviousArtworkAndHistoryIsBounded()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Game"); SqliteArtworkAssignmentStore store = new(context.Factory, context.Paths); using RemoteArtworkDownloader downloader = new();
        for (int index = 0; index < 5; index++) { string source = context.WritePng($"{index}.png", index + 1); ArtworkDownloadResult imported = await downloader.ImportLocalAsync(source, ArtworkKind.CoverPortrait, Request(context)); await store.ApplyAsync(Assignment(index.ToString(CultureInfo.InvariantCulture), imported)); await Task.Delay(2); }
        Assert.True((await store.GetHistoryAsync("item", ArtworkKind.CoverPortrait)).Count <= 3); ArtworkAssignment? restored = await store.UndoAsync("item", ArtworkKind.CoverPortrait); Assert.NotNull(restored); Assert.True(restored.IsCurrent);
    }

    [Fact]
    public async Task ArtworkJobPersistsPauseAndResumeAcrossServiceInstances()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("one", "One"); await context.InsertLibraryItemAsync("two", "Two");
        SqliteArtworkJobService first = new(context.Factory); string id = await first.CreateAsync(new("PS2 missing artwork", ["one", "two"], [ArtworkKind.CoverPortrait])); await first.StartAsync(id); await first.PauseAsync(id);
        SqliteArtworkJobService restarted = new(context.Factory); Assert.Equal(ArtworkJobStatus.Paused, (await restarted.GetAsync(id)).Status); await restarted.ResumeAsync(id); Assert.Equal(ArtworkJobStatus.Identifying, (await restarted.GetAsync(id)).Status);
    }

    [Fact]
    public async Task CompletedAllNoMatchJobHasTruthfulOutcomeAndItemReason()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Missing Art Game"); SqliteArtworkJobService jobs = new(context.Factory); string id = await jobs.CreateAsync(new("No matches", ["item"], [ArtworkKind.CoverPortrait])); await jobs.StartAsync(id);
        await using (SqliteConnection connection = await context.Factory.OpenAsync()) { await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE ArtworkJobItems SET Status=$itemStatus,FailureCode='no_candidates',FailureMessage='No provider returned a candidate.',UpdatedAtUtc=$now WHERE ArtworkJobId=$id; UPDATE ArtworkJobs SET Status=$jobStatus,CompletedItems=1,CompletedAtUtc=$now,LastUpdatedAtUtc=$now WHERE Id=$id;"; command.Parameters.AddWithValue("$itemStatus", (int)ArtworkJobItemStatus.NoMatch); command.Parameters.AddWithValue("$jobStatus", (int)ArtworkJobStatus.Completed); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); command.Parameters.AddWithValue("$id", id); await command.ExecuteNonQueryAsync(); }
        ArtworkJobSnapshot snapshot = await jobs.GetAsync(id); ArtworkJobItemSnapshot item = Assert.Single(await jobs.ListItemsAsync(id));
        Assert.Equal(ArtworkJobOutcome.NoArtworkFound, snapshot.Outcome); Assert.Equal(1, snapshot.NoMatchItems); Assert.Equal("no_candidates", item.FailureCode);
        List<ArtworkJobSnapshot> watched = []; await foreach (ArtworkJobSnapshot update in jobs.WatchAsync(id)) watched.Add(update); Assert.Single(watched); Assert.Equal(ArtworkJobOutcome.NoArtworkFound, watched[0].Outcome);
    }

    [Fact]
    public async Task ArtworkPresenceRejectsTinyLegacyIconButProtectsValidCover()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Steam Game"); string path = context.WritePng("legacy.png"); SqliteArtworkAssignmentStore assignments = new(context.Factory, context.Paths); ArtworkPresenceService presence = new(context.Factory, assignments);
        await using (SqliteConnection connection = await context.Factory.OpenAsync()) { await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO Assets(Id,LibraryItemId,AssetType,LocalPath,Width,Height,FileSize,AddedAtUtc) VALUES('legacy','item',$type,$path,32,32,1,$now); UPDATE LibraryItems SET CoverAssetId='legacy' WHERE Id='item';"; command.Parameters.AddWithValue("$type", (int)AssetType.Cover); command.Parameters.AddWithValue("$path", path); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(); }
        Assert.False(await presence.IsPresentAsync("item", ArtworkKind.CoverPortrait));
        await using (SqliteConnection connection = await context.Factory.OpenAsync()) { await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "UPDATE Assets SET Width=600,Height=900 WHERE Id='legacy';"; await command.ExecuteNonQueryAsync(); }
        Assert.True(await presence.IsPresentAsync("item", ArtworkKind.CoverPortrait)); Assert.False(await presence.IsPresentAsync("item", ArtworkKind.HeroBackground));
    }

    [Fact]
    public async Task PreflightBlocksWhenPrivacyDisablesRemoteProvidersAndLocalSourceIsEmpty()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Game"); string emptyRoot = Path.Combine(context.Root, "EmptyArtwork"); Directory.CreateDirectory(emptyRoot); using RemoteArtworkDownloader downloader = new(); LocalArtworkProvider local = new([emptyRoot], downloader); ArtworkJobPreflightService preflight = new(context.Factory, [local], new FixedSettings());
        ArtworkJobPreflightResult result = await preflight.CheckAsync(new("Blocked", ["item"], [ArtworkKind.CoverPortrait]));
        Assert.False(result.CanStart); Assert.Contains(result.BlockingReasons, reason => reason.Contains("Online artwork is disabled", StringComparison.Ordinal)); Assert.False(Assert.Single(result.Providers).Available);
    }

    [Fact]
    public async Task BulkProcessorAppliesExactLocalArtworkAndCompletesDurably()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Example Game");
        string localRoot = Path.Combine(context.Root, "LocalArtwork"); Directory.CreateDirectory(localRoot); File.WriteAllBytes(Path.Combine(localRoot, "Example Game-cover.png"), ValidPng());
        using RemoteArtworkDownloader downloader = new(); LocalArtworkProvider provider = new([localRoot], downloader); SqliteArtworkAssignmentStore assignments = new(context.Factory, context.Paths);
        SqliteArtworkJobService jobs = new(context.Factory); string jobId = await jobs.CreateAsync(new("Local artwork", ["item"], [ArtworkKind.CoverPortrait])); await jobs.StartAsync(jobId);
        using ArtworkJobProcessor processor = new(context.Factory, new FixedIdentityResolver(new("item", ContentType.Game, "example game", "Example Game")), new DeterministicGameMatchScorer(), [provider], assignments, new SqliteMetadataProvenanceStore(context.Factory), new FixedSettings(), context.Paths, new NullLogger());
        await processor.RunAsync(jobId);
        Assert.Equal(ArtworkJobStatus.Completed, (await jobs.GetAsync(jobId)).Status);
        ArtworkAssignment current = Assert.IsType<ArtworkAssignment>(await assignments.GetCurrentAsync("item", ArtworkKind.CoverPortrait)); Assert.Equal("local", current.SourceProviderId); Assert.False(current.UserSelected);
    }

    [Fact]
    public async Task UserMetadataModificationBlocksAutomaticOverwriteAndConfirmedIdentitySurvives()
    {
        await using Context context = await Context.CreateAsync(); await context.InsertLibraryItemAsync("item", "Example Game"); SqliteMetadataProvenanceStore store = new(context.Factory); DateTimeOffset now = DateTimeOffset.UtcNow;
        await store.RecordFieldOriginAsync(new("item", "Description", MetadataSourceType.TheGamesDb, "thegamesdb", "42", now, 900, false, false)); Assert.True(await store.CanAutomaticallyOverwriteAsync("item", "Description"));
        await store.MarkUserModifiedAsync("item", "Description"); Assert.False(await store.CanAutomaticallyOverwriteAsync("item", "Description"));
        await store.RecordFieldOriginAsync(new("item", "Description", MetadataSourceType.SteamGridDb, "steamgriddb", "new", now.AddMinutes(1), 1000, false, false));
        await store.SaveExternalIdentityAsync(new("identity", "item", "thegamesdb", "42", ExternalIdentityType.ProviderGameId, IdentityConfidence.ExactExternalId, true, now, now));
        ExternalGameIdentity identity = Assert.Single(await store.GetExternalIdentitiesAsync("item")); Assert.True(identity.ConfirmedByUser); Assert.Equal("42", identity.ExternalGameId);
        await using SqliteConnection connection = await context.Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "SELECT SourceProviderId,UserModifiedAfterApply FROM MetadataFieldProvenance WHERE LibraryItemId='item' AND FieldName='Description';"; await using SqliteDataReader reader = await command.ExecuteReaderAsync(); await reader.ReadAsync(); Assert.Equal("thegamesdb", reader.GetString(0)); Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    public async Task EpicManifestDiscoveryIsReadOnlyAndIdempotent()
    {
        await using Context context = await Context.CreateAsync(); string root = Path.Combine(context.Root, "EpicManifests"); Directory.CreateDirectory(root); string install = Path.Combine(context.Root, "EpicGame"); Directory.CreateDirectory(install); string manifest = Path.Combine(root, "game.item");
        await File.WriteAllTextAsync(manifest, $$"""{"AppName":"ExampleApp","CatalogItemId":"catalog-1","DisplayName":"Example Game","InstallLocation":"{{install.Replace("\\", "\\\\", StringComparison.Ordinal)}}","LaunchExecutable":"Game.exe"}"""); byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(manifest));
        EpicLibraryProvider provider = new(); ProviderScanRequest request = new(ConfiguredInstallationPath: root); ProviderScanResult first = await provider.ScanAsync(request); ProviderScanResult second = await provider.ScanAsync(request);
        Assert.Single(first.DiscoveredItems); Assert.Equal(first.DiscoveredItems[0].ProviderItemId, second.DiscoveredItems[0].ProviderItemId); Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(manifest)));
    }

    [Fact] public async Task LocalProviderDoesNotScanSubdirectories() { await using Context c = await Context.CreateAsync(); string root = Path.Combine(c.Root, "art"); string nested = Path.Combine(root, "nested"); Directory.CreateDirectory(nested); File.WriteAllBytes(Path.Combine(nested, "God of War II-cover.png"), ValidPng()); using RemoteArtworkDownloader downloader = new(); LocalArtworkProvider provider = new([root], downloader); GameIdentity identity = new("item", ContentType.Game, "god of war ii", "God of War II", "playstation-2"); Assert.Empty(await provider.SearchAsync(new(identity))); }
    [Fact] public async Task LocalProviderFindsExactPlatformSidecarWithoutModifyingIt() { await using Context c = await Context.CreateAsync(); string root = Path.Combine(c.Root, "art"); string platform = Path.Combine(root, "playstation-2"); Directory.CreateDirectory(platform); string source = Path.Combine(platform, "God of War II-cover.png"); File.WriteAllBytes(source, ValidPng()); DateTime before = File.GetLastWriteTimeUtc(source); using RemoteArtworkDownloader downloader = new(); LocalArtworkProvider provider = new([root], downloader); GameIdentity identity = new("item", ContentType.Game, "god of war ii", "God of War II", "playstation-2"); ArtworkMatchCandidate match = Assert.Single(await provider.SearchAsync(new(identity))); Assert.Equal(MatchDecision.AutoApply, match.Decision); Assert.Equal(before, File.GetLastWriteTimeUtc(source)); }

    private static ArtworkDownloadRequest Request(Context c) => new("item", Path.Combine(c.Paths.Data, "Artwork"), MaximumBytes: 4096, MaximumDimension: 16384);
    private static RemoteArtworkAsset Asset(Uri? uri = null) => new("test", "game", "asset", ArtworkKind.CoverPortrait, uri ?? new("https://example.test/image.png"), null, 1, 1, null, "image/png");
    private static ArtworkAssignment Assignment(string id, ArtworkDownloadResult result, bool user = false, bool locked = false) => new(id, "item", ArtworkKind.CoverPortrait, result.LocalPath!, result.ContentHash!, user ? "user" : "test", "game", "asset", null, result.Width, result.Height, result.Width, result.Height, result.FileSize, 1000, user, locked, null, null, DateTimeOffset.UtcNow);
    private static byte[] ValidPng() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    private static void WriteBigEndian(byte[] bytes, int offset, int value) { bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16); bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value; }

    private sealed class CountingHandler(HttpStatusCode status, string mime, byte[] body, long? declaredLength = null) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Requests++; ByteArrayContent content = new(body); content.Headers.ContentType = new(mime); if (declaredLength is not null) content.Headers.ContentLength = declaredLength; return Task.FromResult(new HttpResponseMessage(status) { Content = content, RequestMessage = request }); }
    }

    private sealed class Context : IAsyncDisposable
    {
        private Context(string root) { Root = root; Paths = new(root); Paths.EnsureCreated(); Factory = new(Paths); }
        public string Root { get; } public NexaStationPaths Paths { get; } public SqliteConnectionFactory Factory { get; }
        public static async Task<Context> CreateAsync() { Context c = new(Path.Combine(Path.GetTempPath(), "NexaStation.Phase11." + Guid.NewGuid().ToString("N"))); await new DatabaseInitializer(c.Factory, new NullLogger()).InitializeAsync(); return c; }
        public string WritePng(string name, int variant = 0) { string path = Path.Combine(Root, name); byte[] bytes = ValidPng(); if (variant > 0) bytes[^13] ^= (byte)variant; File.WriteAllBytes(path, bytes); return path; }
        public async Task InsertLibraryItemAsync(string id, string title) { await using SqliteConnection connection = await Factory.OpenAsync(); await using SqliteCommand command = connection.CreateCommand(); command.CommandText = "INSERT INTO LibraryItems(Id,Title,SortTitle,ContentType,Favorite,Hidden,AddedAtUtc,UpdatedAtUtc,TotalPlaytimeSeconds) VALUES($id,$title,$title,0,0,0,$now,$now,0);"; command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$title", title); command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(); }
        public ValueTask DisposeAsync() { try { Directory.Delete(Root, true); } catch (IOException) { } return ValueTask.CompletedTask; }
    }
    private sealed class NullLogger : IAppLogger { public void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null) { } public void LogError(string eventName, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) { } }
    private sealed class FixedIdentityResolver(GameIdentity identity) : IGameIdentityResolver { public Task<GameIdentity> ResolveAsync(string libraryItemId, CancellationToken cancellationToken = default) => Task.FromResult(identity); }
    private sealed class FixedSettings : ISettingsService { public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default) => Task.FromResult(defaultValue); public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) => Task.CompletedTask; }
}
