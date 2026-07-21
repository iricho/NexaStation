using NexaStation.UI.Navigation;
using NexaStation.UI.Themes;
using NexaStation.UI.Themes.Crossbar;
using NexaStation.UI.Themes.Nexa;
using Xunit;

namespace NexaStation.ThemeParity.Tests;

public sealed class ThemeParityTests
{
    private readonly NexaTheme _nexa = new();
    private readonly CrossbarTheme _crossbar = new();

    [Fact]
    public void ThemesExposeIdenticalDestinationSets()
        => Assert.Equal(_nexa.GetPageMappings().Keys.Order(), _crossbar.GetPageMappings().Keys.Order());

    [Theory]
    [MemberData(nameof(AllDestinations))]
    public void EachDestinationUsesIndependentViewTypes(NavigationDestination destination)
    {
        Type nexaType = _nexa.GetPageMappings()[destination]; Type crossbarType = _crossbar.GetPageMappings()[destination];
        Assert.NotEqual(nexaType, crossbarType);
        Assert.NotEqual(nexaType.Namespace, crossbarType.Namespace);
    }

    [Fact]
    public void RegistryResolvesBothThemesWithoutThemeSpecificConditionals()
    {
        ThemePageRegistry registry = new([_nexa, _crossbar]);
        Assert.Equal(typeof(NexaHomePage), registry.Resolve("nexa.default", NavigationDestination.Home));
        Assert.Equal(typeof(CrossbarHomePage), registry.Resolve("nexa.crossbar", NavigationDestination.Home));
    }

    [Fact]
    public void ThemeIdsAndViewTypesDoNotAlias()
    {
        Assert.NotEqual(_nexa.Descriptor.Id, _crossbar.Descriptor.Id);
        Assert.Empty(_nexa.GetPageMappings().Values.Intersect(_crossbar.GetPageMappings().Values));
    }

    public static TheoryData<NavigationDestination> AllDestinations()
    {
        TheoryData<NavigationDestination> data = new();
        foreach (NavigationDestination destination in Enum.GetValues<NavigationDestination>()) data.Add(destination);
        return data;
    }
}
