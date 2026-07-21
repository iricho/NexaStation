using NexaStation.Onboarding;
using Xunit;

namespace NexaStation.Onboarding.Tests;

public sealed class OnboardingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NexaStation-Onboarding-Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingStateUsesDedicatedFirstRunState()
    {
        OnboardingStateStore store = CreateStore();
        OnboardingLoadResult result = await store.LoadAsync(isUpgrade: false);
        Assert.Equal(OnboardingStatus.NeverStarted, result.State.Status);
        Assert.Equal(OnboardingStep.Welcome, result.State.CurrentStep);
        Assert.False(result.RecoveredFromInvalidState);
    }

    [Fact]
    public async Task ExistingInstallationCanBeMarkedAsUpgrade()
    {
        OnboardingLoadResult result = await CreateStore().LoadAsync(isUpgrade: true);
        Assert.True(result.State.IsUpgrade);
        Assert.False(result.State.Preferences.IncludePicturesFolder);
        Assert.False(result.State.Preferences.GallerySetupRequested);
    }

    [Fact]
    public async Task FreshOnboardingProposesPicturesWithVisibleSafeDefaults()
    {
        OnboardingLoadResult result = await CreateStore().LoadAsync(isUpgrade: false);
        Assert.True(result.State.Preferences.GallerySetupRequested);
        Assert.True(result.State.Preferences.IncludePicturesFolder);
        Assert.True(result.State.Preferences.ShowPicturesOnHome);
        Assert.True(result.State.Preferences.PicturesIncludeSubfolders);
        Assert.False(result.State.Preferences.PicturesPrivate);
    }

    [Fact]
    public async Task PicturesOptOutPersistsBeforeCompletion()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState state = await flow.StartOrResumeAsync(false);
        await flow.SavePreferencesAsync(state, state.Preferences with { IncludePicturesFolder = false });
        Assert.False((await flow.StartOrResumeAsync(false)).Preferences.IncludePicturesFolder);
    }

    [Fact]
    public async Task FlowPersistsResumeStepAndChoices()
    {
        OnboardingStateStore store = CreateStore();
        OnboardingFlowService flow = new(store);
        OnboardingState state = await flow.StartOrResumeAsync(false);
        state = await flow.SavePreferencesAsync(state, state.Preferences with { Layout = "Classic", StartWithWindows = true });
        state = await flow.MoveAsync(state, 1);

        OnboardingState resumed = await flow.StartOrResumeAsync(false);
        Assert.Equal(OnboardingStep.Layout, resumed.CurrentStep);
        Assert.Equal("Classic", resumed.Preferences.Layout);
        Assert.True(resumed.Preferences.StartWithWindows);
    }

    [Fact]
    public async Task BackAndForwardAreClampedToTenStepFlow()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState state = await flow.StartOrResumeAsync(false);
        state = await flow.MoveAsync(state, -1);
        Assert.Equal(OnboardingStep.Welcome, state.CurrentStep);
        for (int index = 0; index < 20; index++) state = await flow.MoveAsync(state, 1);
        Assert.Equal(OnboardingStep.Review, state.CurrentStep);
    }

    [Fact]
    public async Task CompletionIsDurableAndCannotBeMutated()
    {
        OnboardingFlowService flow = new(CreateStore());
        OnboardingState completed = await flow.CompleteAsync(await flow.StartOrResumeAsync(false));
        Assert.Equal(OnboardingStatus.Completed, (await flow.StartOrResumeAsync(false)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MoveAsync(completed, 1));
    }

    [Fact]
    public async Task InvalidStateIsPreservedAndReported()
    {
        OnboardingStateStore store = CreateStore();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "onboarding.json"), "{not-json");
        OnboardingLoadResult result = await store.LoadAsync(false);
        Assert.True(result.RecoveredFromInvalidState);
        Assert.NotNull(result.RecoveryMessage);
        Assert.NotEmpty(Directory.GetFiles(_root, "*.invalid-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private OnboardingStateStore CreateStore() => new(Path.Combine(_root, "onboarding.json"));
}
