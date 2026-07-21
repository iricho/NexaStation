using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.StartMenu;

public sealed class ShellLinkReader : IShellLinkReader
{
    public async Task<ShellLinkReadResult> ReadAsync(string shortcutPath, CancellationToken cancellationToken = default)
    {
        string path = Path.GetFullPath(shortcutPath);
        if (!File.Exists(path)) return new(false, path, Error: "Shortcut is missing.");
        try
        {
            return await Task.Run(() => Read(path), cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException) { return new(false, path, Error: "Shortcut resolution timed out."); }
        catch (Exception exception) when (exception is COMException or IOException or UnauthorizedAccessException or ArgumentException)
        { return new(false, path, Error: exception.Message); }
    }

    private static ShellLinkReadResult Read(string path)
    {
        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return ReadUrl(path);
        object comObject = new ShellLinkComObject();
        try
        {
            IShellLinkW link = (IShellLinkW)comObject;
            ((IPersistFile)link).Load(path, 0);
            StringBuilder target = new(32768), args = new(32768), working = new(32768), description = new(4096), icon = new(32768);
            WIN32_FIND_DATAW data = default;
            link.GetPath(target, target.Capacity, ref data, 4);
            link.GetArguments(args, args.Capacity);
            link.GetWorkingDirectory(working, working.Capacity);
            link.GetDescription(description, description.Capacity);
            link.GetIconLocation(icon, icon.Capacity, out int iconIndex);
            link.GetShowCmd(out int show);
            string? rawTarget = Null(target); string? expanded = rawTarget is null ? null : Environment.ExpandEnvironmentVariables(rawTarget);
            string? rawArgs = Null(args);
            return new(true, path, Path.GetFileNameWithoutExtension(path), expanded, WindowsCommandLineTokenizer.Tokenize(rawArgs), rawArgs, Expand(working), Null(description), Null(icon) is string iconPath ? $"{Environment.ExpandEnvironmentVariables(iconPath)},{iconIndex}" : null, RunState: show);
        }
        finally { if (Marshal.IsComObject(comObject)) Marshal.FinalReleaseComObject(comObject); }
    }

    private static ShellLinkReadResult ReadUrl(string path)
    {
        string? url = null, icon = null;
        foreach (string line in File.ReadLines(path))
        {
            int split = line.IndexOf('='); if (split <= 0) continue;
            string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (key.Equals("URL", StringComparison.OrdinalIgnoreCase)) url = value;
            if (key.Equals("IconFile", StringComparison.OrdinalIgnoreCase)) icon = Environment.ExpandEnvironmentVariables(value);
        }
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out _)) return new(false, path, Error: "The internet shortcut has no valid URI.");
        return new(true, path, Path.GetFileNameWithoutExtension(path), Arguments: [], RawArguments: null, IconLocation: icon, ShellItemIdentity: url);
    }

    private static string? Null(StringBuilder value) => value.Length == 0 ? null : value.ToString();
    private static string? Expand(StringBuilder value) => Null(value) is string text ? Environment.ExpandEnvironmentVariables(text) : null;

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private sealed class ShellLinkComObject { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, ref WIN32_FIND_DATAW data, uint flags);
        void GetIDList(out nint itemIdList); void SetIDList(nint itemIdList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey); void SetHotkey(short hotkey); void GetShowCmd(out int showCommand); void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex); void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRelative, uint reserved); void Resolve(nint window, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WIN32_FIND_DATAW { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint SizeHigh, SizeLow, Reserved0, Reserved1; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string Alternate; }
}
