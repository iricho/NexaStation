using NexaStation.UI.Navigation;
using NexaStation.UI.Themes.Crossbar;
using Xunit;

namespace NexaStation.UI.Themes.Crossbar.Tests;

public sealed class CrossbarThemeContractTests
{
    private readonly CrossbarTheme _theme = new();

    [Fact]
    public void ManifestUsesStableOriginalThemeIdentity()
    {
        Assert.Equal("nexa.crossbar", _theme.Descriptor.Id);
        Assert.True(_theme.Descriptor.BuiltIn);
        Assert.True(_theme.Descriptor.SupportsCrossbarNavigation);
        Assert.Contains("original", _theme.Descriptor.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryDestinationHasACrossbarSpecificPage()
    {
        IReadOnlyDictionary<NavigationDestination, Type> mappings = _theme.GetPageMappings();
        Assert.Equal(Enum.GetValues<NavigationDestination>().Length, mappings.Count);
        Assert.All(Enum.GetValues<NavigationDestination>(), destination => Assert.Contains(destination, mappings.Keys));
        Assert.All(mappings.Values, type => Assert.StartsWith("NexaStation.UI.Themes.Crossbar", type.Namespace, StringComparison.Ordinal));
    }

    [Fact]
    public void ThemeDeclaresCrossbarAppropriateInputAndPerformance()
    {
        Assert.Contains("Controller", _theme.Descriptor.RecommendedInput, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(_theme.Descriptor.RelativePerformance));
    }

    [Fact]
    public void ManifestValidates() => Assert.True(_theme.Validate().IsValid);
}
