using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Infrastructure.Launchers;
using Xunit;
using Xunit.Abstractions;

namespace NexaStation.Infrastructure.Tests;

public sealed class RealLauncherProbeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ReadOnlyLauncherProbeWhenExplicitlyEnabled()
    {
        if(!string.Equals(Environment.GetEnvironmentVariable("NEXASTATION_REAL_LAUNCHER_TEST"),"1",StringComparison.Ordinal))return;
        ILibraryProvider[] providers=[new EpicLibraryProvider(),new RegistryLauncherProvider(BuiltInLauncherProfiles.Gog),new RegistryLauncherProvider(BuiltInLauncherProfiles.Ubisoft),new RegistryLauncherProvider(BuiltInLauncherProfiles.Ea),new RegistryLauncherProvider(BuiltInLauncherProfiles.BattleNet)];
        foreach(ILibraryProvider provider in providers)
        {
            ProviderAvailabilityResult availability=await provider.DetectAsync();ProviderScanResult scan=await provider.ScanAsync(new());output.WriteLine($"REAL_LAUNCHER id={provider.Id} detected={availability.Detected} items={scan.DiscoveredItems.Count} warnings={scan.Warnings.Count} errors={scan.Errors.Count}");Assert.Empty(scan.Errors);
        }
    }
}
