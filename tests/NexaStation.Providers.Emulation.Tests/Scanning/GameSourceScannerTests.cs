using System.Diagnostics;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Identification;
using NexaStation.Providers.Emulation.Scanning;
using Xunit.Abstractions;

namespace NexaStation.Providers.Emulation.Tests.Scanning;

public sealed class GameSourceScannerTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),"NexaStation.Emu.Scan."+Guid.NewGuid().ToString("N"));private readonly GameSourceScanner _scanner=new(new FilenameGameIdentifier());
    public GameSourceScannerTests(){Directory.CreateDirectory(_directory);}
    [Fact] public async Task ScansExtensionsCaseInsensitively(){File.WriteAllBytes(Path.Combine(_directory,"One.ISO"),[]);File.WriteAllBytes(Path.Combine(_directory,"ignore.zip"),[]);GameSourceScanResult result=await _scanner.ScanAsync(Source("playstation-2"));Assert.Single(result.Games);Assert.EndsWith("One.ISO",result.Games[0].GamePath);}
    [Fact] public async Task RespectsSubfolderSetting(){Directory.CreateDirectory(Path.Combine(_directory,"nested"));File.WriteAllBytes(Path.Combine(_directory,"nested","One.iso"),[]);Assert.Empty((await _scanner.ScanAsync(Source("playstation-2") with{IncludeSubfolders=false})).Games);Assert.Single((await _scanner.ScanAsync(Source("playstation-2"))).Games);}
    [Fact] public async Task MissingFolderFailsWithoutMarkingComplete(){GameSourceScanResult result=await _scanner.ScanAsync(Source("playstation-2") with{FolderPath=Path.Combine(_directory,"missing")});Assert.False(result.CompletedSuccessfully);Assert.NotEmpty(result.Errors);}
    [Fact] public async Task CancellationDoesNotReportSuccessfulScan(){using CancellationTokenSource cancellation=new();cancellation.Cancel();GameSourceScanResult result=await _scanner.ScanAsync(Source("playstation-2"),cancellationToken:cancellation.Token);Assert.True(result.WasCancelled);Assert.False(result.CompletedSuccessfully);}
    [Fact] public async Task DoesNotReadLargeImageContents(){string path=Path.Combine(_directory,"large.iso");using(FileStream stream=File.Create(path)){stream.SetLength(256L*1024*1024);}GameSourceScanResult result=await _scanner.ScanAsync(Source("playstation-2"));Assert.Single(result.Games);Assert.Equal(256L*1024*1024,result.Games[0].FileSize);}
    private GameSource Source(string platform)=>new(){Id="source",Name="Games",FolderPath=_directory,PlatformId=platform,IncludeSubfolders=true,Enabled=true,CreatedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow};
    public void Dispose(){try{Directory.Delete(_directory,true);}catch(IOException){}}
}

public sealed class ScannerPerformanceTests(ITestOutputHelper output)
{
    [Theory][InlineData(100)][InlineData(1000)][InlineData(5000)][InlineData(20000)]
    public async Task MeasuresSyntheticSource(int count)
    {
        string root=Path.Combine(Path.GetTempPath(),"NexaStation.Emu.Perf."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{for(int i=0;i<count;i++)File.WriteAllBytes(Path.Combine(root,$"Homebrew {i:D5} (USA).iso"),[]);GameSource source=new(){Id="perf",Name="Performance",FolderPath=root,PlatformId="playstation-2",IncludeSubfolders=true,Enabled=true,CreatedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow};GameSourceScanner scanner=new(new FilenameGameIdentifier());Stopwatch timer=Stopwatch.StartNew();GameSourceScanResult result=await scanner.ScanAsync(source);timer.Stop();Stopwatch noChange=Stopwatch.StartNew();GameSourceScanResult second=await scanner.ScanAsync(source);noChange.Stop();output.WriteLine($"PERF files={count} enumerate_filter_normalize_preview_ms={timer.Elapsed.TotalMilliseconds:F3} no_change_rescan_ms={noChange.Elapsed.TotalMilliseconds:F3}");Assert.Equal(count,result.Games.Count);Assert.Equal(count,second.Games.Count);}finally{Directory.Delete(root,true);}
    }
}
