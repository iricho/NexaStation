using System.Globalization;
using System.Xml.Linq;
using NexaStation.Customization.Models;
using NexaStation.Presentation.Dashboard;
using Xunit;

namespace NexaStation.Presentation.Tests;

public sealed class AppearanceAndLibraryLayoutTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData(360)]
    [InlineData(720)]
    [InlineData(1280)]
    [InlineData(2464)]
    public void GridColumnsFitTheViewportWithScrollbarSpace(double width)
    {
        LibraryGridMetrics metrics=LibraryGridMetrics.Resolve(width);
        Assert.True(metrics.CardWidth>0);
        Assert.True(metrics.Columns*(metrics.CardWidth+18)<=width-32+0.001);
        Assert.Equal(metrics.CardWidth*0.65,metrics.CardHeight);
    }

    [Fact]
    public void GridSupportsCoverAndSingleColumnListLayouts()
    {
        LibraryGridMetrics covers=LibraryGridMetrics.Resolve(1200,covers:true);
        LibraryGridMetrics list=LibraryGridMetrics.Resolve(1200,compact:true);
        Assert.True(covers.CardHeight>covers.CardWidth);
        Assert.Equal(1,list.Columns);
        Assert.Equal(112,list.CardHeight);
    }

    [Fact]
    public void LibraryHasABoundedViewportOutsideTheHomeScrollViewer()
    {
        XDocument document = ReadXaml("MainWindow.xaml");
        XElement grid = document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "SectionAdaptiveGrid");
        Assert.Equal("True", (string?)grid.Attribute("IsScrollEnabled"));
        Assert.DoesNotContain(grid.Ancestors(), element => element.Name.LocalName is "ScrollViewer" or "StackPanel");
        XElement section = grid.Ancestors().Single(element => (string?)element.Attribute(Xaml + "Name") == "ModernSectionSurface");
        Assert.Equal("2", (string?)section.Attribute("Grid.Row"));
        Assert.Contains(section.Descendants(), element => element.Name.LocalName == "RowDefinition" && (string?)element.Attribute("Height") == "*");
    }

    [Theory]
    [InlineData("NexaGameCard")]
    [InlineData("NexaApplicationTile")]
    public void LibraryCardsUseTheResponsiveDisplaySize(string control)
    {
        XElement border = ReadXaml("Controls/" + control + ".xaml").Root!.Elements().First();
        Assert.Equal("{Binding DisplayWidth}", (string?)border.Attribute("Width"));
        Assert.Equal("{Binding DisplayHeight}", (string?)border.Attribute("Height"));
    }

    [Theory]
    [InlineData(ThemeType.Dark)]
    [InlineData(ThemeType.Light)]
    [InlineData(ThemeType.HighContrast)]
    public void ShellTextHasReadableContrastOnEverySurface(ThemeType theme)
    {
        ThemeSurfacePalette palette = ThemeSurfacePalette.Resolve(theme, "#0B1220");
        foreach (string background in new[] { palette.Background, palette.Surface, palette.Elevated })
        {
            Assert.True(Contrast(palette.Text, background) >= 4.5);
            Assert.True(Contrast(palette.Muted, background) >= 4.5);
        }
    }

    [Fact]
    public void CustomDarkBackgroundIsPreservedAndHighContrastIsOpaque()
    {
        Assert.Equal("#101020", ThemeSurfacePalette.Resolve(ThemeType.Custom, "#101020").Background);
        ThemeSurfacePalette contrast = ThemeSurfacePalette.Resolve(ThemeType.HighContrast, "#101020");
        Assert.Equal("#000000", contrast.Background);
        Assert.Equal(contrast.Text, contrast.Border);
        Assert.Equal(21, Contrast(contrast.Text, contrast.Background));
    }

    [Theory]
    [InlineData("black-gold")]
    [InlineData("custom-black-gold")]
    public void GoldSchemeKeepsTextAndAccentsReadable(string scheme)
    {
        ThemeSurfacePalette palette=ThemeSurfacePalette.Resolve(ThemeType.Dark,"#090806",scheme);
        foreach(string surface in new[]{palette.Background,palette.Surface,palette.Elevated})
        {
            Assert.True(Contrast(palette.Text,surface)>=4.5);
            Assert.True(Contrast(palette.Muted,surface)>=4.5);
            Assert.True(Contrast("#E4BD63",surface)>=4.5);
        }
    }

    private static XDocument ReadXaml(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while(directory is not null && !File.Exists(Path.Combine(directory.FullName,"NexaStation.sln"))) directory=directory.Parent;
        string root = directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return XDocument.Load(Path.Combine(root, "src", "NexaStation.App", relativePath));
    }

    private static double Contrast(string foreground, string background)
    {
        double a = Luminance(foreground), b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            double channel = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }
}
