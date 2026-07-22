using Xunit;

namespace NexaStation.Release.Tests;

public sealed class WindowsPackagingContractTests
{
    [Fact]
    public void ReleaseScriptBuildsPortableAndInstallerFromOnePublishDirectory()
    {
        string script = File.ReadAllText(RepositoryFile("scripts", "Build-Release.ps1"));

        Assert.Contains("artifacts", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish\\$Runtime", script, StringComparison.Ordinal);
        Assert.Contains("portable.flag", script, StringComparison.Ordinal);
        Assert.Contains("Compress-Archive", script, StringComparison.Ordinal);
        Assert.Contains("ISCC.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA256SUMS.txt", script, StringComparison.Ordinal);
        Assert.Contains("NexaStationReleasePackaging=true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerIsPerUserAndDefensivelyExcludesPortableData()
    {
        string installer = File.ReadAllText(RepositoryFile("installer", "NexaStation.iss"));

        Assert.Contains("PrivilegesRequired=lowest", installer, StringComparison.Ordinal);
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\NexaStation", installer, StringComparison.Ordinal);
        Assert.Contains("AppId={{9AFC071D-292C-4D3B-921B-31B518BB2606}", installer, StringComparison.Ordinal);
        Assert.Contains("Excludes: \"portable.flag,Data\\*\"", installer, StringComparison.Ordinal);
        Assert.Contains("NexaStation.App.exe", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("{userappdata}\\NexaStation", installer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleasePublishIsSelfContainedUnpackagedAndUntrimmed()
    {
        string project = File.ReadAllText(RepositoryFile("src", "NexaStation.App", "NexaStation.App.csproj"));

        Assert.Contains("<WindowsPackageType>None</WindowsPackageType>", project, StringComparison.Ordinal);
        Assert.Contains("<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>", project, StringComparison.Ordinal);
        Assert.Contains("<SelfContained>true</SelfContained>", project, StringComparison.Ordinal);
        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", project, StringComparison.Ordinal);
        Assert.Contains("<PublishSingleFile>false</PublishSingleFile>", project, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryFile(params string[] parts)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(new[] { root }.Concat(parts).ToArray());
    }
}
