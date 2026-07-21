using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.DesktopControl.Safety;

public sealed class WindowsIntegrityGuard : IDesktopControlSafetyGuard
{
    public DesktopControlSafetyResult CanControl(int? processId)
    {
        if (processId is null)
            return new(true, "No target process was available for integrity comparison.");

        try
        {
            bool currentElevated = IsElevated(Process.GetCurrentProcess());
            using Process target = Process.GetProcessById(processId.Value);
            bool targetElevated = IsElevated(target);
            return targetElevated && !currentElevated
                ? new(false, "Desktop control is disabled because the target application is elevated.")
                : new(true, "The target integrity level is compatible.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return new(false, "Desktop control could not safely verify the target process integrity level.");
        }
    }

    private static bool IsElevated(Process process)
    {
        if (!OpenProcessToken(process.SafeHandle, 0x0008, out SafeAccessTokenHandle token))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        {
            int size = Marshal.SizeOf<TOKEN_ELEVATION>();
            nint buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 20, buffer, size, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return Marshal.PtrToStructure<TOKEN_ELEVATION>(buffer).TokenIsElevated != 0;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle tokenHandle, int tokenInformationClass, nint tokenInformation, int tokenInformationLength, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION { public int TokenIsElevated; }
}
