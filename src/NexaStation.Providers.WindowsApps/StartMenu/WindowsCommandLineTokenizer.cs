using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NexaStation.Providers.WindowsApps.StartMenu;

internal static partial class WindowsCommandLineTokenizer
{
    public static IReadOnlyList<string> Tokenize(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return [];
        nint argv = CommandLineToArgvW("nexastation.exe " + arguments, out int count);
        if (argv == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            string[] values = new string[Math.Max(0, count - 1)];
            for (int index = 1; index < count; index++) values[index - 1] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, index * nint.Size)) ?? string.Empty;
            return values;
        }
        finally { LocalFree(argv); }
    }

    [LibraryImport("shell32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)] private static partial nint CommandLineToArgvW(string commandLine, out int argumentCount);
    [LibraryImport("kernel32.dll")] private static partial nint LocalFree(nint memory);
}
