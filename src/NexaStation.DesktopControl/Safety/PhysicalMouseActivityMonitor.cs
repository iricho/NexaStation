using System.Runtime.InteropServices;
using NexaStation.Core;

namespace NexaStation.DesktopControl.Safety;

public sealed class PhysicalMouseActivityMonitor : IPhysicalMouseActivityMonitor
{
    private uint _lastControllerTick;
    private uint _pausedUntilTick;

    public bool ShouldPause(DateTime nowUtc)
    {
        _ = nowUtc;
        LASTINPUTINFO info = new() { Size = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return false;
        uint now = unchecked((uint)Environment.TickCount);
        if (info.Time != 0 && info.Time != _lastControllerTick && unchecked(now - info.Time) < 120)
            _pausedUntilTick = unchecked(now + 2000);
        return unchecked((int)(_pausedUntilTick - now)) > 0;
    }

    public void RecordControllerMovement(DateTime atUtc)
    {
        _ = atUtc;
        _lastControllerTick = unchecked((uint)Environment.TickCount);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO inputInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO { public uint Size; public uint Time; }
}
