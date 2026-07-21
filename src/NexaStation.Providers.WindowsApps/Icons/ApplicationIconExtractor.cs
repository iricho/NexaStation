using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Recovery;

namespace NexaStation.Providers.WindowsApps.Icons;

public sealed class ApplicationIconExtractor : IApplicationIconExtractor
{
    private readonly string _cacheRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ExtractedApplicationIcon?>>> _inflight = new(StringComparer.Ordinal);

    public ApplicationIconExtractor() : this(new NexaStationPaths()) { }
    public ApplicationIconExtractor(INexaStationPaths paths) : this(paths.IconCache) { }
    public ApplicationIconExtractor(string cacheRoot) => _cacheRoot = Path.GetFullPath(cacheRoot);

    public Task<ExtractedApplicationIcon?> ExtractAsync(ApplicationIconRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        string? source = Source(request);
        if (source is null) return Task.FromResult<ExtractedApplicationIcon?>(null);
        int size = SizeBucket(request.DesiredSize);
        FileInfo? info = File.Exists(source) ? new FileInfo(source) : null;
        string fingerprint = string.Join('|', request.Identity, source, info?.Length ?? 0, info?.LastWriteTimeUtc.Ticks ?? 0, size);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();
        Lazy<Task<ExtractedApplicationIcon?>> lazy = _inflight.GetOrAdd(key, _ => new(() => ExtractCoreAsync(request, source, info, size, key), LazyThreadSafetyMode.ExecutionAndPublication));
        return AwaitAndReleaseAsync(key, lazy, cancellationToken);
    }

    private async Task<ExtractedApplicationIcon?> AwaitAndReleaseAsync(string key, Lazy<Task<ExtractedApplicationIcon?>> lazy, CancellationToken cancellationToken)
    {
        try { return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false); }
        finally { if (lazy.IsValueCreated && lazy.Value.IsCompleted) _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<ExtractedApplicationIcon?>>>(key, lazy)); }
    }

    private Task<ExtractedApplicationIcon?> ExtractCoreAsync(ApplicationIconRequest request, string source, FileInfo? info, int size, string key)
    {
        Directory.CreateDirectory(_cacheRoot);
        string output = Path.Combine(_cacheRoot, key + $"-{size}.png");
        if (File.Exists(output) && !TryReadDimensions(output, out _, out _)) TryDelete(output);
        if (!File.Exists(output) && !WriteRaster(source, output, size) && !WriteResourceIcon(source, output, size) && !WriteShellIcon(source, output)) return Task.FromResult<ExtractedApplicationIcon?>(null);
        if (!TryReadDimensions(output, out int width, out int height)) { TryDelete(output); return Task.FromResult<ExtractedApplicationIcon?>(null); }
        return Task.FromResult<ExtractedApplicationIcon?>(new(output, width, height, request.Identity, info?.LastWriteTimeUtc ?? DateTime.MinValue));
    }

    private static int SizeBucket(int desired) => desired <= 80 ? 64 : desired <= 192 ? 128 : 256;

    private static bool WriteRaster(string source, string output, int desired)
    {
        if (Path.GetExtension(source).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".ico")) return false;
        try
        {
            using Image image = Image.FromFile(source);
            int size = Math.Clamp(desired, 32, 512);
            using Bitmap canvas = new(size, size, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(canvas);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            double scale = Math.Min((double)size / image.Width, (double)size / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            graphics.DrawImage(image, (size - width) / 2, (size - height) / 2, width, height);
            canvas.Save(output, ImageFormat.Png);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException) { return false; }
    }

    private static bool WriteResourceIcon(string source, string output, int desired)
    {
        if (!File.Exists(source)) return false;
        nint handle = 0;
        try
        {
            uint extracted = PrivateExtractIcons(source, 0, Math.Clamp(desired, 32, 512), Math.Clamp(desired, 32, 512), out handle, out _, 1, 0);
            if (extracted == 0 || handle == 0) return false;
            using Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            using Bitmap bitmap = icon.ToBitmap();
            bitmap.Save(output, ImageFormat.Png);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException or UnauthorizedAccessException) { return false; }
        finally { if (handle != 0) DestroyIcon(handle); }
    }

    private static bool WriteShellIcon(string source, string output)
    {
        SHFILEINFO info = default;
        nint pidl = 0;
        try
        {
            nint result;
            if (!File.Exists(source) && SHParseDisplayName(source, 0, out pidl, 0, out _) == 0 && pidl != 0)
                result = SHGetFileInfoPidl(pidl, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_PIDL);
            else
                result = SHGetFileInfoPath(source, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (result == 0 || info.hIcon == 0) return false;
            using Icon icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
            using Bitmap bitmap = icon.ToBitmap();
            bitmap.Save(output, ImageFormat.Png);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ExternalException or IOException or UnauthorizedAccessException) { return false; }
        finally { if (info.hIcon != 0) DestroyIcon(info.hIcon); if (pidl != 0) Marshal.FreeCoTaskMem(pidl); }
    }

    private static string? Source(ApplicationIconRequest request)
    {
        string? icon = request.IconLocation?.Split(',')[0].Trim(' ', '"');
        if (!string.IsNullOrWhiteSpace(icon)) { icon = Environment.ExpandEnvironmentVariables(icon); if (File.Exists(icon)) return icon; }
        if (!string.IsNullOrWhiteSpace(request.ExecutablePath) && File.Exists(request.ExecutablePath)) return request.ExecutablePath;
        if (!string.IsNullOrWhiteSpace(request.ShortcutPath) && File.Exists(request.ShortcutPath)) return request.ShortcutPath;
        if (!string.IsNullOrWhiteSpace(request.ShellParsingPath)) return request.ShellParsingPath;
        return null;
    }

    private static bool TryReadDimensions(string path, out int width, out int height)
    {
        try { using Image image = Image.FromFile(path); width = image.Width; height = image.Height; return width > 0 && height > 0; }
        catch { width = height = 0; return false; }
    }
    private static void TryDelete(string path) { try { File.Delete(path); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { } }

    private const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0, SHGFI_PIDL = 0x8;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SHFILEINFO { public nint hIcon; public int iIcon; public uint dwAttributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint PrivateExtractIcons(string fileName, int iconIndex, int cxIcon, int cyIcon, out nint icon, out uint iconId, uint iconCount, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(nint icon);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")] private static extern nint SHGetFileInfoPath(string pszPath, uint attributes, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")] private static extern nint SHGetFileInfoPidl(nint pidl, uint attributes, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint bindingContext, out nint pidl, uint attributes, out uint attributesOut);
}
