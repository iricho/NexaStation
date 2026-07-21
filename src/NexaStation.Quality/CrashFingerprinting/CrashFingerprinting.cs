using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Recovery;

namespace NexaStation.Quality.CrashFingerprinting;

public sealed record CrashFingerprintRecord(
    string Fingerprint,
    string ExceptionType,
    string NormalizedTopFrames,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    int OccurrenceCount,
    string LastApplicationVersion,
    StartupStage? StartupStage,
    string? ResolvedVersion);

public interface ICrashFingerprintService
{
    string CreateFingerprint(Exception exception, StartupStage? startupStage);
    string NormalizeFrames(Exception exception);
}

public interface ICrashFingerprintRepository
{
    Task<CrashFingerprintRecord?> GetAsync(string fingerprint, CancellationToken cancellationToken = default);
    Task UpsertAsync(CrashFingerprintRecord record, CancellationToken cancellationToken = default);
    Task MarkResolvedAsync(string fingerprint, string resolvedVersion, CancellationToken cancellationToken = default);
}

public interface ICrashFingerprintTracker
{
    Task<CrashFingerprintRecord> RecordAsync(Exception exception, StartupStage? startupStage, string applicationVersion, CancellationToken cancellationToken = default);
}

public sealed class CrashFingerprintService : ICrashFingerprintService
{
    public string CreateFingerprint(Exception exception, StartupStage? startupStage)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string normalized = string.Join('|', new[]
        {
            exception.GetType().FullName,
            exception.InnerException?.GetType().FullName,
            NormalizeFrames(exception),
            startupStage?.ToString(),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    public string NormalizeFrames(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string[] frames = new StackTrace(exception, false).GetFrames()
            .Select(frame => frame.GetMethod())
            .Where(method => method?.DeclaringType?.Namespace?.StartsWith("NexaStation", StringComparison.Ordinal) == true)
            .Select(method => $"{method!.DeclaringType!.FullName}.{method.Name}")
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();
        if (frames.Length > 0) return string.Join('\n', frames);
        return exception.TargetSite is null ? exception.GetType().FullName ?? exception.GetType().Name : $"{exception.TargetSite.DeclaringType?.FullName}.{exception.TargetSite.Name}";
    }
}

public sealed class CrashFingerprintTracker(ICrashFingerprintService fingerprints, ICrashFingerprintRepository repository) : ICrashFingerprintTracker
{
    public async Task<CrashFingerprintRecord> RecordAsync(Exception exception, StartupStage? startupStage, string applicationVersion, CancellationToken cancellationToken = default)
    {
        string fingerprint = fingerprints.CreateFingerprint(exception, startupStage);
        CrashFingerprintRecord? current = await repository.GetAsync(fingerprint, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CrashFingerprintRecord updated = current is null
            ? new(fingerprint, exception.GetType().FullName ?? exception.GetType().Name, fingerprints.NormalizeFrames(exception), now, now, 1, applicationVersion, startupStage, null)
            : current with { LastSeenAtUtc = now, OccurrenceCount = checked(current.OccurrenceCount + 1), LastApplicationVersion = applicationVersion, StartupStage = startupStage, ResolvedVersion = null };
        await repository.UpsertAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }
}
