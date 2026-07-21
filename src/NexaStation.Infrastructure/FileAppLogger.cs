using System.Diagnostics;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Recovery;

namespace NexaStation.Infrastructure;

public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private const long MaximumLogBytes = 10 * 1024 * 1024;
    private const int MaximumRetainedFiles = 20;
    private static readonly TimeSpan MaximumAge = TimeSpan.FromDays(14);
    private readonly object _gate = new();
    private readonly string _logPath;
    private readonly ISensitiveDataRedactor _redactor;
    private bool _disposed;

    public FileAppLogger()
        : this(new NexaStationPaths())
    {
    }

    public FileAppLogger(INexaStationPaths paths)
        : this(paths, new SensitiveDataRedactor(paths))
    {
    }

    public FileAppLogger(INexaStationPaths paths, ISensitiveDataRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        string folder = paths.Logs;
        string dailyPath = Path.Combine(folder, $"nexastation-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        _logPath = File.Exists(dailyPath) && new FileInfo(dailyPath).Length >= MaximumLogBytes
            ? Path.Combine(folder, $"nexastation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jsonl")
            : dailyPath;

        try
        {
            Directory.CreateDirectory(folder);
            Prune(folder);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"NexaStation could not create its log folder: {exception}");
        }
    }

    public void Information(string eventName, string message, IReadOnlyDictionary<string, object?>? properties = null) =>
        Write("Information", eventName, message, null, properties);

    public void LogError(
        string eventName,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null) =>
        Write("Error", eventName, message, exception, properties);

    public void Dispose()
    {
        _disposed = true;
    }

    private void Write(
        string level,
        string eventName,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?>? properties)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var entry = new
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Level = level,
                Event = eventName,
                Message = _redactor.Redact(message),
                Exception = exception is null ? null : _redactor.Redact(exception.ToString()),
                Properties = RedactProperties(properties),
            };

            string line = JsonSerializer.Serialize(entry);
            lock (_gate)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch (Exception loggingException)
        {
            Debug.WriteLine($"NexaStation logging failed: {loggingException}");
        }
    }

    private Dictionary<string, object?>? RedactProperties(IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null)
        {
            return null;
        }

        return properties.ToDictionary(
            property => property.Key,
            property => property.Value is string value ? _redactor.Redact(value) : property.Value,
            StringComparer.Ordinal);
    }

    private static void Prune(string folder)
    {
        DateTime cutoff = DateTime.UtcNow - MaximumAge;
        IEnumerable<FileInfo> expired = new DirectoryInfo(folder).EnumerateFiles("nexastation-*.jsonl")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Where((file, index) => index >= MaximumRetainedFiles || file.LastWriteTimeUtc < cutoff);
        foreach (FileInfo file in expired)
        {
            try
            {
                file.Delete();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"NexaStation could not prune log '{file.Name}': {exception.Message}");
            }
        }
    }
}
