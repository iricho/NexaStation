using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexaStation.Onboarding;

public enum OnboardingStatus { NeverStarted, InProgress, Completed, Skipped }

public enum OnboardingStep
{
    Welcome,
    ProfileAndAppearance,
    GamesAndApplications,
    GalleryAndSystem,
    ReviewAndApply,
}

public enum OnboardingDecisionState
{
    Unanswered,
    OptedIn,
    OptedOut,
    KeepExisting,
}

public enum OnboardingTaskStatus { Pending, Running, Succeeded, Failed, Skipped }

public sealed record OnboardingTaskResult(
    string Id,
    OnboardingTaskStatus Status,
    string? Summary = null,
    string? Error = null);

public sealed record OnboardingPreferences(
    string Layout = "Modern",
    string ThemeId = "nexa-dark",
    string PerformanceProfile = "Balanced",
    string OwnerName = "Owner",
    string AvatarKey = "owner",
    bool ControllerEnabled = true,
    bool SteamImportRequested = false,
    bool OtherLaunchersImportRequested = false,
    bool EmulationSetupRequested = false,
    bool WindowsAppScanRequested = false,
    bool GallerySetupRequested = false,
    bool IncludePicturesFolder = false,
    bool PicturesPrivate = false,
    bool ShowPicturesOnHome = true,
    bool PicturesIncludeSubfolders = true,
    bool StartWithWindows = false,
    bool DiagnosticsConsent = false,
    OnboardingDecisionState AppearanceDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState SteamDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState OtherLaunchersDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState EmulationDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState WindowsAppsDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState GalleryDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState PicturesDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState StartupDecision = OnboardingDecisionState.Unanswered,
    OnboardingDecisionState DiagnosticsDecision = OnboardingDecisionState.Unanswered,
    IReadOnlyList<string>? SelectedCandidateIds = null,
    IReadOnlyList<OnboardingTaskResult>? TaskResults = null,
    bool ApplyConfirmed = false)
{
    public IReadOnlyList<string> CandidateIds => SelectedCandidateIds ?? [];
    public IReadOnlyList<OnboardingTaskResult> Results => TaskResults ?? [];
}

public sealed record OnboardingState(
    int SchemaVersion,
    OnboardingStatus Status,
    OnboardingStep CurrentStep,
    bool IsUpgrade,
    OnboardingPreferences Preferences,
    DateTimeOffset UpdatedAtUtc)
{
    public const int CurrentSchemaVersion = 2;

    public static OnboardingState Create(bool isUpgrade, DateTimeOffset now) =>
        new(CurrentSchemaVersion, OnboardingStatus.NeverStarted, OnboardingStep.Welcome, isUpgrade,
            new(OwnerName: DefaultOwnerName()), now);

    private static string DefaultOwnerName() =>
        string.IsNullOrWhiteSpace(Environment.UserName) ? "Owner" : Environment.UserName.Trim();
}

public sealed record OnboardingLoadResult(OnboardingState State, bool RecoveredFromInvalidState, string? RecoveryMessage);

public sealed record OnboardingValidationResult(bool CanContinue, IReadOnlyList<string> MissingChoices)
{
    public string AccessibleMessage => CanContinue
        ? "Ready to continue."
        : $"Complete: {string.Join(", ", MissingChoices)}.";
}

public interface IOnboardingStateStore
{
    Task<OnboardingLoadResult> LoadAsync(bool isUpgrade, CancellationToken cancellationToken = default);
    Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default);
}

public sealed class TransientOnboardingStateStore(OnboardingState initialState) : IOnboardingStateStore
{
    private OnboardingState _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
    public Task<OnboardingLoadResult> LoadAsync(bool isUpgrade, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(new OnboardingLoadResult(_state, false, null)); }
    public Task SaveAsync(OnboardingState state, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); _state = state ?? throw new ArgumentNullException(nameof(state)); return Task.CompletedTask; }
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
        if (!File.Exists(_statePath)) return new(OnboardingState.Create(isUpgrade, DateTimeOffset.UtcNow), false, null);

        try
        {
            string json = await File.ReadAllTextAsync(_statePath, cancellationToken).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            int schema = document.RootElement.TryGetProperty("schemaVersion", out JsonElement version) ? version.GetInt32() : 1;
            OnboardingState state = schema switch
            {
                OnboardingState.CurrentSchemaVersion => JsonSerializer.Deserialize<OnboardingState>(json, JsonOptions)
                    ?? throw new InvalidDataException("The onboarding state is empty."),
                1 => MigrateV1(json),
                _ => throw new InvalidDataException("The onboarding state uses an unsupported schema."),
            };
            if (!Enum.IsDefined(state.Status) || !Enum.IsDefined(state.CurrentStep))
                throw new InvalidDataException("The onboarding state contains an unsupported value.");
            if (schema == 1) await SaveAsync(state, cancellationToken).ConfigureAwait(false);
            return new(state, false, null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            string invalidPath = _statePath + $".invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            try { File.Move(_statePath, invalidPath, false); } catch (IOException) { invalidPath = _statePath; }
            return new(OnboardingState.Create(isUpgrade, DateTimeOffset.UtcNow), true,
                $"The saved onboarding state was invalid and was preserved at '{invalidPath}'.");
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

    private static OnboardingState MigrateV1(string json)
    {
        LegacyState legacy = JsonSerializer.Deserialize<LegacyState>(json, JsonOptions)
            ?? throw new InvalidDataException("The legacy onboarding state is empty.");
        OnboardingStep step = legacy.CurrentStep switch
        {
            "Welcome" => OnboardingStep.Welcome,
            "Layout" or "Performance" or "Input" => OnboardingStep.ProfileAndAppearance,
            "Providers" or "Applications" => OnboardingStep.GamesAndApplications,
            "Gallery" or "Startup" or "Privacy" => OnboardingStep.GalleryAndSystem,
            "Review" => OnboardingStep.ReviewAndApply,
            _ => throw new InvalidDataException("The legacy onboarding step is unsupported."),
        };
        LegacyPreferences p = legacy.Preferences ?? new();
        OnboardingDecisionState Existing(bool requested) => requested ? OnboardingDecisionState.OptedIn : OnboardingDecisionState.OptedOut;
        OnboardingPreferences preferences = new(
            Layout: p.Layout,
            PerformanceProfile: p.PerformanceProfile,
            OwnerName: string.IsNullOrWhiteSpace(Environment.UserName) ? "Owner" : Environment.UserName.Trim(),
            ControllerEnabled: p.ControllerEnabled,
            SteamImportRequested: p.SteamImportRequested,
            EmulationSetupRequested: p.EmulationSetupRequested,
            WindowsAppScanRequested: p.WindowsAppScanRequested,
            GallerySetupRequested: p.GallerySetupRequested,
            IncludePicturesFolder: p.IncludePicturesFolder,
            PicturesPrivate: p.PicturesPrivate,
            ShowPicturesOnHome: p.ShowPicturesOnHome,
            PicturesIncludeSubfolders: p.PicturesIncludeSubfolders,
            StartWithWindows: p.StartWithWindows,
            DiagnosticsConsent: p.DiagnosticsConsent,
            AppearanceDecision: OnboardingDecisionState.KeepExisting,
            SteamDecision: Existing(p.SteamImportRequested),
            OtherLaunchersDecision: OnboardingDecisionState.OptedOut,
            EmulationDecision: Existing(p.EmulationSetupRequested),
            WindowsAppsDecision: Existing(p.WindowsAppScanRequested),
            GalleryDecision: Existing(p.GallerySetupRequested),
            PicturesDecision: Existing(p.IncludePicturesFolder),
            StartupDecision: Existing(p.StartWithWindows),
            DiagnosticsDecision: Existing(p.DiagnosticsConsent));
        return new(OnboardingState.CurrentSchemaVersion, legacy.Status, step, legacy.IsUpgrade, preferences, legacy.UpdatedAtUtc);
    }

    private sealed record LegacyState(int SchemaVersion, OnboardingStatus Status, string CurrentStep, bool IsUpgrade, LegacyPreferences? Preferences, DateTimeOffset UpdatedAtUtc);
    private sealed record LegacyPreferences(
        string Layout = "Modern", string PerformanceProfile = "Balanced", bool ControllerEnabled = true,
        bool SteamImportRequested = false, bool EmulationSetupRequested = false, bool WindowsAppScanRequested = true,
        bool GallerySetupRequested = true, bool IncludePicturesFolder = true, bool PicturesPrivate = false,
        bool ShowPicturesOnHome = true, bool PicturesIncludeSubfolders = true, bool StartWithWindows = false,
        bool DiagnosticsConsent = false);
}

public sealed class OnboardingFlowService(IOnboardingStateStore store)
{
    private static readonly OnboardingStep[] Steps = Enum.GetValues<OnboardingStep>();

    public async Task<OnboardingState> StartOrResumeAsync(bool isUpgrade, CancellationToken cancellationToken = default)
    {
        OnboardingState state = (await store.LoadAsync(isUpgrade, cancellationToken).ConfigureAwait(false)).State;
        if (state.Status == OnboardingStatus.NeverStarted)
        {
            state = state with { Status = OnboardingStatus.InProgress, UpdatedAtUtc = DateTimeOffset.UtcNow };
            await store.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        return state;
    }

    public Task<OnboardingLoadResult> GetStateAsync(bool isUpgrade, CancellationToken cancellationToken = default) => store.LoadAsync(isUpgrade, cancellationToken);

    public async Task<OnboardingState> SavePreferencesAsync(OnboardingState state, OnboardingPreferences preferences, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        OnboardingState updated = state with { Preferences = preferences, UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public static OnboardingValidationResult Validate(OnboardingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state = ResolveUnansweredForCurrentStep(state);
        OnboardingPreferences p = state.Preferences;
        List<string> missing = state.CurrentStep switch
        {
            OnboardingStep.Welcome => [],
            OnboardingStep.ProfileAndAppearance => Missing(("appearance", p.AppearanceDecision)),
            OnboardingStep.GamesAndApplications => Missing(
                ("Steam", p.SteamDecision), ("other launchers", p.OtherLaunchersDecision),
                ("Windows applications", p.WindowsAppsDecision), ("emulation", p.EmulationDecision)),
            OnboardingStep.GalleryAndSystem => Missing(
                ("Gallery", p.GalleryDecision), ("Pictures folder", p.PicturesDecision),
                ("launch at sign-in", p.StartupDecision), ("diagnostic export logs", p.DiagnosticsDecision)),
            OnboardingStep.ReviewAndApply => p.ApplyConfirmed ? [] : ["Apply these choices"],
            _ => ["this screen"],
        };
        if (state.CurrentStep == OnboardingStep.ProfileAndAppearance && p.AppearanceDecision == OnboardingDecisionState.OptedIn)
        {
            if (string.IsNullOrWhiteSpace(p.OwnerName)) missing.Add("owner name");
            if (string.IsNullOrWhiteSpace(p.Layout)) missing.Add("layout");
            if (string.IsNullOrWhiteSpace(p.ThemeId)) missing.Add("theme");
            if (string.IsNullOrWhiteSpace(p.PerformanceProfile)) missing.Add("performance");
        }
        return new(missing.Count == 0, missing);
    }

    public async Task<OnboardingState> MoveAsync(OnboardingState state, int direction, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        if (direction > 0)
        {
            state = ResolveUnansweredForCurrentStep(state);
            OnboardingValidationResult validation = Validate(state);
            if (!validation.CanContinue) throw new InvalidOperationException(validation.AccessibleMessage);
        }
        int index = Array.IndexOf(Steps, state.CurrentStep);
        int nextIndex = Math.Clamp(index + Math.Sign(direction), 0, Steps.Length - 1);
        OnboardingState updated = state with { CurrentStep = Steps[nextIndex], UpdatedAtUtc = DateTimeOffset.UtcNow };
        await store.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<OnboardingState> CompleteAsync(OnboardingState state, CancellationToken cancellationToken = default)
    {
        EnsureInProgress(state);
        OnboardingValidationResult validation = Validate(state);
        if (state.CurrentStep != OnboardingStep.ReviewAndApply || !validation.CanContinue)
            throw new InvalidOperationException(validation.AccessibleMessage);
        OnboardingState completed = state with { Status = OnboardingStatus.Completed, UpdatedAtUtc = DateTimeOffset.UtcNow };
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

    private static List<string> Missing(params (string Name, OnboardingDecisionState Decision)[] values) =>
        values.Where(value => value.Decision == OnboardingDecisionState.Unanswered).Select(value => value.Name).ToList();

    private static OnboardingState ResolveUnansweredForCurrentStep(OnboardingState state)
    {
        OnboardingPreferences p = state.Preferences;
        OnboardingDecisionState fallback = state.IsUpgrade
            ? OnboardingDecisionState.KeepExisting
            : OnboardingDecisionState.OptedOut;
        OnboardingDecisionState Resolve(OnboardingDecisionState decision) =>
            decision == OnboardingDecisionState.Unanswered ? fallback : decision;

        OnboardingPreferences resolved = state.CurrentStep switch
        {
            OnboardingStep.ProfileAndAppearance => p with
            {
                AppearanceDecision = Resolve(p.AppearanceDecision),
            },
            OnboardingStep.GamesAndApplications => p with
            {
                SteamDecision = Resolve(p.SteamDecision),
                OtherLaunchersDecision = Resolve(p.OtherLaunchersDecision),
                WindowsAppsDecision = Resolve(p.WindowsAppsDecision),
                EmulationDecision = Resolve(p.EmulationDecision),
                SteamImportRequested = p.SteamDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.SteamImportRequested,
                OtherLaunchersImportRequested = p.OtherLaunchersDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.OtherLaunchersImportRequested,
                WindowsAppScanRequested = p.WindowsAppsDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.WindowsAppScanRequested,
                EmulationSetupRequested = p.EmulationDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.EmulationSetupRequested,
            },
            OnboardingStep.GalleryAndSystem => p with
            {
                GalleryDecision = Resolve(p.GalleryDecision),
                PicturesDecision = Resolve(p.PicturesDecision),
                StartupDecision = Resolve(p.StartupDecision),
                DiagnosticsDecision = Resolve(p.DiagnosticsDecision),
                GallerySetupRequested = p.GalleryDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.GallerySetupRequested,
                IncludePicturesFolder = p.PicturesDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.IncludePicturesFolder,
                StartWithWindows = p.StartupDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.StartWithWindows,
                DiagnosticsConsent = p.DiagnosticsDecision == OnboardingDecisionState.Unanswered && !state.IsUpgrade ? false : p.DiagnosticsConsent,
            },
            _ => p,
        };
        return state with { Preferences = resolved };
    }

    private static void EnsureInProgress(OnboardingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != OnboardingStatus.InProgress) throw new InvalidOperationException("Only onboarding that is in progress can be changed.");
    }
}
