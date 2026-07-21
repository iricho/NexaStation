using NexaStation.UI.Navigation;
using NexaStation.UI.Themes.Nexa;
using Xunit;

namespace NexaStation.UI.Themes.Nexa.Tests;

public sealed class NexaThemeContractTests
{
    private readonly NexaTheme _theme = new();

    [Fact]
    public void ManifestUsesStableBuiltInIdentity()
    {
        Assert.Equal("nexa.default", _theme.Descriptor.Id);
        Assert.True(_theme.Descriptor.BuiltIn);
        Assert.True(_theme.Descriptor.SupportsReducedMotion);
        Assert.True(_theme.Descriptor.SupportsHighContrast);
    }

    [Fact]
    public void EveryDestinationHasANexaSpecificPage()
    {
        IReadOnlyDictionary<NavigationDestination, Type> mappings = _theme.GetPageMappings();
        Assert.Equal(Enum.GetValues<NavigationDestination>().Length, mappings.Count);
        Assert.All(Enum.GetValues<NavigationDestination>(), destination => Assert.Contains(destination, mappings.Keys));
        Assert.All(mappings.Values, type => Assert.StartsWith("NexaStation.UI.Themes.Nexa", type.Namespace, StringComparison.Ordinal));
    }

    [Fact]
    public void PageMappingsAreStableAcrossCalls() => Assert.Same(_theme.GetPageMappings(), _theme.GetPageMappings());

    [Fact]
    public void ManifestValidates() => Assert.True(_theme.Validate().IsValid);
}
