using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Rules;

namespace NexaStation.Compatibility.Profiles;

public sealed record CompatibilityVersionConstraints
{
    public string? Application { get; init; }
    public string? Windows { get; init; }
    public string? Adapter { get; init; }
}

public sealed record CompatibilityProfile
{
    public required string Id { get; init; }
    public required CompatibilityTargetMatcher TargetMatcher { get; init; }
    public CompatibilityVersionConstraints VersionConstraints { get; init; } = new();
    public CompatibilityRuleSet Rules { get; init; } = new();
    public string? Notes { get; init; }
    public int ProfileVersion { get; init; } = 1;
    public CompatibilityProfileSource Source { get; init; }
    public bool Enabled { get; init; } = true;
    public bool UserModified { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public interface ICompatibilityProfileRepository
{
    Task<IReadOnlyList<CompatibilityProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CompatibilityProfile profile, CancellationToken cancellationToken = default);
    Task ReplaceImportedFeedAsync(IReadOnlyList<CompatibilityProfile> profiles, CancellationToken cancellationToken = default);
    Task DeleteImportedFeedAsync(CancellationToken cancellationToken = default);
    Task DeleteUserProfilesAsync(IReadOnlyCollection<string> profileIds, CancellationToken cancellationToken = default);
}

public interface ICompatibilityReportRepository
{
    Task SaveAsync(CompatibilityReport report, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompatibilityReport>> GetRecentAsync(int maximumResults, CancellationToken cancellationToken = default);
}

public sealed class InMemoryCompatibilityRepository : ICompatibilityProfileRepository, ICompatibilityReportRepository
{
    private readonly object _gate = new();
    private readonly List<CompatibilityProfile> _profiles = [];
    private readonly List<CompatibilityReport> _reports = [];

    public Task<IReadOnlyList<CompatibilityProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyList<CompatibilityProfile>>(_profiles.ToArray());
    }

    public Task SaveAsync(CompatibilityProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _profiles.RemoveAll(value => string.Equals(value.Id, profile.Id, StringComparison.Ordinal));
            _profiles.Add(profile);
        }
        return Task.CompletedTask;
    }

    public Task ReplaceImportedFeedAsync(IReadOnlyList<CompatibilityProfile> profiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _profiles.RemoveAll(value => value.Source == CompatibilityProfileSource.ImportedSignedFeed && !value.UserModified);
            _profiles.AddRange(profiles);
        }
        return Task.CompletedTask;
    }

    public Task DeleteImportedFeedAsync(CancellationToken cancellationToken = default) => ReplaceImportedFeedAsync([], cancellationToken);

    public Task DeleteUserProfilesAsync(IReadOnlyCollection<string> profileIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profileIds);
        cancellationToken.ThrowIfCancellationRequested();
        HashSet<string> ids = new(profileIds, StringComparer.Ordinal);
        lock (_gate) _profiles.RemoveAll(value => value.Source == CompatibilityProfileSource.User && ids.Contains(value.Id));
        return Task.CompletedTask;
    }

    Task ICompatibilityReportRepository.SaveAsync(CompatibilityReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _reports.RemoveAll(value => value.Id == report.Id);
            _reports.Add(report);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CompatibilityReport>> GetRecentAsync(int maximumResults, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) return Task.FromResult<IReadOnlyList<CompatibilityReport>>(_reports.OrderByDescending(value => value.CreatedAtUtc).Take(Math.Clamp(maximumResults, 0, 1000)).ToArray());
    }
}
