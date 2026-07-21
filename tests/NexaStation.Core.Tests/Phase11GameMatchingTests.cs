using NexaStation.Domain;
using Xunit;

namespace NexaStation.Core.Tests;

public sealed class Phase11GameMatchingTests
{
    private readonly DeterministicGameMatchScorer _scorer = new();
    [Fact] public void ExactProviderIdAutoApplies() => Assert.Equal(MatchDecision.AutoApply, _scorer.Score(Identity(providerItem: "123", confidence: IdentityConfidence.VerifiedProviderId), Candidate("123")).Decision);
    [Fact] public void ExactPs2SerialAutoApplies() => Assert.Equal(MatchDecision.AutoApply, _scorer.Score(Identity(serial: "SCUS-97481", confidence: IdentityConfidence.VerifiedSerial), Candidate("x") with { GameSerial = "scus-97481" }).Decision);
    [Fact] public void ConflictingSerialRejects() => Assert.Equal(MatchDecision.Reject, _scorer.Score(Identity(serial: "SCUS-97481", confidence: IdentityConfidence.VerifiedSerial), Candidate("x") with { GameSerial = "SLUS-20035" }).Decision);
    [Fact] public void ExactPlatformAndTitleIsReviewWithoutVerifiedEvidence() => Assert.Equal(MatchDecision.Review, _scorer.Score(Identity(), Candidate("x")).Decision);
    [Fact] public void RegionIncreasesScore() => Assert.True(_scorer.Score(Identity(region: "USA"), Candidate("x") with { Region = "USA" }).Score > _scorer.Score(Identity(region: null), Candidate("x") with { Region = null }).Score);
    [Fact] public void WrongPlatformRejects() => Assert.Equal(MatchDecision.Reject, _scorer.Score(Identity(), Candidate("x") with { PlatformId = "playstation-3" }).Decision);
    [Fact] public void RemasterAndOriginalRemainDistinct() => Assert.Equal(MatchDecision.Reject, _scorer.Score(Identity(title: "Shadow of the Colossus"), Candidate("x") with { Title = "Shadow of the Colossus Remastered" }).Decision);
    [Fact] public void DiscNumberConflictCannotAutoApply() => Assert.NotEqual(MatchDecision.AutoApply, _scorer.Score(Identity(disc: 1, confidence: IdentityConfidence.VerifiedSerial), Candidate("x") with { DiscNumber = 2 }).Decision);
    [Fact] public void FilenameOnlyIdentityStaysBelowAutoApply() => Assert.NotEqual(MatchDecision.AutoApply, _scorer.Score(Identity(confidence: IdentityConfidence.FilenameOnly), Candidate("x")).Decision);
    [Fact] public void FuzzyTitleRequiresReview() => Assert.NotEqual(MatchDecision.AutoApply, _scorer.Score(Identity(title: "God of War Two"), Candidate("x") with { Title = "God of War II" }).Decision);
    [Fact] public void ExactHashAutoApplies() { GameIdentity identity = Identity(confidence: IdentityConfidence.VerifiedHash) with { OptionalHashes = new Dictionary<string, string> { ["sha1"] = "abc" } }; Assert.Equal(MatchDecision.AutoApply, _scorer.Score(identity, Candidate("x") with { Hashes = new Dictionary<string, string> { ["sha1"] = "ABC" } }).Decision); }
    [Fact] public void ExactSteamAppIdAutoApplies() { GameIdentity identity = Identity(confidence: IdentityConfidence.VerifiedProviderId) with { SteamAppId = "1091500" }; ArtworkMatchCandidate candidate = Candidate("42") with { ExternalIds = new Dictionary<string, string> { ["steam"] = "1091500" } }; Assert.Equal(MatchDecision.AutoApply, _scorer.Score(identity, candidate).Decision); }
    [Fact] public void DifferentSteamAppIdDoesNotCreateExactEvidence() { GameIdentity identity = Identity(confidence: IdentityConfidence.PlatformAndTitle) with { SteamAppId = "1091500" }; ArtworkMatchCandidate candidate = Candidate("42") with { ExternalIds = new Dictionary<string, string> { ["steam"] = "999" } }; Assert.NotEqual(MatchDecision.AutoApply, _scorer.Score(identity, candidate).Decision); }
    [Fact] public void DlcIsRejected() => Assert.Equal(MatchDecision.Reject, _scorer.Score(Identity(), Candidate("x") with { IsDlc = true }).Decision);
    [Theory] [InlineData("Pokémon – Édition", "pokemon edition")] [InlineData("  GOD__OF--WAR  ", "god of war")] public void TitleNormalizationIsDeterministic(string value, string expected) => Assert.Equal(expected, GameTitleNormalizer.Normalize(value));

    private static GameIdentity Identity(string title = "God of War II", string? providerItem = null, string? serial = null, string? region = "USA", int? disc = null, IdentityConfidence confidence = IdentityConfidence.PlatformAndTitle) => new("item", ContentType.Game, GameTitleNormalizer.Normalize(title), title, "playstation-2", "local", providerItem, GameSerial: serial, Region: region, DiscNumber: disc, Confidence: confidence);
    private static ArtworkMatchCandidate Candidate(string id) => new("provider", id, "God of War II", "playstation-2", "USA", 2007);
}
