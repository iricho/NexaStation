using NexaStation.Domain;
using NexaStation.Providers.WindowsApps.AppsFolder;
using NexaStation.Providers.WindowsApps.PackagedApps;
using NexaStation.Providers.WindowsApps.StartMenu;
using Xunit;
using Xunit.Abstractions;

namespace NexaStation.Providers.WindowsApps.Tests;

public sealed class RealWindowsDiscoveryProbeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task CurrentMachineStartMenuProbeCompletesReadOnly()
    {
        StartMenuApplicationScanner scanner=new(new StartMenuKnownFolderResolver(),new ShellLinkReader());WindowsApplicationScanResult result=await scanner.ScanAsync(new());output.WriteLine("Start Menu candidates: {0}; warnings: {1}; errors: {2}",result.Candidates.Count,result.Warnings.Count,result.Errors.Count);Assert.False(result.WasCancelled);Assert.True(result.CompletedSuccessfully);Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task CurrentUserPackagedApplicationProbeCompletesReadOnly()
    {
        AppsFolderScanner scanner=new(new PackagedApplicationReader());WindowsApplicationScanResult result=await scanner.ScanAsync();output.WriteLine("Packaged application candidates: {0}; warnings: {1}; errors: {2}",result.Candidates.Count,result.Warnings.Count,result.Errors.Count);Assert.False(result.WasCancelled);Assert.True(result.CompletedSuccessfully);Assert.Empty(result.Errors);
    }
}
