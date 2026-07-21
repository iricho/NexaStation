using NexaStation.Quality.Accessibility;
using NexaStation.Quality.CrashFingerprinting;
using NexaStation.Quality.Navigation;
using NexaStation.Quality.Performance;
using NexaStation.Recovery;
using Xunit;

namespace NexaStation.Quality.Tests;

public sealed class QualityTests
{
    [Fact]
    public void SameNormalizedCrashProducesSameFingerprint()
    {
        CrashFingerprintService service = new();
        Exception first = CaptureCrash("C:\\Users\\Alice\\private.txt");
        Exception second = CaptureCrash("D:\\Other\\secret.txt");
        Assert.Equal(service.CreateFingerprint(first, StartupStage.DatabaseReady), service.CreateFingerprint(second, StartupStage.DatabaseReady));
        Assert.DoesNotContain("Alice", service.NormalizeFrames(first), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private.txt", service.NormalizeFrames(first), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DifferentRelevantMethodProducesDifferentFingerprint()
    {
        CrashFingerprintService service = new();
        Assert.NotEqual(service.CreateFingerprint(CaptureCrash("x"), null), service.CreateFingerprint(CaptureOtherCrash(), null));
    }

    [Fact]
    public async Task TrackerIncrementsOccurrencesAndClearsResolvedVersion()
    {
        MemoryCrashRepository repository = new();
        CrashFingerprintTracker tracker = new(new CrashFingerprintService(), repository);
        CrashFingerprintRecord first = await tracker.RecordAsync(CaptureCrash("x"), StartupStage.DatabaseReady, "0.9.0");
        await repository.MarkResolvedAsync(first.Fingerprint, "0.9.1");
        CrashFingerprintRecord second = await tracker.RecordAsync(CaptureCrash("y"), StartupStage.DatabaseReady, "0.9.2");
        Assert.Equal(2, second.OccurrenceCount);
        Assert.Null(second.ResolvedVersion);
    }

    [Fact]
    public void AccessibilityAuditFindsMissingNamesHiddenFocusAndDialogTrap()
    {
        AccessibilityAuditService audit = new();
        IReadOnlyList<AccessibilityFinding> findings = audit.Audit([
            new("button", true, true, true, null, 20, 20),
            new("hidden", true, false, true, "Hidden", 40, 40),
            new("dialog", true, true, true, "Dialog", 100, 100, IsDialog: true, TrapsFocus: false, HasBackAction: false),
        ], highContrastResourcesPresent: false, reducedMotionHonored: false);
        Assert.Contains(findings, value => value.SemanticId == "button" && value.Message.Contains("accessible name"));
        Assert.Contains(findings, value => value.SemanticId == "hidden");
        Assert.Contains(findings, value => value.Severity == AccessibilityIssueSeverity.Critical);
    }

    [Fact]
    public void FocusRegressionScenarioIsDeterministic()
    {
        FocusRegressionScenario scenario = new("Home", "hero", [new("Down", "continue"), new("Back", "header", "NavigateBack")], "header");
        Dictionary<(string, string), (string, string?)> map = new() { [("hero", "Down")] = ("continue", null), [("continue", "Back")] = ("header", "NavigateBack") };
        Assert.True(FocusRegressionRunner.Run(scenario, (focus, input) => map[(focus, input)]).Passed);
    }

    [Fact]
    public void StandardFocusCatalogCoversEveryRequiredPhaseNineSurface()
    {
        string[] required = ["Home", "Games", "Apps", "Media", "Gallery", "Library", "Collections", "Settings", "Quick Menu", "Onboarding", "Recovery", "Safe Mode", "Discover Apps", "Item Details", "Emulator Setup", "Gallery Viewer", "Confirmation Dialog"];
        Assert.Equal(required, StandardFocusRegressionCatalog.All.Select(value => value.Name));
        Assert.All(StandardFocusRegressionCatalog.All, scenario =>
        {
            Assert.NotEmpty(scenario.InitialFocus);
            Assert.NotEmpty(scenario.Steps);
            Assert.All(scenario.Steps, step => { Assert.NotEmpty(step.Input); Assert.NotEmpty(step.ExpectedFocusTarget); Assert.False(string.IsNullOrWhiteSpace(step.ExpectedSemanticAction)); });
            Assert.Equal(scenario.BackDestination, scenario.Steps[^1].ExpectedFocusTarget);
        });
    }

    [Fact]
    public void PerformanceRegressionUsesMedianAndTolerance()
    {
        Assert.False(PerformanceRegressionEvaluator.Compare([100, 101, 99], [105, 106, 104], 10).Regressed);
        Assert.True(PerformanceRegressionEvaluator.Compare([100, 101, 99], [130, 131, 129], 10).Regressed);
    }

    [Fact]
    public void SyntheticDatasetsHaveStableNonPersonalIdentities()
    {
        IReadOnlyList<SyntheticLibraryItem> library = SyntheticDatasetFactory.Library(5000);
        IReadOnlyList<SyntheticGalleryItem> gallery = SyntheticDatasetFactory.Gallery(10000);
        Assert.Equal(5000, library.Count);
        Assert.Equal(10000, gallery.Count);
        Assert.StartsWith("item-", library[0].Id);
    }

    [Fact]
    public void BenchmarkHarnessRequiresMultipleIterations()
    {
        BenchmarkMeasurement result = DeterministicBenchmarkHarness.Measure("synthetic", 5, () => _ = SyntheticDatasetFactory.Library(100));
        Assert.Equal(5, result.Iterations);
        Assert.True(result.MedianMilliseconds >= 0);
    }

    private static Exception CaptureCrash(string path)
    {
        try { ThrowKnown(path); return new InvalidOperationException(); }
        catch (Exception exception) { return exception; }
    }

    private static Exception CaptureOtherCrash()
    {
        try { ThrowOther(); return new InvalidOperationException(); }
        catch (Exception exception) { return exception; }
    }

    private static void ThrowKnown(string path) => throw new InvalidOperationException($"Could not open {path}");
    private static void ThrowOther() => throw new ArgumentException("Different failure");

    private sealed class MemoryCrashRepository : ICrashFingerprintRepository
    {
        private readonly Dictionary<string, CrashFingerprintRecord> _values = [];
        public Task<CrashFingerprintRecord?> GetAsync(string fingerprint, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(fingerprint));
        public Task UpsertAsync(CrashFingerprintRecord record, CancellationToken cancellationToken = default) { _values[record.Fingerprint] = record; return Task.CompletedTask; }
        public Task MarkResolvedAsync(string fingerprint, string resolvedVersion, CancellationToken cancellationToken = default) { _values[fingerprint] = _values[fingerprint] with { ResolvedVersion = resolvedVersion }; return Task.CompletedTask; }
    }
}
