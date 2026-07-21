using System.Text.Json;
using NexaStation.Recovery;

namespace NexaStation.Quality.Navigation;

public enum NavigationAuditEventType
{
    FocusMoved,
    FocusMissing,
    FocusTrapped,
    InvalidDirectionalTarget,
    DialogOpened,
    DialogClosed,
    OriginFocusRestored,
    ControllerDisconnected,
    InputDeviceChanged,
}

public sealed record NavigationAuditEvent(DateTimeOffset OccurredAtUtc, NavigationAuditEventType Type, string Surface, string? FromSemanticId, string? ToSemanticId, string? Detail);
public sealed record NavigationAuditReport(int FormatVersion, DateTimeOffset ExportedAtUtc, IReadOnlyList<NavigationAuditEvent> Events, int CriticalFindings);

public interface INavigationAuditService
{
    bool Enabled { get; set; }
    void Record(NavigationAuditEventType type, string surface, string? fromSemanticId = null, string? toSemanticId = null, string? detail = null);
    NavigationAuditReport Snapshot();
    Task<string> ExportAsync(CancellationToken cancellationToken = default);
}

public sealed class NavigationAuditService(INexaStationPaths paths, ISensitiveDataRedactor redactor) : INavigationAuditService
{
    private const int MaximumEvents = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly Queue<NavigationAuditEvent> _events = new();
    public bool Enabled { get; set; }

    public void Record(NavigationAuditEventType type, string surface, string? fromSemanticId = null, string? toSemanticId = null, string? detail = null)
    {
        if (!Enabled) return;
        NavigationAuditEvent entry = new(DateTimeOffset.UtcNow, type, surface, fromSemanticId, toSemanticId, detail is null ? null : redactor.Redact(detail));
        lock (_gate)
        {
            _events.Enqueue(entry);
            while (_events.Count > MaximumEvents) _events.Dequeue();
        }
    }

    public NavigationAuditReport Snapshot()
    {
        lock (_gate)
        {
            NavigationAuditEvent[] copy = _events.ToArray();
            int critical = copy.Count(value => value.Type is NavigationAuditEventType.FocusMissing or NavigationAuditEventType.FocusTrapped);
            return new(1, DateTimeOffset.UtcNow, copy, critical);
        }
    }

    public async Task<string> ExportAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureCreated();
        string target = Path.Combine(paths.Diagnostics, $"navigation-audit-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
        await using FileStream stream = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, Snapshot(), JsonOptions, cancellationToken).ConfigureAwait(false);
        return target;
    }
}

public sealed record FocusRegressionStep(string Input, string ExpectedFocusTarget, string? ExpectedSemanticAction = null);
public sealed record FocusRegressionScenario(string Name, string InitialFocus, IReadOnlyList<FocusRegressionStep> Steps, string BackDestination);
public sealed record FocusRegressionResult(string Scenario, bool Passed, IReadOnlyList<string> Failures);

public static class FocusRegressionRunner
{
    public static FocusRegressionResult Run(FocusRegressionScenario scenario, Func<string, string, (string Focus, string? Action)> transition)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(transition);
        List<string> failures = [];
        string focus = scenario.InitialFocus;
        foreach (FocusRegressionStep step in scenario.Steps)
        {
            (string next, string? action) = transition(focus, step.Input);
            if (!string.Equals(next, step.ExpectedFocusTarget, StringComparison.Ordinal)) failures.Add($"{step.Input}: expected focus '{step.ExpectedFocusTarget}', got '{next}'.");
            if (step.ExpectedSemanticAction is not null && !string.Equals(action, step.ExpectedSemanticAction, StringComparison.Ordinal)) failures.Add($"{step.Input}: expected action '{step.ExpectedSemanticAction}', got '{action ?? "none"}'.");
            focus = next;
        }
        return new(scenario.Name, failures.Count == 0, failures);
    }
}

public static class StandardFocusRegressionCatalog
{
    public static IReadOnlyList<FocusRegressionScenario> All { get; } =
    [
        Page("Home", "hero", "continue-playing", "home-header"),
        Page("Games", "games-first-card", "games-second-card", "games-header"),
        Page("Apps", "apps-installed-mode", "apps-first-card", "apps-header"),
        Page("Media", "media-first-service", "media-view-all", "media-header"),
        Page("Gallery", "gallery-first-item", "gallery-viewer", "gallery-header", "OpenViewer"),
        Page("Library", "library-filter", "library-first-card", "library-header"),
        Page("Collections", "collections-first-card", "collections-manage", "collections-header"),
        Page("Settings", "settings-first-category", "settings-first-action", "settings-header"),
        Page("Quick Menu", "quick-menu-first-action", "quick-menu-second-action", "quick-menu-origin"),
        Page("Onboarding", "onboarding-primary", "onboarding-next", "onboarding-exit"),
        Page("Recovery", "recovery-first-action", "recovery-backup", "recovery-exit"),
        Page("Safe Mode", "safe-mode-continue", "safe-mode-recovery", "safe-mode-exit"),
        Page("Discover Apps", "discover-category", "discover-first-card", "apps-header"),
        Page("Item Details", "details-primary", "details-compatibility", "details-origin"),
        Page("Emulator Setup", "emulator-name", "emulator-next", "emulator-origin"),
        Page("Gallery Viewer", "viewer-surface", "viewer-next", "gallery-origin", "NextItem"),
        Page("Confirmation Dialog", "dialog-primary", "dialog-secondary", "dialog-origin"),
    ];

    private static FocusRegressionScenario Page(string name, string initial, string next, string back, string action = "Navigate") =>
        new(name, initial, [new("Next", next, action), new("Back", back, "NavigateBack")], back);
}
