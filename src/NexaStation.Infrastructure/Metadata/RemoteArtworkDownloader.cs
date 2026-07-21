using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

public sealed class RemoteArtworkDownloader : IRemoteArtworkDownloader, IDisposable
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public RemoteArtworkDownloader()
    {
        _http = new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
        _ownsClient = true;
    }

    public RemoteArtworkDownloader(HttpClient httpClient) { _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient)); }

    public async Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(asset.DownloadUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return Failed("non_https", "Artwork must use HTTPS.");
        Uri current = asset.DownloadUri;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout ?? TimeSpan.FromSeconds(30));
        for (int redirect = 0; redirect <= request.RedirectLimit; redirect++)
        {
            using HttpRequestMessage message = new(HttpMethod.Get, current);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/webp"));
            using HttpResponseMessage response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect == request.RedirectLimit || response.Headers.Location is null) return Failed("redirect_limit", "The artwork response exceeded the redirect limit.");
                current = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                if (!string.Equals(current.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return Failed("unsafe_redirect", "Artwork redirected to a non-HTTPS address.");
                continue;
            }
            if (!response.IsSuccessStatusCode) return Failed($"http_{(int)response.StatusCode}", "The artwork provider returned an error.");
            string? mime = response.Content.Headers.ContentType?.MediaType;
            if (mime is null || !AllowedMimeTypes.Contains(mime)) return Failed("invalid_mime", "The response is not a supported image type.");
            if (response.Content.Headers.ContentLength is long length && (length <= 0 || length > request.MaximumBytes)) return Failed("oversized", "The artwork file exceeds the configured size limit.");
            await using Stream source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return await StoreAsync(source, mime, asset.Type, request, timeout.Token).ConfigureAwait(false);
        }
        return Failed("redirect_limit", "The artwork response exceeded the redirect limit.");
    }

    public async Task<ArtworkDownloadResult> ImportLocalAsync(string sourcePath, ArtworkKind assetType, ArtworkDownloadRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return Failed("missing_source", "The local artwork source is unavailable.");
        await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length <= 0 || source.Length > request.MaximumBytes) return Failed("oversized", "The artwork file exceeds the configured size limit.");
        string mime = Path.GetExtension(sourcePath).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", _ => string.Empty };
        if (mime.Length == 0) return Failed("invalid_mime", "The local file is not a supported image type.");
        return await StoreAsync(source, mime, assetType, request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ArtworkDownloadResult> StoreAsync(Stream source, string declaredMime, ArtworkKind type, ArtworkDownloadRequest request, CancellationToken cancellationToken)
    {
        string destinationRoot = Path.GetFullPath(request.DestinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        string temporary = Path.Combine(destinationRoot, $".{Guid.NewGuid():N}.download");
        try
        {
            await using (FileStream target = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                byte[] buffer = new byte[64 * 1024]; long total = 0;
                while (true)
                {
                    int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); if (read == 0) break;
                    total += read; if (total > request.MaximumBytes) return Failed("oversized", "The artwork file exceeds the configured size limit.");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                target.Flush(true);
            }
            ImageFileInspection inspection = ImageFileInspector.Inspect(temporary);
            if (!inspection.Valid || !string.Equals(inspection.MimeType, declaredMime, StringComparison.OrdinalIgnoreCase)) return Failed(inspection.FailureCode ?? "invalid_image", "The downloaded content did not match a supported image signature.");
            if (inspection.Width > request.MaximumDimension || inspection.Height > request.MaximumDimension) return Failed("excessive_dimensions", "The artwork dimensions exceed the configured limit.");
            string hash;
            await using (FileStream file = File.OpenRead(temporary)) hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            string directory = Path.Combine(destinationRoot, Folder(type)); Directory.CreateDirectory(directory);
            string final = Path.Combine(directory, $"{Safe(request.LibraryItemId)}-{(int)type}-{hash[..24]}{inspection.Extension}");
            if (File.Exists(final)) File.Delete(temporary); else File.Move(temporary, final);
            return new(true, final, hash, new FileInfo(final).Length, inspection.Width, inspection.Height);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Failed("storage_failure", "The artwork could not be stored safely."); }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } }
    }

    private static bool IsRedirect(HttpStatusCode code) => code is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    private static string Folder(ArtworkKind type) => type switch { ArtworkKind.CoverPortrait or ArtworkKind.BoxFront or ArtworkKind.BoxBack => "covers", ArtworkKind.GridWide => "grids", ArtworkKind.HeroBackground => "heroes", ArtworkKind.Logo => "logos", ArtworkKind.Icon => "icons", _ => "screenshots" };
    private static string Safe(string value) { string safe = new(value.Where(char.IsLetterOrDigit).ToArray()); return safe.Length == 0 ? "item" : safe; }
    private static ArtworkDownloadResult Failed(string code, string message) => new(false, null, null, 0, null, null, code, message);
    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}

internal sealed record ImageFileInspection(bool Valid, string? MimeType, string Extension, int Width, int Height, string? FailureCode);

internal static class ImageFileInspector
{
    public static ImageFileInspection Inspect(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path); Span<byte> header = stackalloc byte[32]; int read = stream.Read(header);
            if (read >= 24 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                return HasPngEnd(stream) ? Dimensions("image/png", ".png", BinaryPrimitives.ReadInt32BigEndian(header[16..20]), BinaryPrimitives.ReadInt32BigEndian(header[20..24])) : new(false, "image/png", ".png", 0, 0, "corrupt_image");
            if (read >= 16 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
                return WebP(header, read);
            if (read >= 4 && header[0] == 0xff && header[1] == 0xd8)
                return HasJpegEnd(stream) ? Jpeg(stream) : new(false, "image/jpeg", ".jpg", 0, 0, "corrupt_image");
            return new(false, null, string.Empty, 0, 0, "invalid_signature");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return new(false, null, string.Empty, 0, 0, "decode_failure"); }
    }
    private static ImageFileInspection WebP(ReadOnlySpan<byte> h, int read)
    {
        if (read >= 30 && h[12..16].SequenceEqual("VP8X"u8)) return Dimensions("image/webp", ".webp", 1 + h[24] + (h[25] << 8) + (h[26] << 16), 1 + h[27] + (h[28] << 8) + (h[29] << 16));
        return new(false, "image/webp", ".webp", 0, 0, "unsupported_webp_header");
    }
    private static bool HasPngEnd(FileStream stream)
    {
        if (stream.Length < 12) return false; Span<byte> end = stackalloc byte[12]; stream.Position = stream.Length - end.Length; return stream.Read(end) == end.Length && end[4..8].SequenceEqual("IEND"u8);
    }
    private static bool HasJpegEnd(FileStream stream)
    {
        if (stream.Length < 4) return false; stream.Position = stream.Length - 2; return stream.ReadByte() == 0xff && stream.ReadByte() == 0xd9;
    }
    private static ImageFileInspection Jpeg(FileStream stream)
    {
        stream.Position = 2; Span<byte> marker = stackalloc byte[4];
        while (stream.Read(marker[..2]) == 2)
        {
            if (marker[0] != 0xff) return new(false, "image/jpeg", ".jpg", 0, 0, "corrupt_image");
            byte code = marker[1]; if (code is 0xd8 or 0xd9) continue;
            if (stream.Read(marker[2..4]) != 2) break; int length = BinaryPrimitives.ReadUInt16BigEndian(marker[2..4]); if (length < 2) break;
            if (code is >= 0xc0 and <= 0xc3 or >= 0xc5 and <= 0xc7 or >= 0xc9 and <= 0xcb or >= 0xcd and <= 0xcf)
            {
                Span<byte> dimensions = stackalloc byte[5]; if (stream.Read(dimensions) != 5) break;
                return Dimensions("image/jpeg", ".jpg", BinaryPrimitives.ReadUInt16BigEndian(dimensions[3..5]), BinaryPrimitives.ReadUInt16BigEndian(dimensions[1..3]));
            }
            stream.Seek(length - 2, SeekOrigin.Current);
        }
        return new(false, "image/jpeg", ".jpg", 0, 0, "corrupt_image");
    }
    private static ImageFileInspection Dimensions(string mime, string extension, int width, int height) => width > 0 && height > 0 ? new(true, mime, extension, width, height, null) : new(false, mime, extension, width, height, "invalid_dimensions");
}
