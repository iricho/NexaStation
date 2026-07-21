using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;
using NexaStation.Recovery;

namespace NexaStation.Feedback.ReproductionBundles;

public sealed record ReproductionBundleRequest
{
    public required string Category { get; init; }
    public required string ReproductionSteps { get; init; }
    public string? ExpectedBehavior { get; init; }
    public string? ActualBehavior { get; init; }
    public CompatibilityReport? CompatibilityReport { get; init; }
    public CompatibilityProfile? ActiveProfile { get; init; }
    public string? SelectedCrashReportPath { get; init; }
    public string? StartupHealthSummary { get; init; }
    public string? ProviderDiagnosticSummary { get; init; }
    public string? DatabaseSchemaVersion { get; init; }
    public string? ControllerFamily { get; init; }
    public string? DisplaySummary { get; init; }
    public bool IncludeRecentLogs { get; init; } = true;
    public bool ExactPathConsent { get; init; }
    public string? ExactExecutablePath { get; init; }
    public string? ExactRomFilename { get; init; }
    public string? ExactGalleryFilename { get; init; }
    public string? ExactProviderLibraryPath { get; init; }
}

public sealed record ReproductionBundleResult(bool Succeeded, string? BundlePath, string Message, IReadOnlyList<string> IncludedEntries);

public interface IReproductionBundleService
{
    Task<ReproductionBundleResult> CreateAsync(ReproductionBundleRequest request, CancellationToken cancellationToken = default);
}

public sealed class ReproductionBundleService(INexaStationPaths paths, ISensitiveDataRedactor redactor) : IReproductionBundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const long MaximumSourceBytes = 2 * 1024 * 1024;

    public async Task<ReproductionBundleResult> CreateAsync(ReproductionBundleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.ReproductionSteps)) return new(false, null, "A category and reproduction steps are required.", []);
        paths.EnsureCreated();
        string output = Path.Combine(paths.Diagnostics, $"nexastation-reproduction-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");
        List<string> entries = [];
        await using FileStream stream = new(output, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false);
        await AddTextAsync(archive, "reproduction.json", JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            createdAtUtc = DateTimeOffset.UtcNow,
            category = redactor.Redact(request.Category),
            reproductionSteps = redactor.Redact(request.ReproductionSteps),
            expectedBehavior = redactor.Redact(request.ExpectedBehavior ?? string.Empty),
            actualBehavior = redactor.Redact(request.ActualBehavior ?? string.Empty),
            request.DatabaseSchemaVersion,
            controllerFamily = redactor.Redact(request.ControllerFamily ?? string.Empty),
            displaySummary = redactor.Redact(request.DisplaySummary ?? string.Empty),
            exactPathsIncludedWithConsent = request.ExactPathConsent,
        }, JsonOptions), entries, cancellationToken).ConfigureAwait(false);

        if (request.CompatibilityReport is not null) await AddTextAsync(archive, "compatibility/report.json", redactor.Redact(JsonSerializer.Serialize(request.CompatibilityReport, JsonOptions)), entries, cancellationToken).ConfigureAwait(false);
        if (request.ActiveProfile is not null) await AddTextAsync(archive, "compatibility/active-profile.json", redactor.Redact(JsonSerializer.Serialize(request.ActiveProfile, JsonOptions)), entries, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.StartupHealthSummary)) await AddTextAsync(archive, "startup/health.txt", redactor.Redact(request.StartupHealthSummary), entries, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(request.ProviderDiagnosticSummary)) await AddTextAsync(archive, "providers/summary.txt", redactor.Redact(request.ProviderDiagnosticSummary), entries, cancellationToken).ConfigureAwait(false);
        await AddSelectedFileAsync(archive, request.SelectedCrashReportPath, "crash/selected.json", entries, cancellationToken).ConfigureAwait(false);
        if (request.IncludeRecentLogs && Directory.Exists(paths.Logs))
        {
            foreach (FileInfo file in new DirectoryInfo(paths.Logs).EnumerateFiles("*.jsonl").Where(file => file.Length <= MaximumSourceBytes).OrderByDescending(file => file.LastWriteTimeUtc).Take(3))
                await AddTextAsync(archive, $"logs/{file.Name}", redactor.Redact(await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false)), entries, cancellationToken).ConfigureAwait(false);
        }
        if (request.ExactPathConsent)
        {
            await AddTextAsync(archive, "consented/exact-paths.json", JsonSerializer.Serialize(new
            {
                request.ExactExecutablePath,
                request.ExactRomFilename,
                request.ExactGalleryFilename,
                request.ExactProviderLibraryPath,
            }, JsonOptions), entries, cancellationToken).ConfigureAwait(false);
        }
        return new(true, output, "The reproduction bundle was saved locally. It was not uploaded.", entries);
    }

    private async Task AddSelectedFileAsync(ZipArchive archive, string? source, string target, List<string> entries, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return;
        FileInfo info = new(source);
        if (info.Length > MaximumSourceBytes || !Path.GetFullPath(source).StartsWith(Path.GetFullPath(paths.CrashReports), StringComparison.OrdinalIgnoreCase)) return;
        await AddTextAsync(archive, target, redactor.Redact(await File.ReadAllTextAsync(source, cancellationToken).ConfigureAwait(false)), entries, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddTextAsync(ZipArchive archive, string name, string content, List<string> entries, CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream output = entry.Open();
        await using StreamWriter writer = new(output, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        entries.Add(name);
    }
}
