using Xunit;

namespace NexaStation.Core.Tests;

public sealed class MyStuffUiContractTests
{
    [Fact]
    public void VisibleNavigationAndActionsUseMyStuffAndShelfLanguage()
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));string xaml=File.ReadAllText(Path.Combine(root,"src","NexaStation.App","MainWindow.xaml"));string actions=File.ReadAllText(Path.Combine(root,"src","NexaStation.Core","DefaultItemActionProvider.cs"));string navigation=File.ReadAllText(Path.Combine(root,"src","NexaStation.App","ViewModels","DashboardViewModel.cs"));
        Assert.Contains("Text=\"My Stuff\"",xaml,StringComparison.Ordinal);Assert.Contains("AutomationProperties.Name=\"My Stuff\"",xaml,StringComparison.Ordinal);Assert.DoesNotContain("Text=\"Collections\"",xaml,StringComparison.Ordinal);Assert.DoesNotContain("Content=\"Create Collection\"",xaml,StringComparison.Ordinal);Assert.DoesNotContain("Add to collection",actions,StringComparison.OrdinalIgnoreCase);Assert.Contains("NavigationSection.Collections,\"My Stuff\"",navigation,StringComparison.Ordinal);
    }

    [Fact]
    public void MyStuffIsBlockedInGuestModeWithoutRenamingSerializedEnum()
    {
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));string window=File.ReadAllText(Path.Combine(root,"src","NexaStation.App","MainWindow.xaml.cs"));string navigation=File.ReadAllText(Path.Combine(root,"src","NexaStation.Domain","NavigationSection.cs"));Assert.Contains("NavigationSection.Collections => false",window,StringComparison.Ordinal);Assert.Contains("Collections",navigation,StringComparison.Ordinal);Assert.DoesNotContain("MyStuff",navigation,StringComparison.Ordinal);
    }
}
