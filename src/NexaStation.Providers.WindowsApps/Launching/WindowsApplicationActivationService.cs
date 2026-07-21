using System.Diagnostics;
using System.Runtime.InteropServices;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.Launching;

public sealed class PackagedApplicationActivator : IPackagedApplicationActivator
{
    public Task<PackagedApplicationActivationResult> ActivateAsync(string appUserModelId, string? arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(appUserModelId) || !appUserModelId.Contains('!', StringComparison.Ordinal)) return Task.FromResult(new PackagedApplicationActivationResult(false, null, "The AppUserModelId is invalid."));
        try
        {
            IApplicationActivationManager manager = (IApplicationActivationManager)(object)new ApplicationActivationManager();
            int result = manager.ActivateApplication(appUserModelId, arguments, 0, out uint processId);
            return Task.FromResult(result >= 0 ? new PackagedApplicationActivationResult(true, processId == 0 ? null : checked((int)processId), "Windows accepted the packaged-application activation request.") : new PackagedApplicationActivationResult(false, null, "Windows rejected packaged-application activation.", result));
        }
        catch (COMException exception) { return Task.FromResult(new PackagedApplicationActivationResult(false, null, exception.Message, exception.HResult)); }
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] private sealed class ApplicationActivationManager { }
    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId, [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
        [PreserveSig] int ActivateForFile(nint appUserModelId, nint itemArray, [MarshalAs(UnmanagedType.LPWStr)] string? verb, out uint processId);
        [PreserveSig] int ActivateForProtocol(nint appUserModelId, nint itemArray, out uint processId);
    }
}

public sealed class WindowsApplicationActivationService(IPackagedApplicationActivator packaged) : IWindowsApplicationActivationService
{
    public async Task<WindowsActivationResult> ActivateAsync(WindowsApplicationLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.LaunchMethod == WindowsLaunchMethod.AppUserModelId)
        {
            PackagedApplicationActivationResult result = await packaged.ActivateAsync(request.AppUserModelId ?? string.Empty, request.Arguments is { Count: > 0 } ? string.Join(' ', request.Arguments.Select(Quote)) : null, cancellationToken).ConfigureAwait(false);
            return new(result.Succeeded, result.ProcessIdHint, result.Message, result.ErrorCode);
        }
        try
        {
            ProcessStartInfo info = request.LaunchMethod switch
            {
                WindowsLaunchMethod.ShellLink => new(request.ShortcutPath!) { UseShellExecute = true },
                WindowsLaunchMethod.Uri => new(request.Uri!.AbsoluteUri) { UseShellExecute = true },
                WindowsLaunchMethod.ShellItem => new(request.ShellParsingPath!) { UseShellExecute = true },
                _ => new(request.ExecutablePath!) { UseShellExecute = false, WorkingDirectory = request.WorkingDirectory ?? string.Empty },
            };
            if (!info.UseShellExecute && request.Arguments is not null) foreach (string argument in request.Arguments) info.ArgumentList.Add(argument);
            Process? process = Process.Start(info);
            return process is null ? new(false, null, "Windows did not start the application.") : new(true, process.Id, "Application started.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException) { return new(false, null, exception.Message); }
    }

    private static string Quote(string value) => value.Any(char.IsWhiteSpace) ? '"' + value.Replace("\"", "\\\"", StringComparison.Ordinal) + '"' : value;
}
