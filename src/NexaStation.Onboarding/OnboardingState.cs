using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexaStation.Onboarding;

public enum OnboardingStatus
{
    NeverStarted,
    InProgress,
    Completed,
    Skipped,
}

public enum OnboardingStep
{
    Welcome,
    Layout,
    Performance,
    Input,
    Providers,
    Applications,
    Gallery,
    Startup,
    Privacy,
    Review,
}

public sealed record OnboardingPreferences(
    string Layout = "Modern",
    string PerformanceProfile = "Balanced",
    bool ControllerEnabled = true,
    bool SteamImportRequested = false,
    bool EmulationSetupRequested = false,
    bool WindowsAppScanRequested = true,
    bool GallerySetupRequested = true,
    bool IncludePicturesFolder = true,
    bool PicturesPrivate = false,
    bool ShowPicturesOnHome = true,
    bool PicturesIncludeSubfolders = true,
    bool StartWithWindows = false,
    bool DiagnosticsConsent = false);

public sealed record OnboardingState(
    int SchemaVersion,
    OnboardingStatus Status,
    OnboardingStep CurrentStep,
    bool IsUpgrade,
    OnboardingPreferences Preferences,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 1;

    public static OnboardingState Create(bool isUpgrade, DateTimeOffset now) =>
        new(CurrentSchemaVersion, OnboardingStatus.NeverStarted, OnboardingStep.Welcome, isUpgrade, new(IncludePicturesFolder: !isUpgrade, GallerySetupRequested: !isUpgrade), now);
}

public sealed record OnboardingLoadResult(OnboardingState State, bool RecoveredFromInvalidState, string? RecoveryMessage);

public interface IOnboardingStateStore
{
    Task<OnboardingLoadResult> LoadAsync(bool isUpgrade, CancellationToken cancellationToken = default);
    Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default);
}

public sealed class OnboardingStateStore(string statePath) : IOnboardingStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _statePath = Path.GetFullPath(statePath ?? throw new ArgumentNullException(nameof(statePath)));

    public async Task<OnboardingLoadResult> LoadAsync(bool isUpgrade, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            return new(OnboardingState.Create(isUpgrade, DateTimeOffset.UtcNow), false, null);
        }

        try
        {
            await using FileStream stream = File.OpenRead(_statePath);
            OnboardingState? state = await JsonSerializer.DeserializeAsync<OnboardingState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (state is null || state.SchemaVersion != OnboardingState.CurrentSchemaVersion || !Enum.IsDefined(state.Status) || !Enum.IsDefined(state.CurrentStep))
            {
                throw new InvalidDataException("The onboarding state is incomplete or uses an unsupported schema.");
            }

            return new(state, false, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            string invalidPath = _statePath + $".invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            try
            {
                File.Move(_statePath, invalidPath, false);
            }
            catch (IOException)
            {
                invalidPath = _statePath;
            }

            string message = $"The saved onboarding state was invalid and was preserved at '{invalidPath}'.";
            return new(OnboardingState.Create(isUpgrade, DateTimeOffset.UtcNow), true, message);
        }
    }

    public async Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        string directory = Path.GetDirectoryName(_statePath) ?? throw new InvalidOperationException("The onboarding state path has no parent directory.");
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

public sealed class OnboardingFlowService(IOnboardingStateStore store)
{
    private static readonly OnboardingStep[] Steps = Enum.GetValues<OnboardingStep>();

    public async Task<OnboardingState> StartOrResumeAsync(bool isUpgrade, CancellationToken cancellationToken = default)
    {
        OnboardingLoadResult result = await store.LoadAsync(isUpgrade, cancellationToken).ConfigureAwait(false);
        OnboardingState state = result.State;
        if (state.Status == OnboardingStatus.NeverStarted)
        {
            state = state with { Status = OnboardingStatus.InProgress, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await store.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }

        return state;
    }

    public Task<OnboardingLoadResult> GetStateAsync(bool isUpgrade, CancellationToken cancellationToken = default) =>
        store.LoadAsync(isUpgrade, cancellationToken);

    public async Task<OnboardingState> SavePreferencesAsync(OnboardingState state, OnboardingPreferences preferences, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        OnboardingState updated = state with { Preferences = preferences, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<OnboardingState> MoveAsync(OnboardingState state, int direction, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        int index = Array.IndexOf(Steps, state.CurrentStep);
        int nextIndex = Math.Clamp(index + Math.Sign(direction), 0, Steps.Length - 1);
        OnboardingState updated = state with { CurrentStep = Steps[nextIndex], UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<OnboardingState> CompleteAsync(OnboardingState state, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        OnboardingState completed = state with { Status = OnboardingStatus.Completed, CurrentStep = OnboardingStep.Review, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(completed, cancellationToken).ConfigureAwait(false);
        return completed;
    }

    public async Task<OnboardingState> SkipAsync(OnboardingState state, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        OnboardingState skipped = state with { Status = OnboardingStatus.Skipped, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(skipped, cancellationToken).ConfigureAwait(false);
        return skipped;
    }

    private static void EnsureInProgress(OnboardingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != OnboardingStatus.InProgress)
        {
            throw new InvalidOperationException("Only onboarding that is in progress can be changed.");
        }
    }
}
