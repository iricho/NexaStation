using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexaStation.Compatibility.Feeds;
using NexaStation.Compatibility.Matching;
using NexaStation.Compatibility.Models;
using NexaStation.Compatibility.Profiles;
using NexaStation.Compatibility.Rules;
using NexaStation.Compatibility.Validation;
using NexaStation.Domain;
using Xunit;

namespace NexaStation.Compatibility.Tests;

public sealed class CompatibilityTests
{
    private readonly CompatibilityProfileValidator _validator = new();

    [Fact]
    public async Task ExactProviderIdentityMatchesButDisplayNameCannotMatch()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("steam-10", CompatibilityProfileSource.BuiltIn, providerId: "steam", providerItemId: "10"));
        CompatibilityProfileResolver resolver = new(repository, _validator);
        CompatibilityResolutionResult match = await resolver.ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "10"));
        CompatibilityResolutionResult miss = await resolver.ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "Different Display Name"));
        Assert.Single(match.AppliedProfiles);
        Assert.Empty(miss.AppliedProfiles);
    }

    [Fact]
    public async Task UserProfileOverridesBuiltInAndConflictIsReported()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("built-in", CompatibilityProfileSource.BuiltIn, "steam", "20", timeout: 90));
        await repository.SaveAsync(Profile("user", CompatibilityProfileSource.User, "steam", "20", timeout: 120));
        CompatibilityResolutionResult result = await new CompatibilityProfileResolver(repository, _validator).ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "20"));
        Assert.Equal(120, result.Rules.Tracking.DetectionTimeoutSeconds);
        Assert.Contains(result.Conflicts, value => value.WinningProfileId == "user" && value.ConflictingProfileId == "built-in");
    }

    [Fact]
    public async Task ExactProfileOverridesProviderWideProfile()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("provider", CompatibilityProfileSource.BuiltIn, "steam", null, timeout: 60, targetType: CompatibilityTargetType.Provider));
        await repository.SaveAsync(Profile("exact", CompatibilityProfileSource.BuiltIn, "steam", "30", timeout: 100));
        CompatibilityResolutionResult result = await new CompatibilityProfileResolver(repository, _validator).ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "30"));
        Assert.Equal(100, result.Rules.Tracking.DetectionTimeoutSeconds);
    }

    [Theory]
    [InlineData(">=1.2,<2.0", "1.5", true)]
    [InlineData(">=1.2,<2.0", "2.1", false)]
    [InlineData("not-a-version", "1.0", false)]
    public void VersionConstraintsAreValidated(string expression, string version, bool expected)
    {
        bool parsed = VersionConstraint.TryParse(expression, out VersionConstraint constraint);
        Assert.Equal(expected, parsed && constraint.Matches(version));
    }

    [Theory]
    [InlineData("script")]
    [InlineData("killProcess")]
    [InlineData("registry")]
    [InlineData("download")]
    public void UnsafeFeedRuleFieldsAreRejected(string field)
    {
        using JsonDocument document = JsonDocument.Parse($"{{\"profiles\":[{{\"{field}\":\"unsafe\"}}]}}");
        Assert.False(_validator.ValidateFeedJson(document.RootElement).IsValid);
    }

    [Fact]
    public void InvalidRangesAndIdentityAreRejected()
    {
        CompatibilityProfile profile = new()
        {
            Id = "invalid", TargetMatcher = new(), Rules = new() { Input = new() { NavigationDeadzone = 2 } }, VersionConstraints = new() { Application = "bad" },
        };
        CompatibilityValidationResult result = _validator.Validate(profile);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, value => value.Contains("stable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DisabledProfileIsIgnored()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("disabled", CompatibilityProfileSource.User, "steam", "40") with { Enabled = false });
        CompatibilityResolutionResult result = await new CompatibilityProfileResolver(repository, _validator).ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "40"));
        Assert.Empty(result.AppliedProfiles);
    }

    [Fact]
    public async Task FeedIsDisabledByDefault()
    {
        InMemoryCompatibilityRepository repository = new();
        CompatibilityFeedService service = new(new(), new RejectAllCompatibilityFeedSignatureVerifier(), _validator, repository);
        CompatibilityFeedImportResult result = await service.ImportAsync("{}", "{}");
        Assert.False(result.Succeeded);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidSignedFeedImportsAndInvalidSignaturePreservesPreviousFeed()
    {
        using RSA rsa = RSA.Create(2048);
        string publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        InMemoryCompatibilityRepository repository = new();
        CompatibilityProfile profile = Profile("feed-profile", CompatibilityProfileSource.ImportedSignedFeed, "steam", "50");
        string payload = JsonSerializer.Serialize(new CompatibilityFeedPayload(1, DateTimeOffset.UtcNow, [profile]));
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        string signature = Convert.ToBase64String(rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        CompatibilityFeedManifest manifest = new(1, "test", "RSA-SHA256", Convert.ToHexString(SHA256.HashData(bytes)), signature);
        CompatibilityFeedService service = new(new(true, new Uri("https://example.invalid/feed.json")), new RsaCompatibilityFeedSignatureVerifier(new Dictionary<string, string> { ["test"] = publicKey }), _validator, repository);
        Assert.True((await service.ImportAsync(JsonSerializer.Serialize(manifest), payload)).Succeeded);
        Assert.False((await service.ImportAsync(JsonSerializer.Serialize(manifest with { SignatureBase64 = Convert.ToBase64String([1, 2, 3]) }), payload)).Succeeded);
        Assert.Contains(await repository.GetProfilesAsync(), value => value.Id == "feed-profile");
    }

    [Fact]
    public async Task SignedFeedRejectsUnknownSchemaAndUnknownRule()
    {
        CompatibilityProfile profile = Profile("strict", CompatibilityProfileSource.ImportedSignedFeed, "steam", "55");
        string profileJson = JsonSerializer.Serialize(profile);
        string unknownRule = profileJson.Replace("\"Rules\":{", "\"Rules\":{\"UnknownRule\":true,", StringComparison.Ordinal);
        string unknownPayload = $"{{\"SchemaVersion\":1,\"GeneratedAtUtc\":\"{DateTimeOffset.UtcNow:O}\",\"Profiles\":[{unknownRule}]}}";
        string unknownSchema = JsonSerializer.Serialize(new CompatibilityFeedPayload(2, DateTimeOffset.UtcNow, [profile]));

        CompatibilityFeedImportResult ruleResult = await ImportSignedAsync(unknownPayload);
        CompatibilityFeedImportResult schemaResult = await ImportSignedAsync(unknownSchema);

        Assert.False(ruleResult.Succeeded);
        Assert.Contains("invalid", ruleResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(schemaResult.Succeeded);
        Assert.Contains("unsupported", schemaResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FeedRefreshDoesNotOverwriteUserProfile()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("user", CompatibilityProfileSource.User, "steam", "60"));
        await repository.ReplaceImportedFeedAsync([Profile("feed", CompatibilityProfileSource.ImportedSignedFeed, "steam", "60")]);
        await repository.ReplaceImportedFeedAsync([]);
        Assert.Contains(await repository.GetProfilesAsync(), value => value.Id == "user");
    }

    [Fact]
    public async Task ResetDeletesOnlySelectedUserProfiles()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("user-a", CompatibilityProfileSource.User, "steam", "60"));
        await repository.SaveAsync(Profile("user-b", CompatibilityProfileSource.User, "steam", "61"));
        await repository.SaveAsync(Profile("built-in", CompatibilityProfileSource.BuiltIn, "steam", "60"));

        await repository.DeleteUserProfilesAsync(["user-a", "built-in"]);

        IReadOnlyList<CompatibilityProfile> remaining = await repository.GetProfilesAsync();
        Assert.DoesNotContain(remaining, value => value.Id == "user-a");
        Assert.Contains(remaining, value => value.Id == "user-b");
        Assert.Contains(remaining, value => value.Id == "built-in");
    }

    [Fact]
    public void ControllerProfileContainsNoSerialNumberSurface()
    {
        CompatibilityProfile profile = new() { Id = "controller", Source = CompatibilityProfileSource.User, TargetMatcher = new() { TargetType = CompatibilityTargetType.Controller, ControllerFamily = "xinput" } };
        string json = JsonSerializer.Serialize(profile);
        Assert.DoesNotContain("serial", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(_validator.Validate(profile).IsValid);
    }

    [Fact]
    public async Task ControllerFamilyRuleResolvesDeadzoneAndGlyphWithoutSerialIdentity()
    {
        CompatibilityProfile profile = new() { Id = "dualsense", Source = CompatibilityProfileSource.BuiltIn, TargetMatcher = new() { TargetType = CompatibilityTargetType.Controller, ControllerFamily = "dualsense" }, Rules = new() { Input = new() { NavigationDeadzone = 0.68, ControllerGlyphFamily = "playstation", DisableAnalogNavigation = true } } };
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(profile);
        CompatibilityProfileResolver resolver = new(repository, _validator);

        CompatibilityResolutionResult result = await ControllerCompatibilityPolicy.ResolveAsync(resolver, new("dualsense", VendorId: "054c", ProductId: "0ce6", ConnectionType: "wireless", InputBackend: "Windows.Gaming.Input"));
        InputCompatibilityOverrides input = ControllerCompatibilityPolicy.EffectiveInput(result);

        Assert.Equal(0.68, input.NavigationDeadzone);
        Assert.Equal("playstation", input.ControllerGlyphFamily);
        Assert.True(input.DisableAnalogNavigation);
        Assert.DoesNotContain("serial", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratedSuggestionNeverActivatesAutomatically()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("suggestion", CompatibilityProfileSource.GeneratedDiagnosticSuggestion, "steam", "70", 300));
        CompatibilityResolutionResult result = await new CompatibilityProfileResolver(repository, _validator).ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "70"));
        Assert.Empty(result.AppliedProfiles);
        Assert.Equal(30, CompatibilityLaunchPolicy.Apply(new(LaunchType.Executable, "game.exe"), result.Rules).DetectionTimeoutSeconds);
    }

    [Fact]
    public async Task SignedExactRuleBeatsBuiltInProviderWideRule()
    {
        InMemoryCompatibilityRepository repository = new();
        await repository.SaveAsync(Profile("wide", CompatibilityProfileSource.BuiltIn, "steam", null, 45, CompatibilityTargetType.Provider));
        await repository.SaveAsync(Profile("exact", CompatibilityProfileSource.ImportedSignedFeed, "steam", "70", 90));
        CompatibilityResolutionResult result = await new CompatibilityProfileResolver(repository, _validator).ResolveAsync(new(CompatibilityTargetType.SteamGame, "steam", "70"));
        Assert.Equal(90, result.Rules.Tracking.DetectionTimeoutSeconds);
        Assert.Contains(result.Conflicts, conflict => conflict.WinningProfileId == "exact" && conflict.ConflictingProfileId == "wide");
    }

    [Fact]
    public void LaunchPolicyAppliesOnlyTypedDeclarativeOverrides()
    {
        NexaStation.Domain.LaunchTarget target = new(NexaStation.Domain.LaunchType.Executable, "game.exe") { ProviderId = "steam" };
        CompatibilityRuleSet rules = new() { Launch = new() { KeepFrontendVisible = true }, Tracking = new() { DetectionTimeoutSeconds = 120, IgnoredProcessNames = ["launcher"] }, Window = new() { FullscreenRestoreDelayMilliseconds = 750, DoNotForceForeground = true, RestoreOnlyAfterManualAction = true } };
        NexaStation.Domain.LaunchTarget result = CompatibilityLaunchPolicy.Apply(target, rules);
        Assert.False(result.HideFrontendWhileRunning);
        Assert.Equal(120, result.DetectionTimeoutSeconds);
        Assert.Equal("game.exe", result.ExecutablePath);
        Assert.Equal(750, result.FrontendRestoreDelayMilliseconds);
        Assert.True(result.DoNotForceFrontendForeground);
        Assert.True(result.RestoreFrontendManually);
    }

    [Fact]
    public void EmulatorTargetUsesAdapterPlatformAndDetectedVersionForMatching()
    {
        LibraryItem item = new() { Id = "game", Title = "Game", SortTitle = "Game", Description = "Test", ContentType = ContentType.Game, AddedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        LaunchTarget launch = new(LaunchType.Executable, "emu.exe") { ProviderId = "emulation", EmulatorInstallationId = "local-install", EmulatorAdapterId = "retroarch", EmulatorVersion = "1.20.0", EmulatedPlatformId = "snes" };

        CompatibilityTarget target = CompatibilityLaunchPolicy.CreateTarget(item, launch);

        Assert.Equal("retroarch", target.EmulatorAdapterId);
        Assert.Equal("snes", target.PlatformId);
        Assert.Equal("1.20.0", target.ApplicationVersion);
        Assert.NotEqual("local-install", target.EmulatorAdapterId);
    }

    [Fact]
    public void ExpectedRelativeExecutableAlsoSuppliesAProcessIdentity()
    {
        LaunchTarget launch = new(LaunchType.Uri, Uri: new Uri("steam://rungameid/10")) { ProviderId = "steam" };
        LaunchTarget result = CompatibilityLaunchPolicy.Apply(launch, new() { Tracking = new() { ExpectedRelativeExecutablePath = "bin\\game.exe" } });

        Assert.Equal("game", result.ExpectedProcessName);
        Assert.Equal("bin\\game.exe", result.ExpectedExecutablePath);
    }

    private static CompatibilityProfile Profile(string id, CompatibilityProfileSource source, string? providerId = null, string? providerItemId = null, int timeout = 45, CompatibilityTargetType targetType = CompatibilityTargetType.SteamGame) => new()
    {
        Id = id,
        Source = source,
        TargetMatcher = new() { TargetType = targetType, ProviderId = providerId, ProviderItemId = providerItemId },
        Rules = new() { Tracking = new() { DetectionTimeoutSeconds = timeout } },
    };

    private async Task<CompatibilityFeedImportResult> ImportSignedAsync(string payload)
    {
        using RSA rsa = RSA.Create(2048);
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        CompatibilityFeedManifest manifest = new(1, "strict-test", "RSA-SHA256", Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToBase64String(rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)));
        CompatibilityFeedService service = new(new(true, new Uri("https://example.invalid/feed.json")), new RsaCompatibilityFeedSignatureVerifier(new Dictionary<string, string> { ["strict-test"] = rsa.ExportSubjectPublicKeyInfoPem() }), _validator, new InMemoryCompatibilityRepository());
        return await service.ImportAsync(JsonSerializer.Serialize(manifest), payload);
    }
}
