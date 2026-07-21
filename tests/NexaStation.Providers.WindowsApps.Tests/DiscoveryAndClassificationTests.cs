using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.WindowsApps.AppsFolder;
using NexaStation.Providers.WindowsApps.Categorization;
using NexaStation.Providers.WindowsApps.ProgressiveWebApps;
using NexaStation.Providers.WindowsApps.Validation;
using Xunit;

namespace NexaStation.Providers.WindowsApps.Tests;

public sealed class DiscoveryAndClassificationTests
{
    [Theory]
    [InlineData("msedge.exe", "Microsoft Edge")]
    [InlineData("chrome.exe", "Google Chrome")]
    [InlineData("brave.exe", "Brave")]
    [InlineData("chromium.exe", "Chromium")]
    public void PwaDetectorRecognizesKnownBrowserFamilies(string executable,string expected)
    {
        PwaCandidate? result=new PwaDetector().Detect(Candidate(target:Path.Combine("C:\\Browser",executable),arguments:["--app-id=abc"]));
        Assert.NotNull(result);Assert.Equal(expected,result.BrowserFamily);Assert.Equal(ApplicationDiscoveryConfidence.High,result.DetectionConfidence);
    }

    [Fact] public void PwaDetectorRejectsOrdinaryExecutable()=>Assert.Null(new PwaDetector().Detect(Candidate(target:"C:\\Tools\\tool.exe")));
    [Fact] public void PwaDetectorReadsOnlyVisibleArguments(){PwaCandidate? result=new PwaDetector().Detect(Candidate(target:"C:\\Browser\\msedge.exe",arguments:["--app=https://example.test/app","--profile-directory=Profile 2"]));Assert.Equal(new Uri("https://example.test/app"),result?.ApplicationUrl);Assert.Equal("Profile 2",result?.ProfileHint);}
    [Fact] public void PwaDetectorRejectsUnsafeUrl(){PwaCandidate? result=new PwaDetector().Detect(Candidate(target:"C:\\Browser\\msedge.exe",arguments:["--app=file:///secret"]));Assert.NotNull(result);Assert.Null(result.ApplicationUrl);Assert.Equal(ApplicationDiscoveryConfidence.Low,result.DetectionConfidence);}

    [Fact] public void ValidatorRejectsMalformedAumid()=>Assert.NotNull(WindowsApplicationValidator.Validate(Candidate(method:WindowsLaunchMethod.AppUserModelId,aumid:"invalid")));
    [Fact] public void ValidatorAcceptsAumid()=>Assert.Null(WindowsApplicationValidator.Validate(Candidate(method:WindowsLaunchMethod.AppUserModelId,aumid:"Family_abc!App")));
    [Fact] public void ValidatorRejectsMissingShortcut()=>Assert.NotNull(WindowsApplicationValidator.Validate(Candidate(method:WindowsLaunchMethod.ShellLink,shortcut:"C:\\missing.lnk")));
    [Fact] public void ValidatorRejectsUnconfirmedCustomProtocol()=>Assert.Contains("confirmation",WindowsApplicationValidator.Validate(Candidate(method:WindowsLaunchMethod.Uri,uri:new Uri("custom:test"),confidence:ApplicationDiscoveryConfidence.Medium))!,StringComparison.OrdinalIgnoreCase);
    [Fact] public void ValidatorAcceptsHttps()=>Assert.Null(WindowsApplicationValidator.Validate(Candidate(method:WindowsLaunchMethod.Uri,uri:new Uri("https://example.test"),confidence:ApplicationDiscoveryConfidence.Medium)));

    [Theory]
    [InlineData("Uninstall Product",WindowsCandidateKind.PossibleUninstaller,"System",false)]
    [InlineData("Product Documentation",WindowsCandidateKind.Documentation,"Uncategorized",false)]
    [InlineData("Visual Studio Code",WindowsCandidateKind.Application,"Development",true)]
    [InlineData("Spotify Music",WindowsCandidateKind.Application,"Media",true)]
    [InlineData("Remote Desktop",WindowsCandidateKind.Application,"Remote Access",true)]
    public void ClassifierUsesConservativeRules(string name,WindowsCandidateKind kind,string category,bool selected)
    {
        WindowsApplicationClassification result=new WindowsApplicationClassifier(new PwaDetector()).Classify(Candidate(name:name,target:"C:\\Windows\\notepad.exe",confidence:ApplicationDiscoveryConfidence.High));
        Assert.Equal(kind,result.Kind);Assert.Equal(category,result.SuggestedCategory);Assert.Equal(selected,result.SelectedByDefault);
    }

    [Fact]
    public async Task AppsFolderScannerNormalizesAumidAndSkipsUnlaunchableEntries()
    {
        AppsFolderScanner scanner=new(new FakeAppsReader([
            new("Calculator","Microsoft.WindowsCalculator_abc!App","family","full","App",null,"Microsoft",null),
            new("Broken",null,null,null,null,null,null,null)]));
        WindowsApplicationScanResult result=await scanner.ScanAsync();
        Assert.Single(result.Candidates);Assert.Single(result.Warnings);Assert.Equal(WindowsLaunchMethod.AppUserModelId,result.Candidates[0].LaunchMethod);Assert.Equal(ApplicationDiscoveryConfidence.Verified,result.Candidates[0].DiscoveryConfidence);
    }

    [Fact]
    public async Task AppsFolderScannerUsesShellIdentityFallback()
    {
        AppsFolderScanner scanner=new(new FakeAppsReader([new("Shell App",null,null,null,null,null,null,"shell:AppsFolder\\abc")]));
        WindowsApplicationCandidate candidate=Assert.Single((await scanner.ScanAsync()).Candidates);Assert.Equal(WindowsLaunchMethod.ShellItem,candidate.LaunchMethod);Assert.Equal("shell:AppsFolder\\abc",candidate.ProviderItemId);
    }

    [Fact]
    public async Task AppsFolderScannerTurnsReaderFailureIntoDiagnostic()
    {
        WindowsApplicationScanResult result=await new AppsFolderScanner(new ThrowingAppsReader()).ScanAsync();Assert.False(result.CompletedSuccessfully);Assert.Single(result.Errors);Assert.Empty(result.Candidates);
    }

    private static WindowsApplicationCandidate Candidate(string name="Application",string? target=null,IReadOnlyList<string>? arguments=null,WindowsLaunchMethod method=WindowsLaunchMethod.Executable,string? shortcut=null,string? aumid=null,Uri? uri=null,ApplicationDiscoveryConfidence confidence=ApplicationDiscoveryConfidence.High)=>new(){ProviderId="test",ProviderItemId=name,DisplayName=name,TargetPath=target,Arguments=arguments??[],LaunchMethod=method,ShortcutPath=shortcut,AppUserModelId=aumid,ProtocolUri=uri,DiscoveryConfidence=confidence};
    private sealed class FakeAppsReader(IReadOnlyList<AppsFolderApplication> entries):IAppsFolderApplicationReader{public Task<IReadOnlyList<AppsFolderApplication>>ReadAsync(CancellationToken cancellationToken=default)=>Task.FromResult(entries);}
    private sealed class ThrowingAppsReader:IAppsFolderApplicationReader{public Task<IReadOnlyList<AppsFolderApplication>>ReadAsync(CancellationToken cancellationToken=default)=>throw new InvalidOperationException("AppsFolder unavailable");}
}
