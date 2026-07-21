using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Core;
using NexaStation.Recovery;

namespace NexaStation.Presentation.Collections;

public sealed class CollectionMosaicService : ICollectionMosaicService
{
    private readonly string _cacheRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<CollectionMosaicResult>>> _inflight = new(StringComparer.Ordinal);
    public CollectionMosaicService() : this(new NexaStationPaths()) { }
    public CollectionMosaicService(INexaStationPaths paths) : this(paths.CollectionMosaicCache) { }
    public CollectionMosaicService(string cacheRoot) => _cacheRoot = Path.GetFullPath(cacheRoot);

    public Task<CollectionMosaicResult> ResolveAsync(CollectionMosaicRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        string[] paths = request.VisualPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
        string fingerprint = request.CollectionId + '|' + request.ContentsUpdatedAtUtc.Ticks + '|' + string.Join('|', paths.Select(path => { FileInfo info = new(path); return path + ':' + info.Length + ':' + info.LastWriteTimeUtc.Ticks; }));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint))).ToLowerInvariant();
        if (paths.Length == 0) return Task.FromResult(new CollectionMosaicResult(null, 0, true, key));
        Lazy<Task<CollectionMosaicResult>> lazy = _inflight.GetOrAdd(key, _ => new(() => Task.Run(() => Render(request, paths, key, cancellationToken), cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));
        return AwaitAsync(key, lazy, cancellationToken);
    }

    private async Task<CollectionMosaicResult> AwaitAsync(string key, Lazy<Task<CollectionMosaicResult>> lazy, CancellationToken token)
    {
        try { return await lazy.Value.WaitAsync(token).ConfigureAwait(false); }
        finally { if (lazy.IsValueCreated && lazy.Value.IsCompleted) _inflight.TryRemove(new(key, lazy)); }
    }

    private CollectionMosaicResult Render(CollectionMosaicRequest request, string[] paths, string key, CancellationToken token)
    {
        Directory.CreateDirectory(_cacheRoot); int size = Math.Clamp(request.Size, 192, 512); string output = Path.Combine(_cacheRoot, key + $"-{size}.png");
        if (File.Exists(output)) return new(output, paths.Length, false, key);
        using Bitmap canvas = new(size, size, PixelFormat.Format32bppArgb); using Graphics graphics = Graphics.FromImage(canvas); graphics.Clear(Color.FromArgb(255, 24, 29, 43)); graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.CompositingQuality = CompositingQuality.HighQuality;
        int gap = Math.Max(2, size / 96), cell = (size - gap) / 2;
        for (int index = 0; index < 4; index++)
        {
            token.ThrowIfCancellationRequested(); Rectangle destination = new((index % 2) * (cell + gap), (index / 2) * (cell + gap), cell, cell);
            if (index >= paths.Length) { using SolidBrush fill = new(Color.FromArgb(255, 31, 39, 57)); graphics.FillRectangle(fill, destination); continue; }
            try { using Image image = Image.FromFile(paths[index]); Rectangle source = Crop(image.Width, image.Height); graphics.DrawImage(image, destination, source, GraphicsUnit.Pixel); }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or OutOfMemoryException) { using SolidBrush fill = new(Color.FromArgb(255, 44, 53, 75)); graphics.FillRectangle(fill, destination); }
        }
        canvas.Save(output, ImageFormat.Png); return new(output, paths.Length, false, key);
    }
    private static Rectangle Crop(int width, int height) { int side = Math.Min(width, height); return new((width - side) / 2, (height - side) / 2, side, side); }
}
