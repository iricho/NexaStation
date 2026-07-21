using System.ComponentModel;
using System.Runtime.InteropServices;
using NexaStation.SystemIntegration.Models;
using NexaStation.SystemIntegration.Services;

namespace NexaStation.SystemIntegration.Power;

public sealed class WindowsSystemPowerService:ISystemPowerService
{
    public Task<SystemActionAvailability> GetAvailabilityAsync(SystemPowerAction action,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(new SystemActionAvailability(OperatingSystem.IsWindows(),OperatingSystem.IsWindows()?null:"Windows system actions are unavailable."));}
    public async Task<SystemPowerResult> ExecuteAsync(SystemPowerAction action,CancellationToken cancellationToken=default){SystemActionAvailability available=await GetAvailabilityAsync(action,cancellationToken);if(!available.Available)return new(false,available.UnavailableReason);try{bool succeeded=action switch{SystemPowerAction.Sleep=>SetSuspendState(false,false,false),SystemPowerAction.SignOut=>ExitWindowsEx(0,0),SystemPowerAction.Restart=>InitiateSystemShutdownEx(null,null,0,false,true,0x80040002),SystemPowerAction.ShutDown=>InitiateSystemShutdownEx(null,null,0,false,false,0x80040002),_=>false};return succeeded?new(true):new(false,new Win32Exception(Marshal.GetLastWin32Error()).Message);}catch(Exception ex) when(ex is Win32Exception or ExternalException){return new(false,ex.Message);}}
    [DllImport("powrprof.dll",SetLastError=true)]private static extern bool SetSuspendState(bool hibernate,bool forceCritical,bool disableWakeEvent);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool ExitWindowsEx(uint flags,uint reason);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool InitiateSystemShutdownEx(string? machineName,string? message,uint timeout,bool forceAppsClosed,bool rebootAfterShutdown,uint reason);
}
