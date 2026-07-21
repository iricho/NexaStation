using NexaStation.Providers.Steam.Discovery;
using NexaStation.Providers.Steam.KeyValues;
using NexaStation.Providers.Steam.Manifests;
using Xunit;
using Xunit.Abstractions;

namespace NexaStation.Providers.Steam.Tests;

public sealed class RealSteamProbeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ReadOnlyRealSteamProbeWhenExplicitlyEnabled()
    {
        if(!string.Equals(Environment.GetEnvironmentVariable("NEXASTATION_REAL_STEAM_TEST"),"1",StringComparison.Ordinal))return;
        SteamInstallationDetectionResult detection=await new SteamInstallationLocator().DetectAsync();
        Assert.True(detection.Detected);Assert.NotNull(detection.InstallationPath);
        IReadOnlyList<SteamLibraryLocation> libraries=await new SteamLibraryLocator(new KeyValuesParser()).FindLibrariesAsync(detection.InstallationPath);
        SteamScanResult scan=await new SteamApplicationScanner(new SteamAppManifestParser(new KeyValuesParser()),8).ScanAsync(libraries);
        output.WriteLine($"REAL_STEAM detected={detection.Detected} libraries={libraries.Count} available_libraries={libraries.Count(value=>value.Available)} applications={scan.Applications.Count} warnings={scan.Warnings.Count} errors={scan.Errors.Count}");
        Assert.NotEmpty(libraries);Assert.NotEmpty(scan.Applications);
    }
}
