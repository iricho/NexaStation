using Xunit;

namespace NexaStation.Recovery.Tests;

public sealed class NexaStationPathTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NexaStation-Path-Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DeveloperPortableMarkerBesideExecutableEnablesPortableMode()
    {
        string installation = Path.Combine(_root, "PortableApp");
        string localAppData = Path.Combine(_root, "LocalAppData");
        Directory.CreateDirectory(installation);
        File.WriteAllText(Path.Combine(installation, "portable.flag"), string.Empty);

        NexaStationPaths paths = Create(installation, localAppData);

        Assert.True(paths.IsPortable);
        Assert.Equal(Path.Combine(installation, "portable.flag"), paths.PortableMarkerPath);
        Assert.Equal(Path.Combine(installation, "Data"), paths.Root);
        Assert.Equal(paths.Root, paths.Data);
        Assert.Equal(Path.Combine(installation, "Data", "nexastation.db"), paths.Database);
    }

    [Fact]
    public void VelopackPortableModeUsesStableRootInsteadOfVersionedContentDirectory()
    {
        string rootAppDirectory = Path.Combine(_root, "VelopackApp");
        string contentDirectory = Path.Combine(rootAppDirectory, "current");
        string localAppData = Path.Combine(_root, "LocalAppData");
        Directory.CreateDirectory(contentDirectory);

        NexaStationPaths paths = new(new NexaStationPathOptions(
            LocalApplicationDataDirectory: localAppData,
            PackageLocation: new(true, true, rootAppDirectory, contentDirectory)));

        Assert.True(paths.IsPortable);
        Assert.Equal(contentDirectory, paths.InstallationDirectory);
        Assert.Equal(Path.Combine(rootAppDirectory, "Data"), paths.Root);
        Assert.DoesNotContain(Path.Combine(rootAppDirectory, "current"), paths.Database, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VelopackInstalledModeIgnoresDeveloperMarkerAndUsesLocalAppData()
    {
        string rootAppDirectory = Path.Combine(_root, "VelopackApp");
        string contentDirectory = Path.Combine(rootAppDirectory, "current");
        string localAppData = Path.Combine(_root, "LocalAppData");
        Directory.CreateDirectory(contentDirectory);
        File.WriteAllText(Path.Combine(contentDirectory, "portable.flag"), string.Empty);

        NexaStationPaths paths = new(new NexaStationPathOptions(
            LocalApplicationDataDirectory: localAppData,
            PackageLocation: new(true, false, rootAppDirectory, contentDirectory)));

        Assert.False(paths.IsPortable);
        Assert.Equal(Path.Combine(localAppData, "NexaStation"), paths.Root);
    }

    [Fact]
    public void MissingMarkerUsesInstalledLocalAppDataLayout()
    {
        string installation = Path.Combine(_root, "InstalledApp");
        string localAppData = Path.Combine(_root, "LocalAppData");
        Directory.CreateDirectory(installation);

        NexaStationPaths paths = Create(installation, localAppData);

        Assert.False(paths.IsPortable);
        Assert.Equal(Path.Combine(localAppData, "NexaStation"), paths.Root);
        Assert.Equal(Path.Combine(localAppData, "NexaStation", "Data"), paths.Data);
        Assert.Equal(Path.Combine(localAppData, "NexaStation", "Data", "nexastation.db"), paths.Database);
    }

    [Fact]
    public void EnsureCreatedCreatesEveryRequiredMutableDirectory()
    {
        string installation = Path.Combine(_root, "PortableApp");
        Directory.CreateDirectory(installation);
        File.WriteAllText(Path.Combine(installation, "portable.flag"), string.Empty);
        NexaStationPaths paths = Create(installation, Path.Combine(_root, "LocalAppData"));

        paths.EnsureCreated();

        Assert.All(new[]
        {
            paths.Root,
            paths.Data,
            paths.State,
            paths.Logs,
            paths.CrashReports,
            paths.Backups,
            paths.Diagnostics,
            paths.Cache,
            paths.ArtworkCache,
            paths.ThumbnailCache,
            paths.IconCache,
            paths.ProviderCache,
            paths.UiSoundCache,
            paths.CollectionMosaicCache,
            paths.Backgrounds,
            paths.ThemeBackgrounds,
            paths.Themes,
        }, path => Assert.True(Directory.Exists(path), $"Expected directory was not created: {path}"));
    }

    [Fact]
    public void DefaultInstallationDirectoryUsesAppContextInsteadOfCurrentWorkingDirectory()
    {
        NexaStationPaths paths = new();

        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
            paths.InstallationDirectory);
    }

    [Fact]
    public void CurrentWorkingDirectoryDoesNotAffectInstalledDataPath()
    {
        string installation = Path.Combine(_root, "App");
        string localAppData = Path.Combine(_root, "LocalAppData");
        string unrelatedWorkingDirectory = Path.Combine(_root, "WorkingDirectory");
        Directory.CreateDirectory(installation);
        Directory.CreateDirectory(unrelatedWorkingDirectory);
        string originalWorkingDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = unrelatedWorkingDirectory;
            NexaStationPaths paths = Create(installation, localAppData);

            Assert.Equal(Path.Combine(localAppData, "NexaStation"), paths.Root);
            Assert.DoesNotContain(unrelatedWorkingDirectory, paths.Database, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDirectory;
        }
    }

    [Fact]
    public void PortableAndInstalledRootsCannotResolveToTheSameDirectory()
    {
        string installation = Path.Combine(_root, "App");
        string localAppData = Path.Combine(_root, "LocalAppData");
        Directory.CreateDirectory(installation);
        NexaStationPaths installed = Create(installation, localAppData);
        File.WriteAllText(Path.Combine(installation, "portable.flag"), string.Empty);
        NexaStationPaths portable = Create(installation, localAppData);

        Assert.NotEqual(installed.Root, portable.Root);
        Assert.StartsWith(installation, portable.Root, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(localAppData, installed.Root, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static NexaStationPaths Create(string installation, string localAppData) =>
        new(new NexaStationPathOptions(installation, localAppData));
}
