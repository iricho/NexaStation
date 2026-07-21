using System.Globalization;
using System.Text;
using NexaStation.Domain;

namespace NexaStation.Core;

public static class GameTitleNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder result = new(decomposed.Length);
        bool lastWasSpace = true;
        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                result.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                result.Append(' ');
                lastWasSpace = true;
            }
        }
        return result.ToString().Trim();
    }
}

public sealed class DeterministicGameMatchScorer : IGameMatchScorer
{
    public const int AutoApplyThreshold = 900;
    public const int ReviewThreshold = 520;

    public ArtworkMatchCandidate Score(GameIdentity identity, ArtworkMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(candidate);
        List<string> reasons = [];
        List<string> conflicts = [];
        int score = 0;
        if (candidate.IsDlc)
        {
            conflicts.Add("Candidate is downloadable content rather than the base game.");
            return candidate with { Score = 0, Decision = MatchDecision.Reject, Reasons = reasons, Conflicts = conflicts };
        }
        if (!string.IsNullOrWhiteSpace(identity.PlatformId) && !string.IsNullOrWhiteSpace(candidate.PlatformId))
        {
            if (!string.Equals(identity.PlatformId, candidate.PlatformId, StringComparison.OrdinalIgnoreCase))
            {
                conflicts.Add("Platform conflicts with the local game.");
                return candidate with { Score = 0, Decision = MatchDecision.Reject, Reasons = reasons, Conflicts = conflicts };
            }
            score += 260;
            reasons.Add("Platform matches exactly.");
        }
        string candidateTitle = GameTitleNormalizer.Normalize(candidate.Title);
        if (string.Equals(identity.NormalizedTitle, candidateTitle, StringComparison.Ordinal))
        {
            score += 430;
            reasons.Add("Normalized title matches exactly.");
        }
        else
        {
            double similarity = Similarity(identity.NormalizedTitle, candidateTitle);
            score += (int)Math.Round(similarity * 260, MidpointRounding.AwayFromZero);
            if (similarity >= 0.72) reasons.Add("Title is similar.");
            else conflicts.Add("Title evidence is weak.");
        }
        if (!string.IsNullOrWhiteSpace(identity.ProviderItemId) && string.Equals(identity.ProviderItemId, candidate.ProviderGameId, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
            reasons.Add("External provider ID matches exactly.");
        }
        if (!string.IsNullOrWhiteSpace(identity.SteamAppId)
            && candidate.ExternalIds is not null
            && candidate.ExternalIds.TryGetValue("steam", out string? steamId)
            && string.Equals(identity.SteamAppId, steamId, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
            reasons.Add("Steam App ID matches exactly.");
        }
        if (!string.IsNullOrWhiteSpace(identity.GameSerial) && !string.IsNullOrWhiteSpace(candidate.GameSerial))
        {
            if (string.Equals(identity.GameSerial, candidate.GameSerial, StringComparison.OrdinalIgnoreCase))
            {
                score += 900;
                reasons.Add("Game serial matches exactly.");
            }
            else
            {
                conflicts.Add("Game serial conflicts.");
                return candidate with { Score = 0, Decision = MatchDecision.Reject, Reasons = reasons, Conflicts = conflicts };
            }
        }
        if (identity.OptionalHashes is not null && candidate.Hashes is not null && identity.OptionalHashes.Any(local => candidate.Hashes.TryGetValue(local.Key, out string? remote) && string.Equals(local.Value, remote, StringComparison.OrdinalIgnoreCase)))
        {
            score += 1100;
            reasons.Add("ROM hash matches exactly.");
        }
        if (identity.DiscNumber is not null && candidate.DiscNumber is not null)
        {
            if (identity.DiscNumber == candidate.DiscNumber) { score += 50; reasons.Add("Disc number matches."); }
            else { score -= 160; conflicts.Add("Disc number differs."); }
        }
        if (!string.IsNullOrWhiteSpace(identity.Revision) && !string.IsNullOrWhiteSpace(candidate.Revision))
        {
            if (string.Equals(identity.Revision, candidate.Revision, StringComparison.OrdinalIgnoreCase)) score += 40;
            else { score -= 80; conflicts.Add("Revision differs."); }
        }
        if (!string.IsNullOrWhiteSpace(identity.Region) && !string.IsNullOrWhiteSpace(candidate.Region))
        {
            if (string.Equals(identity.Region, candidate.Region, StringComparison.OrdinalIgnoreCase))
            {
                score += 90;
                reasons.Add("Region matches.");
            }
            else
            {
                score -= 120;
                conflicts.Add("Region differs.");
            }
        }
        if (RemasterConflict(identity.NormalizedTitle, candidateTitle))
        {
            conflicts.Add("Original and remastered editions are distinct.");
            return candidate with { Score = Math.Max(0, score - 500), Decision = MatchDecision.Reject, Reasons = reasons, Conflicts = conflicts };
        }
        bool verifiedIdentity = identity.Confidence >= IdentityConfidence.VerifiedSerial;
        MatchDecision decision = score >= AutoApplyThreshold && (verifiedIdentity || score >= 1000)
            ? MatchDecision.AutoApply
            : score >= ReviewThreshold ? MatchDecision.Review : MatchDecision.Reject;
        return candidate with { Score = Math.Max(0, score), Decision = decision, Reasons = reasons, Conflicts = conflicts };
    }

    private static bool RemasterConflict(string left, string right)
    {
        string[] editionTokens = ["remake", "remastered", "definitive edition"];
        return editionTokens.Any(token => left.Contains(token, StringComparison.Ordinal) != right.Contains(token, StringComparison.Ordinal));
    }

    private static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return 0;
        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];
        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return 1d - (double)previous[right.Length] / Math.Max(left.Length, right.Length);
    }
}
