using NexaStation.UI.Settings;
using Xunit;

namespace NexaStation.UI.Contracts.Tests;

public sealed class SettingsCatalogTests
{
    private readonly ISettingsCatalog _catalog = BuiltInSettingsCatalog.Create();

    [Fact]
    public void CatalogIsValid() => Assert.True(_catalog.Validate().IsValid);

    [Fact]
    public void CatalogContainsRequiredCategories()
    {
        string[] required = ["personalization", "input", "providers", "emulators", "artwork", "applications", "media-player", "gallery", "capture", "audio", "display", "network", "privacy", "startup", "updates", "storage", "accessibility", "help", "advanced", "reset-recovery"];
        Assert.Equal(required, _catalog.Categories.Select(category => category.Id));
    }

    [Fact]
    public void EverySettingHasAStableUniqueIdAndCategory()
    {
        Assert.All(_catalog.Settings, setting => Assert.Contains(_catalog.Categories, category => category.Id == setting.CategoryId));
        Assert.Equal(_catalog.Settings.Count, _catalog.Settings.Select(setting => setting.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void DangerousSettingsAreIsolatedInRecovery()
        => Assert.All(_catalog.Settings.Where(setting => setting.Dangerous), setting => Assert.Equal("reset-recovery", setting.CategoryId));

    [Theory]
    [InlineData("DualSense", "input.detected")]
    [InlineData("PlayStation", "input.detected")]
    [InlineData("Steam", "providers.steam")]
    [InlineData("artwork", "artwork.providers")]
    [InlineData("Pictures", "gallery.pictures")]
    [InlineData("screenshot", "capture.explanation")]
    public async Task SearchSupportsDocumentedTerms(string query, string expectedSettingId)
    {
        SettingsSearchService search = new(_catalog);
        IReadOnlyList<SettingsSearchResult> results = await search.SearchAsync(query);
        Assert.Contains(results, result => result.SettingId == expectedSettingId);
        Assert.All(results, result => Assert.Contains("Settings", result.Breadcrumb, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchMatchesDescriptionAndIsDeterministic()
    {
        SettingsSearchService search = new(_catalog);
        IReadOnlyList<SettingsSearchResult> first = await search.SearchAsync("controller mapping");
        IReadOnlyList<SettingsSearchResult> second = await search.SearchAsync("controller mapping");
        Assert.NotEmpty(first);
        Assert.Equal(first.Select(result => result.SettingId), second.Select(result => result.SettingId));
    }
}
