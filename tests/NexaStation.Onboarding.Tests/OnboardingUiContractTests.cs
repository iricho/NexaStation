using Xunit;

namespace NexaStation.Onboarding.Tests;

public sealed class OnboardingUiContractTests
{
    [Fact]
    public void FiveScreenUiRequiresExplicitSourceAndSystemDecisions()
    {
        string root = FindRepositoryRoot();
        string xaml = File.ReadAllText(Path.Combine(root, "src", "NexaStation.App", "OnboardingWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(root, "src", "NexaStation.App", "OnboardingWindow.xaml.cs"));
        Assert.Contains("Maximum=\"5\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"steam:in\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"steam:out\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"gallery:in\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"gallery:out\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Don't scan Steam", xaml, StringComparison.Ordinal);
        Assert.Contains("Leave Gallery disabled", xaml, StringComparison.Ordinal);
        Assert.Contains("Continue skips anything you leave unselected", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Not now", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Not now", code, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Apply these choices to this PC", xaml, StringComparison.Ordinal);
        Assert.Contains("NextButton.IsEnabled = canContinue", code, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Step {index + 1} of 10", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ModernSettingsTabOpensThePopulatedSettingsHub()
    {
        string root = FindRepositoryRoot();
        string main = File.ReadAllText(Path.Combine(root, "src", "NexaStation.App", "MainWindow.xaml.cs"));
        Assert.Contains("if (section.Section == NavigationSection.Settings)", main, StringComparison.Ordinal);
        Assert.Contains("await OpenSettingsHomeAsync();", main, StringComparison.Ordinal);
        Assert.Contains("SettingsViewHost.Content=_themeManager.CreateSettingsView", main, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsExposesNonBlockingSetupAssistant()
    {
        string root = FindRepositoryRoot();
        string main = File.ReadAllText(Path.Combine(root, "src", "NexaStation.App", "MainWindow.xaml.cs"));
        Assert.Contains("Run Setup Assistant", main, StringComparison.Ordinal);
        Assert.Contains("TransientOnboardingStateStore", main, StringComparison.Ordinal);
        Assert.Contains("draft was discarded", main, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NexaStation.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the NexaStation repository root.");
    }
}
