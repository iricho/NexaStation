using NexaStation.Onboarding;
using Xunit;

namespace NexaStation.Onboarding.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NexaStation-Onboarding-Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingStateUsesSchemaTwoFiveScreenFlow()
    {
        OnboardingLoadResult result = await CreateStore().LoadAsync(false);
        Assert.Equal(OnboardingState.CurrentSchemaVersion, result.State.SchemaVersion);
        Assert.Equal(OnboardingStatus.NeverStarted, result.State.Status);
        Assert.Equal(OnboardingStep.Welcome, result.State.CurrentStep);
        Assert.Equal(5, Enum.GetValues<OnboardingStep>().Length);
        Assert.Equal(OnboardingDecisionState.Unanswered, result.State.Preferences.SteamDecision);
    }

    [Fact]
    public async Task ContinueIsBlockedUntilEveryChoiceOnScreenIsAnswered()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState state = await flow.StartOrResumeAsync(false);
        state = await flow.MoveAsync(state, 1);
        InvalidOperationException profileError = await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MoveAsync(state, 1));
        Assert.Contains("appearance", profileError.Message, StringComparison.OrdinalIgnoreCase);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { AppearanceDecision = OnboardingDecisionState.OptedOut });
        state = await flow.MoveAsync(state, 1);
        InvalidOperationException sourceError = await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MoveAsync(state, 1));
        Assert.Contains("Steam", sourceError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitOptOutsPermitProgressAndPersistAcrossResume()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState state = await flow.StartOrResumeAsync(false);
        state = await flow.MoveAsync(state, 1);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { AppearanceDecision = OnboardingDecisionState.OptedOut });
        state = await flow.MoveAsync(state, 1);
        state = await flow.SavePreferencesAsync(state, state.Preferences with
        {
            SteamDecision = OnboardingDecisionState.OptedOut,
            OtherLaunchersDecision = OnboardingDecisionState.OptedOut,
            WindowsAppsDecision = OnboardingDecisionState.OptedOut,
            EmulationDecision = OnboardingDecisionState.OptedOut,
        });
        state = await flow.MoveAsync(state, 1);
        OnboardingState resumed = await flow.StartOrResumeAsync(false);
        Assert.Equal(OnboardingStep.GalleryAndSystem, resumed.CurrentStep);
        Assert.Equal(OnboardingDecisionState.OptedOut, resumed.Preferences.SteamDecision);
    }

    [Fact]
    public async Task ReviewRequiresExplicitApplyConfirmation()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState state = await MoveToReviewAsync(flow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => flow.CompleteAsync(state));
        state = await flow.SavePreferencesAsync(state, state.Preferences with { ApplyConfirmed = true });
        OnboardingState completed = await flow.CompleteAsync(state);
        Assert.Equal(OnboardingStatus.Completed, completed.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MoveAsync(completed, -1));
    }

    [Fact]
    public async Task LegacySchemaMigratesWithoutLosingTerminalStateOrPreferences()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "onboarding.json"), """
        {
          "schemaVersion": 1,
          "status": "Completed",
          "currentStep": "Review",
          "isUpgrade": true,
          "preferences": { "layout": "Classic", "performanceProfile": "Efficient", "steamImportRequested": true, "includePicturesFolder": false },
          "updatedAtUtc": "2026-01-02T03:04:05+00:00"
        }
        """);
        OnboardingLoadResult result = await CreateStore().LoadAsync(true);
        Assert.Equal(2, result.State.SchemaVersion);
        Assert.Equal(OnboardingStatus.Completed, result.State.Status);
        Assert.Equal(OnboardingStep.ReviewAndApply, result.State.CurrentStep);
        Assert.Equal("Classic", result.State.Preferences.Layout);
        Assert.Equal(OnboardingDecisionState.OptedIn, result.State.Preferences.SteamDecision);
        Assert.False(result.RecoveredFromInvalidState);
    }

    [Fact]
    public void GalleryAndPicturesRemainIndependentDecisions()
    {
        OnboardingState state = OnboardingState.Create(false, DateTimeOffset.UtcNow) with { Status = OnboardingStatus.InProgress, CurrentStep = OnboardingStep.GalleryAndSystem };
        state = state with { Preferences = state.Preferences with { GalleryDecision = OnboardingDecisionState.OptedIn } };
        OnboardingValidationResult result = OnboardingFlowService.Validate(state);
        Assert.False(result.CanContinue);
        Assert.Contains("Pictures folder", result.MissingChoices);
    }

    [Fact]
    public async Task InvalidStateIsPreservedAndReported()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "onboarding.json"), "{not-json");
        OnboardingLoadResult result = await CreateStore().LoadAsync(false);
        Assert.True(result.RecoveredFromInvalidState);
        Assert.NotEmpty(Directory.GetFiles(_root, "*.invalid-*"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private OnboardingStateStore CreateStore() => new(Path.Combine(_root, "onboarding.json"));

    private static async Task<OnboardingState> MoveToReviewAsync(OnboardingFlowService flow)
    {
        OnboardingState state = await flow.StartOrResumeAsync(false);
        state = await flow.MoveAsync(state, 1);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { AppearanceDecision = OnboardingDecisionState.OptedOut });
        state = await flow.MoveAsync(state, 1);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { SteamDecision = OnboardingDecisionState.OptedOut, OtherLaunchersDecision = OnboardingDecisionState.OptedOut, WindowsAppsDecision = OnboardingDecisionState.OptedOut, EmulationDecision = OnboardingDecisionState.OptedOut });
        state = await flow.MoveAsync(state, 1);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { GalleryDecision = OnboardingDecisionState.OptedOut, PicturesDecision = OnboardingDecisionState.OptedOut, StartupDecision = OnboardingDecisionState.OptedOut, DiagnosticsDecision = OnboardingDecisionState.OptedOut });
        return await flow.MoveAsync(state, 1);
    }
}
