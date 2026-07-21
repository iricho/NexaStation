namespace NexaStation.Feedback.Triage;

public enum AlphaIssueSeverity { Critical, High, Medium, Low }
public enum AlphaIssueState { NeedsTriage, NeedsInformation, Reproducible, CannotReproduce, Confirmed, FixInProgress, FixReady, VerificationNeeded, Fixed, Deferred, Duplicate, NotNexaStation, ReleaseBlocker }
public enum AlphaIssueCategory { Startup, Packaging, Updates, Database, Steam, WindowsApps, Emulation, Gallery, Media, Controller, Navigation, Display, Audio, Network, Artwork, Accessibility, Performance, Recovery, Security }

public sealed record AlphaIssue(
    string Id,
    string Title,
    AlphaIssueSeverity Severity,
    AlphaIssueCategory Category,
    AlphaIssueState State,
    string SanitizedSummary,
    string? Owner,
    string? TargetRelease,
    string? LinkedTest,
    string? LinkedIssue,
    DateTimeOffset UpdatedAtUtc);

public sealed record AlphaIssueTransitionResult(bool Allowed, string Message);

public static class AlphaIssueWorkflow
{
    private static readonly Dictionary<AlphaIssueState, AlphaIssueState[]> Allowed = new()
    {
        [AlphaIssueState.NeedsTriage] = [AlphaIssueState.NeedsInformation, AlphaIssueState.Reproducible, AlphaIssueState.CannotReproduce, AlphaIssueState.NotNexaStation, AlphaIssueState.Duplicate, AlphaIssueState.ReleaseBlocker],
        [AlphaIssueState.NeedsInformation] = [AlphaIssueState.NeedsTriage, AlphaIssueState.Reproducible, AlphaIssueState.CannotReproduce],
        [AlphaIssueState.Reproducible] = [AlphaIssueState.Confirmed, AlphaIssueState.Deferred, AlphaIssueState.ReleaseBlocker],
        [AlphaIssueState.Confirmed] = [AlphaIssueState.FixInProgress, AlphaIssueState.Deferred, AlphaIssueState.ReleaseBlocker],
        [AlphaIssueState.FixInProgress] = [AlphaIssueState.FixReady, AlphaIssueState.Deferred],
        [AlphaIssueState.FixReady] = [AlphaIssueState.VerificationNeeded, AlphaIssueState.FixInProgress],
        [AlphaIssueState.VerificationNeeded] = [AlphaIssueState.Fixed, AlphaIssueState.FixInProgress, AlphaIssueState.ReleaseBlocker],
        [AlphaIssueState.ReleaseBlocker] = [AlphaIssueState.FixInProgress, AlphaIssueState.VerificationNeeded, AlphaIssueState.Deferred],
        [AlphaIssueState.CannotReproduce] = [AlphaIssueState.NeedsInformation, AlphaIssueState.Reproducible, AlphaIssueState.Deferred],
        [AlphaIssueState.Deferred] = [AlphaIssueState.NeedsTriage, AlphaIssueState.FixInProgress],
        [AlphaIssueState.Fixed] = [AlphaIssueState.VerificationNeeded],
    };

    public static AlphaIssueTransitionResult CanTransition(AlphaIssue issue, AlphaIssueState target, bool causeIdentified = false, bool fixExists = false, bool regressionTestExistsOrNotPractical = false, bool manuallyVerified = false, bool relatedSystemsVerified = false)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (target == AlphaIssueState.Fixed && !(causeIdentified && fixExists && regressionTestExistsOrNotPractical && manuallyVerified && relatedSystemsVerified))
            return new(false, "Fixed requires an identified cause, a fix, regression coverage where practical, original-scenario verification, and related-system regression verification.");
        bool allowed = Allowed.TryGetValue(issue.State, out AlphaIssueState[]? states) && states.Contains(target);
        return new(allowed, allowed ? "Transition is allowed." : $"Transition from {issue.State} to {target} is not allowed.");
    }
}
