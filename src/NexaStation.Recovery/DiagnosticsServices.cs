using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NexaStation.Recovery;

public interface ISensitiveDataRedactor
{
    string Redact(string value);
}

public sealed partial class SensitiveDataRedactor : ISensitiveDataRedactor
{
    private readonly string[] _sensitiveRoots;

    public SensitiveDataRedactor(INexaStationPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _sensitiveRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            paths.Root,
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(path => path.Length).ToArray();
    }

    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string redacted = value;
        foreach (string root in _sensitiveRoots)
        {
            redacted = redacted.Replace(root, root == _sensitiveRoots[0] ? "%USERPROFILE%" : "%NEXASTATION_DATA%", StringComparison.OrdinalIgnoreCase);
        }

        redacted = SecretPattern().Replace(redacted, "$1=[REDACTED]");
        redacted = BearerPattern().Replace(redacted, "$1[REDACTED]");
        return EmailPattern().Replace(redacted, "[REDACTED-EMAIL]");
    }

    [GeneratedRegex("(?i)(api[_-]?key|token|secret|password|steam[_-]?key)\\s*[:=]\\s*[^\\s,;\\\"}]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex("(?i)(Bearer\\s+)[A-Za-z0-9._~+/-]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();

    [GeneratedRegex("(?i)[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}

public sealed record CrashReport(
    int FormatVersion,
    DateTimeOffset OccurredAtUtc,
    string AppVersion,
    string Event,
    string Message,
    string ExceptionType,
    string Exception,
    StartupStage StartupStage,
    bool SafeMode);

public interface ICrashReportService
{
    Task<string> WriteAsync(string eventName, Exception exception, string appVersion, StartupStage stage, bool safeMode, CancellationToken cancellationToken = default);
}

public sealed class CrashReportService(INexaStationPaths paths, ISensitiveDataRedactor redactor) : ICrashReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public async Task<string> WriteAsync(string eventName, Exception exception, string appVersion, StartupStage stage, bool safeMode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        paths.EnsureCreated();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CrashReport report = new(
            1,
            now,
            appVersion,
            eventName,
            redactor.Redact(exception.Message),
            exception.GetType().FullName ?? exception.GetType().Name,
            redactor.Redact(exception.ToString()),
            stage,
            safeMode);
        string path = Path.Combine(paths.CrashReports, $"crash-{now:yyyyMMdd-HHmmss-fff}.json");
        await using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, report, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        Prune(paths.CrashReports, "crash-*.json", 20);
        return path;
    }

    private static void Prune(string directory, string pattern, int keep)
    {
        foreach (FileInfo file in new DirectoryInfo(directory).EnumerateFiles(pattern).OrderByDescending(file => file.CreationTimeUtc).Skip(keep))
        {
            try { file.Delete(); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }
}

public sealed record DiagnosticsExportOptions(bool IncludeLogs = true, bool IncludeCrashReports = true, int MaximumFilesPerCategory = 5);

public interface IDiagnosticsExportService
{
    Task<string> ExportAsync(string appVersion, string releaseChannel, DiagnosticsExportOptions options, CancellationToken cancellationToken = default);
}

public sealed class DiagnosticsExportService(INexaStationPaths paths, ISensitiveDataRedactor redactor) : IDiagnosticsExportService
{
    private const long MaximumSourceFileBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions SummaryJsonOptions = new() { WriteIndented = true };

    public async Task<string> ExportAsync(string appVersion, string releaseChannel, DiagnosticsExportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        paths.EnsureCreated();
        string target = Path.Combine(paths.Diagnostics, $"nexastation-diagnostics-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");
        await using FileStream archiveStream = new(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: false);
        await AddTextAsync(archive, "system/summary.json", JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            product = "NexaStation",
            version = appVersion,
            channel = releaseChannel,
            exportedAtUtc = DateTimeOffset.UtcNow,
            osVersion = Environment.OSVersion.VersionString,
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        }, SummaryJsonOptions), cancellationToken).ConfigureAwait(false);

        if (options.IncludeLogs)
        {
            await AddRedactedFilesAsync(archive, paths.Logs, "*.jsonl", "logs", options.MaximumFilesPerCategory, cancellationToken).ConfigureAwait(false);
        }

        if (options.IncludeCrashReports)
        {
            await AddRedactedFilesAsync(archive, paths.CrashReports, "*.json", "crashes", options.MaximumFilesPerCategory, cancellationToken).ConfigureAwait(false);
        }

        return target;
    }

    private async Task AddRedactedFilesAsync(ZipArchive archive, string sourceDirectory, string pattern, string targetDirectory, int maximumFiles, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        IEnumerable<FileInfo> files = new DirectoryInfo(sourceDirectory).EnumerateFiles(pattern)
            .Where(file => file.Length <= MaximumSourceFileBytes)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(Math.Clamp(maximumFiles, 0, 20));
        foreach (FileInfo file in files)
        {
            string content = await File.ReadAllTextAsync(file.FullName, cancellationToken).ConfigureAwait(false);
            await AddTextAsync(archive, $"{targetDirectory}/{file.Name}", redactor.Redact(content), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AddTextAsync(ZipArchive archive, string path, string content, CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await using StreamWriter writer = new(stream, new UTF8Encoding(false), leaveOpen: false);
        await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
