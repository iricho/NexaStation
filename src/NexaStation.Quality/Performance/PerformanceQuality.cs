using System.Diagnostics;

namespace NexaStation.Quality.Performance;

public enum MachinePerformanceProfile { LowEnd, MidRange, HighEnd }

public sealed record PerformanceBaseline(string Id, string Scenario, MachinePerformanceProfile MachineProfile, string ApplicationVersion, string MetricName, double MetricValue, string Unit, DateTimeOffset RecordedAtUtc);
public sealed record PerformanceRegressionResult(bool Regressed, double PreviousMedian, double CurrentMedian, double ChangePercent, double TolerancePercent, string Message);

public interface IPerformanceBaselineRepository
{
    Task SaveAsync(PerformanceBaseline baseline, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PerformanceBaseline>> GetAsync(string scenario, MachinePerformanceProfile machineProfile, string metricName, CancellationToken cancellationToken = default);
}

public static class PerformanceRegressionEvaluator
{
    public static PerformanceRegressionResult Compare(IEnumerable<double> baseline, IEnumerable<double> current, double tolerancePercent)
    {
        double previous = Median(baseline);
        double now = Median(current);
        if (previous <= 0) throw new ArgumentOutOfRangeException(nameof(baseline), "The baseline median must be positive.");
        double change = (now - previous) / previous * 100d;
        bool regressed = change > Math.Max(0, tolerancePercent);
        return new(regressed, previous, now, change, tolerancePercent, regressed ? "Performance exceeded the configured regression tolerance." : "Performance remains within the configured regression tolerance.");
    }

    public static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("At least one measurement is required.", nameof(values));
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2d : sorted[middle];
    }
}

public sealed record SyntheticLibraryItem(string Id, string ProviderId, string ProviderItemId, string Title, bool Favorite, long PlaytimeSeconds);
public sealed record SyntheticGalleryItem(string Id, string SourceId, int Width, int Height, DateTimeOffset CapturedAtUtc);

public static class SyntheticDatasetFactory
{
    public static IReadOnlyList<SyntheticLibraryItem> Library(int count) => Enumerable.Range(0, Math.Max(0, count)).Select(index => new SyntheticLibraryItem($"item-{index:D6}", index % 3 == 0 ? "steam" : "synthetic", index.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Synthetic Item {index:D6}", index % 11 == 0, index * 17L)).ToArray();
    public static IReadOnlyList<SyntheticGalleryItem> Gallery(int count) => Enumerable.Range(0, Math.Max(0, count)).Select(index => new SyntheticGalleryItem($"gallery-{index:D6}", $"source-{index % 8}", 1920, 1080, DateTimeOffset.UnixEpoch.AddMinutes(index))).ToArray();
}

public sealed record BenchmarkMeasurement(string Scenario, int Iterations, double MedianMilliseconds, double MinimumMilliseconds, double MaximumMilliseconds, DateTimeOffset MeasuredAtUtc);

public static class DeterministicBenchmarkHarness
{
    public static BenchmarkMeasurement Measure(string scenario, int iterations, Action action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);
        ArgumentNullException.ThrowIfNull(action);
        if (iterations < 3) throw new ArgumentOutOfRangeException(nameof(iterations), "Use at least three iterations so the median is meaningful.");
        action();
        double[] samples = new double[iterations];
        for (int index = 0; index < iterations; index++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }
        return new(scenario, iterations, PerformanceRegressionEvaluator.Median(samples), samples.Min(), samples.Max(), DateTimeOffset.UtcNow);
    }
}
