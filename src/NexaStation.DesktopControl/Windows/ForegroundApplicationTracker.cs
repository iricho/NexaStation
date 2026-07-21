using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.DesktopControl.Windows;

public sealed class ForegroundApplicationTracker : IForegroundApplicationTracker
{
    public async Task<ForegroundApplicationDetectionResult> DetectAsync(ForegroundApplicationDetectionRequest request, CancellationToken cancellationToken = default)
    {
        DateTime deadline = DateTime.UtcNow + request.Timeout;
        while (DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ForegroundApplicationDetectionResult? result = TryDetect(request);
            if (result is not null) return result;
            await Task.Delay(request.PollInterval, cancellationToken).ConfigureAwait(false);
        }
        return new(false, null, null, request.ExpectedAppUserModelId, request.ExpectedWindowClass, null, 0, "No matching foreground application was detected before the timeout.");
    }

    private static ForegroundApplicationDetectionResult? TryDetect(ForegroundApplicationDetectionRequest request)
    {
        nint window = GetForegroundWindow();
        if (window == 0) return null;
        _ = GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0) return null;
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string? path = null;
            try { path = process.MainModule?.FileName; } catch (Exception exception) when (exception is Win32Exception or InvalidOperationException) { }
            string windowClass = ReadWindowText(window, GetClassName);
            string title = ReadWindowText(window, GetWindowText);
            bool pidMatch = request.ProcessIdHint == processId;
            bool pathMatch = !string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(request.ExpectedExecutablePath) && string.Equals(Path.GetFullPath(path), Path.GetFullPath(request.ExpectedExecutablePath), StringComparison.OrdinalIgnoreCase);
            bool classMatch = !string.IsNullOrWhiteSpace(request.ExpectedWindowClass) && string.Equals(windowClass, request.ExpectedWindowClass, StringComparison.OrdinalIgnoreCase);
            if (!pidMatch && !pathMatch && !classMatch) return null;
            double confidence = pidMatch ? 1 : pathMatch ? 0.9 : 0.7;
            return new(true, (int)processId, path, request.ExpectedAppUserModelId, windowClass, title, confidence, "A matching foreground application was detected.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception) { return null; }
    }

    private static string ReadWindowText(nint window, Func<nint, char[], int, int> reader)
    {
        char[] buffer = new char[512];
        int length = reader(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, [Out] char[] className, int maximumCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, [Out] char[] text, int maximumCount);
}
