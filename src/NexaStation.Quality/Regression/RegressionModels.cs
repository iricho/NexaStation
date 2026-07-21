namespace NexaStation.Quality.Regression;

public enum RegressionCaseStatus { Active, Fixed, NeedsRetest, Deferred }

public sealed record RegressionCase(
    string Id,
    string Title,
    string Subsystem,
    string SanitizedFixturePath,
    string ReproductionSteps,
    string ExpectedResult,
    RegressionCaseStatus Status,
    string IntroducedVersion,
    string? FixedVersion,
    DateTimeOffset UpdatedAtUtc);

public interface IRegressionCaseRepository
{
    Task SaveAsync(RegressionCase regressionCase, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegressionCase>> GetAsync(CancellationToken cancellationToken = default);
}
