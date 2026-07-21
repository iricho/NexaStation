using System.IO.Compression;
using System.Text.Json;
using NexaStation.Feedback.IssuePreparation;
using NexaStation.Feedback.ReproductionBundles;
using NexaStation.Feedback.Triage;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Feedback.Tests;

public sealed class FeedbackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nexastation-feedback-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BundleIsLocalAndExcludesDatabaseAndUnconsentedPaths()
    {
        NexaStationPaths paths = new(_root); paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.Database, "private database");
        ReproductionBundleService service = new(paths, new SensitiveDataRedactor(paths));
        ReproductionBundleResult result = await service.CreateAsync(new()
        {
            Category = "Steam", ReproductionSteps = "Launch C:\\Users\\Private\\game.exe", ExactExecutablePath = "C:\\Users\\Private\\game.exe", ExactPathConsent = false,
        });
        Assert.True(result.Succeeded);
        using ZipArchive archive = ZipFile.OpenRead(result.BundlePath!);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("database", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("exact-paths", StringComparison.OrdinalIgnoreCase));
        string reproduction = Read(archive, "reproduction.json");
        Assert.DoesNotContain("C:\\Users\\Private", reproduction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExactPathRequiresExplicitConsent()
    {
        NexaStationPaths paths = new(_root); paths.EnsureCreated();
        ReproductionBundleResult result = await new ReproductionBundleService(paths, new SensitiveDataRedactor(paths)).CreateAsync(new()
        {
            Category = "Windows Apps", ReproductionSteps = "Reproduce", ExactPathConsent = true, ExactExecutablePath = "D:\\Approved\\app.exe",
        });
        using ZipArchive archive = ZipFile.OpenRead(result.BundlePath!);
        Assert.Contains(archive.Entries, entry => entry.FullName == "consented/exact-paths.json");
        using JsonDocument document = JsonDocument.Parse(Read(archive, "consented/exact-paths.json"));
        Assert.Equal("D:\\Approved\\app.exe", document.RootElement.GetProperty("ExactExecutablePath").GetString());
    }

    [Fact]
    public void IssuePreparationRedactsAndDoesNotEmbedUploadOrPrivateQuery()
    {
        NexaStationPaths paths = new(_root); paths.EnsureCreated();
        GitHubIssuePreparationResult result = new GitHubIssuePreparationService(new SensitiveDataRedactor(paths)).Prepare(new("0.9.0", "Alpha", "Windows", "Startup", "Works", $"Failed at {paths.Root}", "1. Start", "Works", "abc", true));
        Assert.DoesNotContain(paths.Root, result.Markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.IssuePage.Query);
        Assert.Contains("attached manually", result.Markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FixedTransitionRequiresAllVerificationEvidence()
    {
        AlphaIssue issue = new("a", "Bug", AlphaIssueSeverity.High, AlphaIssueCategory.Steam, AlphaIssueState.VerificationNeeded, "summary", null, null, null, null, DateTimeOffset.UtcNow);
        Assert.False(AlphaIssueWorkflow.CanTransition(issue, AlphaIssueState.Fixed).Allowed);
        Assert.True(AlphaIssueWorkflow.CanTransition(issue, AlphaIssueState.Fixed, true, true, true, true, true).Allowed);
    }

    private static string Read(ZipArchive archive, string name)
    {
        using StreamReader reader = new(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
