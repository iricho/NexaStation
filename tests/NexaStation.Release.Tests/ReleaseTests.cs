using NexaStation.Release;
using System.IO.Compression;
using Xunit;

namespace NexaStation.Release.Tests;

public sealed class ReleaseTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NexaStation-Release-Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("0.8.0", 0, 8, 0, 0)]
    [InlineData("1.2.3.4-alpha", 1, 2, 3, 4)]
    public void SemanticVersionParsesReleaseAndAssemblyForms(string value, int major, int minor, int patch, int revision)
    {
        SemanticVersion version = SemanticVersion.Parse(value);
        Assert.Equal((major, minor, patch, revision), (version.Major, version.Minor, version.Patch, version.Revision));
    }

    [Fact]
    public void VersionOrderingIsDeterministic() => Assert.True(SemanticVersion.Parse("0.8.1").CompareTo(SemanticVersion.Parse("0.8.0.9")) > 0);

    [Fact]
    public void MissingLicenseIsAnExplicitReleaseBlocker()
    {
        CreateManifest("0.8.0.0");
        CreateAssets();
        File.WriteAllText(Path.Combine(_root, "THIRD-PARTY-NOTICES.md"), "notices");
        IReadOnlyList<ReleaseBlocker> blockers = ReleaseReadinessValidator.Validate(new(_root, new(0, 8, 0)));
        Assert.Contains(blockers, blocker => blocker.Code == ReleaseBlockerCode.MissingLicenseDecision);
    }

    [Fact]
    public void ManifestVersionAndReleaseTagMustMatch()
    {
        CreateManifest("0.7.0.0");
        CreateAssets();
        File.WriteAllText(Path.Combine(_root, "LICENSE"), "owner-selected license");
        File.WriteAllText(Path.Combine(_root, "THIRD-PARTY-NOTICES.md"), "notices");
        IReadOnlyList<ReleaseBlocker> blockers = ReleaseReadinessValidator.Validate(new(_root, new(0, 8, 0), "wrong", true));
        Assert.Contains(blockers, blocker => blocker.Code == ReleaseBlockerCode.ManifestVersionMismatch);
        Assert.Contains(blockers, blocker => blocker.Code == ReleaseBlockerCode.InvalidReleaseTag);
    }

    [Fact]
    public void VersionMetadataAndManifestIdentityMustAgree()
    {
        CreateManifest("0.11.75.1", name: "Wrong.Name", publisher: "CN=Wrong", publisherDisplayName: "Wrong Publisher");
        CreateAssets();
        CreateReleaseFiles("0.11.75-alpha.1", "0.11.75.1", "Alpha");
        File.WriteAllText(Path.Combine(_root, "LICENSE"), "owner-selected license");
        File.WriteAllText(Path.Combine(_root, "THIRD-PARTY-NOTICES.md"), "notices");

        IReadOnlyList<ReleaseBlocker> blockers = ReleaseReadinessValidator.Validate(new(_root, new(0, 11, 75, 1)));

        Assert.Contains(blockers, blocker => blocker.Code == ReleaseBlockerCode.PackageIdentityMismatch);
    }

    [Fact]
    public void UnsupportedCapabilitiesBlockPrivateAlphaRelease()
    {
        CreateManifest("0.11.75.1", extraCapability: "broadFileSystemAccess");
        CreateAssets();
        CreateReleaseFiles("0.11.75-alpha.1", "0.11.75.1", "Alpha");
        File.WriteAllText(Path.Combine(_root, "LICENSE"), "owner-selected license");
        File.WriteAllText(Path.Combine(_root, "THIRD-PARTY-NOTICES.md"), "notices");

        IReadOnlyList<ReleaseBlocker> blockers = ReleaseReadinessValidator.Validate(new(_root, new(0, 11, 75, 1)));

        Assert.Contains(blockers, blocker => blocker.Code == ReleaseBlockerCode.UnsupportedCapability);
    }

    [Fact]
    public void TesterBundleContainsRequiredFilesAndNoPrivateKey()
    {
        TesterBundleResult result = TesterBundleBuilder.Create(Request());
        string zipPath = Path.Combine(_root, "NexaStation-0.11.75-alpha.1-Private-Alpha-x64.zip");

        TesterBundleBuilder.WriteZip(result, zipPath);

        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        string[] entries = archive.Entries.Select(entry => entry.FullName.Replace("NexaStation-Private-Alpha/", string.Empty)).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
        Assert.Empty(TesterBundleBuilder.ValidateBundleFiles(entries));
        Assert.DoesNotContain(entries, name => name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, name => name == "INSTALL.txt");
        Assert.Contains("release-manifest.json", result.Sha256Sums, StringComparison.Ordinal);
        Assert.Contains("Trusted People", Text(result, "INSTALL.txt"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NexaStation does not delete", Text(result, "TESTER-GUIDE.txt"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForbiddenBundleOutputsAreRejected()
    {
        string[] errors = TesterBundleBuilder.ValidateBundleFiles(["INSTALL.txt", "NexaStation-Alpha-Test.cer", "secret.pfx"]).ToArray();
        Assert.Contains(errors, error => error.Contains("Forbidden", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void CreateManifest(string version, string name = "NexaStation.Alpha", string publisher = "CN=NexaStation Alpha", string publisherDisplayName = "NexaStation Alpha", string? extraCapability = null)
    {
        string directory = Path.Combine(_root, "src", "NexaStation.App");
        Directory.CreateDirectory(directory);
        string extra = extraCapability is null ? string.Empty : $"<Capability Name=\"{extraCapability}\" />";
        File.WriteAllText(Path.Combine(directory, "Package.appxmanifest"), $"""
<Package>
  <Identity Name="{name}" Publisher="{publisher}" Version="{version}" />
  <Properties><PublisherDisplayName>{publisherDisplayName}</PublisherDisplayName></Properties>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications><Application Id="App" Executable="NexaStation.App.exe" /></Applications>
  <Capabilities><Capability Name="runFullTrust" />{extra}</Capabilities>
</Package>
""");
    }

    private void CreateAssets()
    {
        string directory = Path.Combine(_root, "src", "NexaStation.App", "Assets");
        Directory.CreateDirectory(directory);
        foreach (string asset in new[] { "Square44x44Logo.png", "Square150x150Logo.png", "Wide310x150Logo.png", "StoreLogo.png" }) File.WriteAllText(Path.Combine(directory, asset), "asset");
    }

    private void CreateReleaseFiles(string semantic, string package, string channel)
    {
        File.WriteAllText(Path.Combine(_root, "Version.props"), $"""
<Project><PropertyGroup><NexaStationVersion>{semantic}</NexaStationVersion><NexaStationPackageVersion>{package}</NexaStationPackageVersion><NexaStationReleaseChannel>{channel}</NexaStationReleaseChannel></PropertyGroup></Project>
""");
        string directory = Path.Combine(_root, "eng", "release");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "version.json"), $$"""
{"semanticVersion":"{{semantic}}","packageVersion":"{{package}}","releaseChannel":"{{channel}}","packageName":"NexaStation.Alpha","publisher":"CN=NexaStation Alpha","publisherDisplayName":"NexaStation Alpha","architecture":"x64","minimumWindowsVersion":"10.0.17763.0","diagnosticsFormatVersion":1}
""");
    }

    private static TesterBundleRequest Request() => new(
        "0.11.75-alpha.1",
        "0.11.75.1",
        "Alpha",
        "x64",
        "abc123",
        64,
        "10.0.17763.0",
        "CN=NexaStation Alpha",
        "001122",
        true,
        true,
        [],
        ["WACK and clean-machine results must be recorded only when performed."],
        [1, 2, 3],
        [4, 5, 6]);

    private static string Text(TesterBundleResult result, string path) => System.Text.Encoding.UTF8.GetString(result.Files.Single(file => file.RelativePath == path).Contents);
}
