using Xunit;

namespace NexaStation.Release.Tests;

public sealed class WindowsPackagingContractTests
{
    [Fact]
    public void ReleaseScriptBuildsAllVelopackArtifactsFromOnePublishDirectory()
    {
        string script = File.ReadAllText(RepositoryFile("scripts", "Build-Release.ps1"));

        Assert.Contains("artifacts", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish\\$Runtime", script, StringComparison.Ordinal);
        Assert.Contains("releases\\$Version", script, StringComparison.Ordinal);
        Assert.Contains("'tool', 'run', 'vpk', 'pack'", script, StringComparison.Ordinal);
        Assert.Contains("Iricho.NexaStation", script, StringComparison.Ordinal);
        Assert.Contains("releases.$channel.json", script, StringComparison.Ordinal);
        Assert.Contains("assets.$channel.json", script, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS.txt", script, StringComparison.Ordinal);
        Assert.Contains("NexaStationReleasePackaging=true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ISCC.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compress-Archive", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackageAndToolUseTheSamePinnedVersion()
    {
        string project = File.ReadAllText(RepositoryFile("src", "NexaStation.App", "NexaStation.App.csproj"));
        string manifest = File.ReadAllText(RepositoryFile(".config", "dotnet-tools.json"));

        Assert.Contains("<PackageReference Include=\"Velopack\" Version=\"1.2.0\" />", project, StringComparison.Ordinal);
        Assert.Contains("\"vpk\"", manifest, StringComparison.Ordinal);
        Assert.Contains("\"version\": \"1.2.0\"", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void VelopackBootstrapRunsBeforeWinUiInitialization()
    {
        string program = File.ReadAllText(RepositoryFile("src", "NexaStation.App", "Program.cs"));

        int bootstrap = program.IndexOf("VelopackApp.Build().Run();", StringComparison.Ordinal);
        int applicationStart = program.IndexOf("Application.Start", bootstrap, StringComparison.Ordinal);
        Assert.True(bootstrap >= 0);
        Assert.True(applicationStart > bootstrap);
        Assert.DoesNotContain("UpdateManager", program, StringComparison.Ordinal);
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
        Assert.Contains("<PackageReference Include=\"Velopack\" Version=\"1.2.0\" />", project, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\Users\\", project, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryFile(params string[] parts)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(new[] { root }.Concat(parts).ToArray());
    }
}
