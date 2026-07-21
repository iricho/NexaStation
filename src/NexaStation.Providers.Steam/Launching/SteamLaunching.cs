using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Steam.Launching;

public interface ISteamLaunchTargetFactory
{
    Uri CreateRunUri(string appId);
    LaunchTarget Create(string appId,string? installationPath=null,string? expectedProcessName=null);
}

public sealed class SteamLaunchTargetFactory : ISteamLaunchTargetFactory
{
    public Uri CreateRunUri(string appId)
    {
        if(!long.TryParse(appId,out long value)||value<=0)throw new ArgumentException("Steam App ID must be a positive number.",nameof(appId));
        return new Uri($"steam://run/{value}");
    }
    public LaunchTarget Create(string appId,string? installationPath=null,string? expectedProcessName=null)=>new(LaunchType.Uri,WorkingDirectory:installationPath,Uri:CreateRunUri(appId),ExpectedProcessName:expectedProcessName){Name="Steam",ProviderId="steam",IsEnabled=true,UserManaged=false};
}

public sealed record ProcessSnapshot(int ProcessId,string Name,string? ExecutablePath,DateTime? StartedAtUtc);
public interface IProcessSnapshotProvider{IReadOnlyList<ProcessSnapshot> Capture();RunningProcess? Attach(int processId);}

public sealed class WindowsProcessSnapshotProvider : IProcessSnapshotProvider
{
    public IReadOnlyList<ProcessSnapshot> Capture()
    {
        List<ProcessSnapshot> result=[];
        foreach(Process process in Process.GetProcesses())
        {
            try{result.Add(new(process.Id,process.ProcessName,TryPath(process),TryStart(process)));}catch(InvalidOperationException){}finally{process.Dispose();}
        }
        return result;
    }
    public RunningProcess? Attach(int processId)
    {
        try
        {
            Process process=Process.GetProcessById(processId);process.EnableRaisingEvents=true;TaskCompletionSource<int> exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? sender,EventArgs args){try{exited.TrySetResult(process.ExitCode);}catch(InvalidOperationException){exited.TrySetResult(0);}}
            process.Exited+=Handler;if(process.HasExited)exited.TrySetResult(process.ExitCode);
            return new(processId,exited.Task,()=>{process.Exited-=Handler;process.Dispose();return ValueTask.CompletedTask;});
        }
        catch(Exception exception) when(exception is ArgumentException or InvalidOperationException){return null;}
    }
    private static string? TryPath(Process process){try{return process.MainModule?.FileName;}catch(Exception exception) when(exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){return null;}}
    private static DateTime? TryStart(Process process){try{return process.StartTime.ToUniversalTime();}catch(Exception exception) when(exception is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){return null;}}
}

public sealed class SteamLaunchedApplicationDetector(IProcessSnapshotProvider snapshots,ISettingsService? settings=null) : ILaunchedApplicationDetector
{
    private static readonly HashSet<string> DefaultIgnored=new(["steam","steamwebhelper","gameoverlayui","crashhandler","steamservice"],StringComparer.OrdinalIgnoreCase);
    public bool CanDetect(LaunchTarget target)=>string.Equals(target.ProviderId,"steam",StringComparison.OrdinalIgnoreCase)&&target.Uri?.Scheme.Equals("steam",StringComparison.OrdinalIgnoreCase)==true;

    public async Task<LaunchedApplicationDetectionResult> DetectAsync(LaunchedApplicationDetectionRequest request,CancellationToken cancellationToken=default)
    {
        DateTime deadline=DateTime.UtcNow+request.Timeout;HashSet<int> baseline=snapshots.Capture().Where(value=>value.StartedAtUtc is null||value.StartedAtUtc<request.LaunchStartedAtUtc.AddSeconds(-2)).Select(value=>value.ProcessId).ToHashSet();
        HashSet<string> ignored=new(DefaultIgnored,StringComparer.OrdinalIgnoreCase);ignored.UnionWith(request.IgnoredProcessNames.Select(NormalizeName));
        HashSet<string> expected=new(request.ExpectedProcessNames.Select(NormalizeName),StringComparer.OrdinalIgnoreCase);
        string? appId=request.LaunchTarget.Uri?.AbsolutePath.Trim('/');
        if(settings is not null&&appId is not null){string? learned=await settings.GetAsync<string?>($"Providers.Steam.ProcessRules.{appId}",null,cancellationToken).ConfigureAwait(false);if(!string.IsNullOrWhiteSpace(learned))expected.Add(NormalizeName(learned));}
        Dictionary<int,(ProcessSnapshot Snapshot,double Confidence)> candidates=[];DateTime? firstDetected=null;
        while(DateTime.UtcNow<deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach(ProcessSnapshot process in snapshots.Capture())
            {
                string name=NormalizeName(process.Name);if(baseline.Contains(process.ProcessId)||ignored.Contains(name))continue;
                bool expectedName=expected.Contains(name);bool inside=Inside(request.InstallationPath,process.ExecutablePath);bool newEnough=process.StartedAtUtc is null||process.StartedAtUtc>=request.LaunchStartedAtUtc.AddSeconds(-2);
                if(!newEnough||(!expectedName&&!inside))continue;
                candidates[process.ProcessId]=(process,expectedName?0.98:0.88);firstDetected??=DateTime.UtcNow;
            }
            if(firstDetected is not null&&DateTime.UtcNow-firstDetected>=request.RelatedProcessGracePeriod)break;
            await Task.Delay(request.PollInterval,cancellationToken).ConfigureAwait(false);
        }
        List<RunningProcess> running=[];List<string> names=[];double confidence=0;
        foreach((ProcessSnapshot process,double score) in candidates.Values.OrderByDescending(value=>value.Confidence))
        {
            RunningProcess? attached=snapshots.Attach(process.ProcessId);if(attached is null)continue;running.Add(attached);names.Add(process.Name);confidence=Math.Max(confidence,score);
        }
        if(running.Count==0)return new(false,[],[],0,"No new non-Steam process matched the installation directory or configured process rules.");
        string? learnedRule=confidence>=0.85?NormalizeName(names[0]):null;
        if(settings is not null&&appId is not null&&learnedRule is not null)await settings.SetAsync($"Providers.Steam.ProcessRules.{appId}",learnedRule,cancellationToken).ConfigureAwait(false);
        return new(true,running,names,confidence,"A Steam game process was detected.",learnedRule);
    }
    private static bool Inside(string? root,string? candidate){if(string.IsNullOrWhiteSpace(root)||string.IsNullOrWhiteSpace(candidate))return false;try{string normalizedRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;return Path.GetFullPath(candidate).StartsWith(normalizedRoot,StringComparison.OrdinalIgnoreCase);}catch(Exception exception) when(exception is ArgumentException or NotSupportedException or PathTooLongException){return false;}}
    private static string NormalizeName(string name)=>Path.GetFileNameWithoutExtension(name.Trim());
}
