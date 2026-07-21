using System.Runtime.InteropServices;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure;

public sealed class WindowsShortcutResolver : IWindowsShortcutResolver
{
    public Task<ShortcutResolutionResult> ResolveAsync(string shortcutPath,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(!File.Exists(shortcutPath))return Task.FromResult(new ShortcutResolutionResult(false,false,null,null,null,"Shortcut file was not found."));
        try
        {
            Type? shellType=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(shellType!)!;dynamic shortcut=shell.CreateShortcut(Path.GetFullPath(shortcutPath));
            string? target=Environment.ExpandEnvironmentVariables((string)shortcut.TargetPath);string? arguments=shortcut.Arguments;string? working=shortcut.WorkingDirectory;
            Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(shell);
            return Task.FromResult(new ShortcutResolutionResult(!string.IsNullOrWhiteSpace(target),string.IsNullOrWhiteSpace(target),target,arguments,working,null));
        }
        catch(Exception ex) when(ex is COMException or InvalidCastException){return Task.FromResult(new ShortcutResolutionResult(false,true,null,null,null,ex.Message));}
    }
}
