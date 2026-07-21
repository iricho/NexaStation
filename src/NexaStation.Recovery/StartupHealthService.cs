using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexaStation.Recovery;

public enum StartupStage
{
    Starting,
    PathsReady,
    DatabaseReady,
    ServicesReady,
    OnboardingReady,
    DashboardReady,
    CleanShutdown,
    Failed,
}

public sealed record StartupFailure(DateTimeOffset OccurredAtUtc, StartupStage Stage, string Reason);

public sealed record StartupHealthState(
    int SchemaVersion,
    StartupStage Stage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<StartupFailure> RecentFailures,
    bool SafeModeRequested)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record StartupDecision(bool ShouldEnterSafeMode, int FailureCount, string? Reason, StartupHealthState State);

public interface IStartupHealthService
{
    Task<StartupDecision> BeginStartupAsync(CancellationToken cancellationToken = default);
    Task MarkStageAsync(StartupStage stage, CancellationToken cancellationToken = default);
    Task MarkFailureAsync(StartupStage stage, string reason, CancellationToken cancellationToken = default);
    Task MarkCleanShutdownAsync(CancellationToken cancellationToken = default);
    Task RequestSafeModeAsync(bool requested, CancellationToken cancellationToken = default);
}

public sealed class StartupHealthService : IStartupHealthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly TimeSpan CrashLoopWindow = TimeSpan.FromMinutes(10);
    private const int CrashLoopThreshold = 3;
    private readonly string _statePath;
    private readonly Func<DateTimeOffset> _clock;
    private StartupHealthState? _current;

    public StartupHealthService(INexaStationPaths paths, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _statePath = paths.StartupHealthState;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<StartupDecision> BeginStartupAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock();
        StartupHealthState? previous = await LoadAsync(cancellationToken).ConfigureAwait(false);
        List<StartupFailure> failures = previous?.RecentFailures
            .Where(failure => now - failure.OccurredAtUtc <= CrashLoopWindow)
            .ToList() ?? [];

        if (previous is not null && previous.Stage is not (StartupStage.CleanShutdown or StartupStage.OnboardingReady or StartupStage.DashboardReady or StartupStage.Failed))
        {
            failures.Add(new(previous.UpdatedAtUtc, previous.Stage, "The previous startup did not reach a healthy state."));
        }

        bool safeMode = previous?.SafeModeRequested == true || failures.Count >= CrashLoopThreshold;
        _current = new(StartupHealthState.CurrentSchemaVersion, StartupStage.Starting, now, now, failures, safeMode);
        await SaveAsync(_current, cancellationToken).ConfigureAwait(false);
        string? reason = safeMode ? $"Safe mode was requested after {failures.Count} recent startup failures." : null;
        return new(safeMode, failures.Count, reason, _current);
    }

    public async Task MarkStageAsync(StartupStage stage, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        bool healthyInteractiveStage = stage is StartupStage.OnboardingReady or StartupStage.DashboardReady;
        IReadOnlyList<StartupFailure> failures = healthyInteractiveStage ? [] : _current!.RecentFailures;
        _current = _current! with { Stage = stage, UpdatedAtUtc = _clock(), RecentFailures = failures, SafeModeRequested = healthyInteractiveStage ? false : _current.SafeModeRequested };
        await SaveAsync(_current, cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailureAsync(StartupStage stage, string reason, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        DateTimeOffset now = _clock();
        List<StartupFailure> failures = _current!.RecentFailures
            .Where(failure => now - failure.OccurredAtUtc <= CrashLoopWindow)
            .ToList();
        failures.Add(new(now, stage, string.IsNullOrWhiteSpace(reason) ? "Unknown startup failure." : reason));
        _current = _current with { Stage = StartupStage.Failed, UpdatedAtUtc = now, RecentFailures = failures, SafeModeRequested = failures.Count >= CrashLoopThreshold };
        await SaveAsync(_current, cancellationToken).ConfigureAwait(false);
    }

    public Task MarkCleanShutdownAsync(CancellationToken cancellationToken = default) => MarkStageAsync(StartupStage.CleanShutdown, cancellationToken);

    public async Task RequestSafeModeAsync(bool requested, CancellationToken cancellationToken = default)
    {
        EnsureStarted();
        _current = _current! with { SafeModeRequested = requested, UpdatedAtUtc = _clock() };
        await SaveAsync(_current, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureStarted()
    {
        if (_current is null)
        {
            throw new InvalidOperationException("BeginStartupAsync must be called before startup health is updated.");
        }
    }

    private async Task<StartupHealthState?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statePath))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(_statePath);
            StartupHealthState? state = await JsonSerializer.DeserializeAsync<StartupHealthState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return state?.SchemaVersion == StartupHealthState.CurrentSchemaVersion ? state : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task SaveAsync(StartupHealthState state, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(_statePath) ?? throw new InvalidOperationException("The startup state path has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = _statePath + ".tmp";
        await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _statePath, true);
    }
}
