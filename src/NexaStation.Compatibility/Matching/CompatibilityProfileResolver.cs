using System.Reflection;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;
using NexaStation.Compatibility.Rules;
using NexaStation.Compatibility.Validation;

namespace NexaStation.Compatibility.Matching;

public sealed record CompatibilityConflict(string Property, string WinningProfileId, string ConflictingProfileId);

public sealed record CompatibilityResolutionResult(
    CompatibilityTarget Target,
    CompatibilityRuleSet Rules,
    IReadOnlyList<CompatibilityProfile> AppliedProfiles,
    IReadOnlyList<CompatibilityConflict> Conflicts)
{
    public bool HasOverrides => AppliedProfiles.Count > 0;
}

public interface ICompatibilityProfileResolver
{
    Task<CompatibilityResolutionResult> ResolveAsync(CompatibilityTarget target, CancellationToken cancellationToken = default);
}

public sealed class CompatibilityProfileResolver(
    ICompatibilityProfileRepository repository,
    ICompatibilityProfileValidator validator) : ICompatibilityProfileResolver
{
    public async Task<CompatibilityResolutionResult> ResolveAsync(CompatibilityTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IReadOnlyList<CompatibilityProfile> all = await repository.GetProfilesAsync(cancellationToken).ConfigureAwait(false);
        CompatibilityProfile[] matches = all
            .Where(profile => profile.Enabled && profile.Source != CompatibilityProfileSource.GeneratedDiagnosticSuggestion && validator.Validate(profile).IsValid && Matches(profile, target))
            .OrderBy(profile => profile.Source == CompatibilityProfileSource.User ? 0 : 1)
            .ThenByDescending(profile => Specificity(profile.TargetMatcher))
            .ThenBy(profile => SourcePriority(profile.Source))
            .ThenByDescending(profile => profile.ProfileVersion)
            .ThenByDescending(profile => profile.UpdatedAtUtc)
            .ToArray();

        CompatibilityRuleSet rules = new();
        List<CompatibilityConflict> conflicts = [];
        Dictionary<string, (object Value, string ProfileId)> winners = new(StringComparer.Ordinal);
        foreach (CompatibilityProfile profile in matches)
        {
            rules = Merge(rules, profile.Rules, profile.Id, winners, conflicts);
        }
        return new(target, rules, matches, conflicts);
    }

    private static bool Matches(CompatibilityProfile profile, CompatibilityTarget target)
    {
        CompatibilityTargetMatcher matcher = profile.TargetMatcher;
        if (matcher.TargetType is not (CompatibilityTargetType.Unknown or CompatibilityTargetType.Provider) && matcher.TargetType != target.TargetType) return false;
        if (!EqualsStable(matcher.ProviderId, target.ProviderId)) return false;
        if (!EqualsStable(matcher.ProviderItemId, target.ProviderItemId)) return false;
        if (!EqualsStable(matcher.PlatformId, target.PlatformId)) return false;
        if (!EqualsStable(matcher.EmulatorAdapterId, target.EmulatorAdapterId)) return false;
        if (!EqualsStable(matcher.ExecutableIdentity, target.ExecutableIdentity)) return false;
        if (!EqualsStable(matcher.AppUserModelId, target.AppUserModelId)) return false;
        if (!EqualsStable(matcher.ControllerFamily, target.ControllerFamily)) return false;
        if (!EqualsStable(matcher.DisplayIdentity, target.DisplayIdentity)) return false;
        return ConstraintMatches(profile.VersionConstraints.Application, target.ApplicationVersion)
            && ConstraintMatches(profile.VersionConstraints.Windows, target.WindowsVersion);
    }

    private static bool EqualsStable(string? matcher, string? target) => string.IsNullOrWhiteSpace(matcher) || string.Equals(matcher, target, StringComparison.OrdinalIgnoreCase);

    private static bool ConstraintMatches(string? text, string? version) => VersionConstraint.TryParse(text, out VersionConstraint constraint) && constraint.Matches(version);

    private static int SourcePriority(CompatibilityProfileSource source) => source switch
    {
        CompatibilityProfileSource.User => 0,
        CompatibilityProfileSource.BuiltIn => 1,
        CompatibilityProfileSource.ImportedSignedFeed => 2,
        CompatibilityProfileSource.GeneratedDiagnosticSuggestion => 3,
        _ => 4,
    };

    private static int Specificity(CompatibilityTargetMatcher matcher) => new[]
    {
        matcher.ProviderItemId,
        matcher.ExecutableIdentity,
        matcher.AppUserModelId,
        matcher.PlatformId,
        matcher.EmulatorAdapterId,
        matcher.ControllerFamily,
        matcher.DisplayIdentity,
        matcher.ProviderId,
    }.Count(value => !string.IsNullOrWhiteSpace(value));

    private static CompatibilityRuleSet Merge(
        CompatibilityRuleSet target,
        CompatibilityRuleSet incoming,
        string profileId,
        Dictionary<string, (object Value, string ProfileId)> winners,
        List<CompatibilityConflict> conflicts) => new()
    {
        Launch = MergeRecord(target.Launch, incoming.Launch, "launch", profileId, winners, conflicts),
        Tracking = MergeRecord(target.Tracking, incoming.Tracking, "tracking", profileId, winners, conflicts),
        Window = MergeRecord(target.Window, incoming.Window, "window", profileId, winners, conflicts),
        Input = MergeRecord(target.Input, incoming.Input, "input", profileId, winners, conflicts),
        Artwork = MergeRecord(target.Artwork, incoming.Artwork, "artwork", profileId, winners, conflicts),
        Performance = MergeRecord(target.Performance, incoming.Performance, "performance", profileId, winners, conflicts),
    };

    private static T MergeRecord<T>(T target, T incoming, string prefix, string profileId, Dictionary<string, (object Value, string ProfileId)> winners, List<CompatibilityConflict> conflicts) where T : class, new()
    {
        T result = new();
        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? current = property.GetValue(target);
            object? candidate = property.GetValue(incoming);
            bool candidateMeaningful = IsMeaningful(candidate);
            string key = $"{prefix}.{property.Name}";
            object? selected = current;
            if (!IsMeaningful(current) && candidateMeaningful)
            {
                selected = candidate;
                winners[key] = (candidate!, profileId);
            }
            else if (IsMeaningful(current) && candidateMeaningful && !ValuesEqual(current!, candidate!) && winners.TryGetValue(key, out var winner))
            {
                conflicts.Add(new(key, winner.ProfileId, profileId));
            }
            property.SetValue(result, selected);
        }
        return result;
    }

    private static bool IsMeaningful(object? value) => value switch
    {
        null => false,
        string text => !string.IsNullOrWhiteSpace(text),
        System.Collections.ICollection collection => collection.Count > 0,
        _ => true,
    };

    private static bool ValuesEqual(object left, object right)
    {
        if (left is IEnumerable<string> leftValues && right is IEnumerable<string> rightValues) return leftValues.SequenceEqual(rightValues, StringComparer.OrdinalIgnoreCase);
        return Equals(left, right);
    }
}
