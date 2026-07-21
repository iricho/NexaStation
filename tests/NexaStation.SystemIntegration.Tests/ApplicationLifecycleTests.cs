using NexaStation.SystemIntegration.Services;
using Xunit;

namespace NexaStation.SystemIntegration.Tests;

public sealed class ApplicationLifecycleTests
{
    [Theory]
    [InlineData("nexastation://section/home", "home")]
    [InlineData("nexastation://section/Gallery", "gallery")]
    [InlineData("nexastation://section/settings", "settings")]
    public void SupportedProtocolRoutesToExistingSection(string uri, string expectedSection)
    {
        ActivationRoute route = new ActivationRouter().Route(new Uri(uri));
        Assert.Equal(ActivationRouteKind.Section, route.Kind);
        Assert.Equal(expectedSection, route.Section);
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("nexastation://section/downloads")]
    [InlineData("nexastation://unknown/home")]
    public void UnsupportedProtocolDoesNotInventANavigationTarget(string uri)
    {
        ActivationRoute route = new ActivationRouter().Route(new Uri(uri));
        Assert.Equal(ActivationRouteKind.Unsupported, route.Kind);
        Assert.Null(route.Section);
    }
}
