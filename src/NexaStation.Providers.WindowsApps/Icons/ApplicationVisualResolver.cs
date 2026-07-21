using System.Collections.Concurrent;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.Icons;

public sealed class ApplicationVisualResolver(IApplicationIconExtractor extractor) : IApplicationVisualResolver
{
    private readonly ConcurrentDictionary<string, ApplicationVisualMetadata> _metadata = new(StringComparer.Ordinal);

    public async Task<ApplicationVisualResult> ResolveAsync(ApplicationVisualRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        (ApplicationIconSourceType Type, string? Path, bool Shell)[] sources = IsCuratedMediaService(request)
            ?
            [
                (ApplicationIconSourceType.UserSelected, request.UserSelectedIconPath, false),
                (ApplicationIconSourceType.DiscoverCatalogue, request.CatalogueIconPath, false),
                (ApplicationIconSourceType.PackagedApplication, request.PackagedIconPath, false),
                (ApplicationIconSourceType.ShellLink, request.ShortcutPath, false),
                (ApplicationIconSourceType.Executable, request.ExecutablePath, false),
                (ApplicationIconSourceType.ProgressiveWebApplication, request.PwaIconPath, false),
                (ApplicationIconSourceType.AssociatedShell, request.ShellParsingPath, true),
            ]
            :
            [
                (ApplicationIconSourceType.UserSelected, request.UserSelectedIconPath, false),
                (ApplicationIconSourceType.PackagedApplication, request.PackagedIconPath, false),
                (ApplicationIconSourceType.ShellLink, request.ShortcutPath, false),
                (ApplicationIconSourceType.Executable, request.ExecutablePath, false),
                (ApplicationIconSourceType.ProgressiveWebApplication, request.PwaIconPath, false),
                (ApplicationIconSourceType.DiscoverCatalogue, request.CatalogueIconPath, false),
                (ApplicationIconSourceType.AssociatedShell, request.ShellParsingPath, true),
            ];
        List<string> failures = [];
        foreach ((ApplicationIconSourceType type, string? path, bool shell) in sources)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            ApplicationIconRequest iconRequest = shell
                ? new(request.Identity + '|' + type, AppUserModelId: request.AppUserModelId, DesiredSize: request.DesiredSize, ShellParsingPath: path)
                : type switch
                {
                    ApplicationIconSourceType.ShellLink => new(request.Identity + '|' + type, ShortcutPath: path, IconLocation: request.IconLocation, AppUserModelId: request.AppUserModelId, DesiredSize: request.DesiredSize),
                    ApplicationIconSourceType.Executable => new(request.Identity + '|' + type, ExecutablePath: path, IconLocation: request.IconLocation, AppUserModelId: request.AppUserModelId, DesiredSize: request.DesiredSize),
                    _ => new(request.Identity + '|' + type, IconLocation: path, AppUserModelId: request.AppUserModelId, DesiredSize: request.DesiredSize),
                };
            ExtractedApplicationIcon? icon = await extractor.ExtractAsync(iconRequest, cancellationToken).ConfigureAwait(false);
            if (icon is null) { failures.Add(type.ToString()); continue; }
            ApplicationVisualMetadata metadata = ReadMetadata(icon.CachePath, request.Identity, request.HighContrast);
            string background = request.UserBackgroundColor ?? metadata.Background;
            return new(icon.CachePath, type, icon.Width, icon.Height, metadata.HasTransparency, background, metadata.Foreground, false, null, ContentCardInitials.Create(request.DisplayName));
        }

        (string fallbackBackground, string fallbackForeground) = StableColors(request.Identity, request.HighContrast);
        return new(null, ApplicationIconSourceType.GeneratedInitial, null, null, true, request.UserBackgroundColor ?? fallbackBackground, fallbackForeground, true, failures.Count == 0 ? "No application icon source was supplied." : $"Icon extraction failed for: {string.Join(", ", failures)}.", ContentCardInitials.Create(request.DisplayName));
    }

    private ApplicationVisualMetadata ReadMetadata(string iconPath, string identity, bool highContrast)
    {
        string key = iconPath + '|' + highContrast;
        return _metadata.GetOrAdd(key, _ =>
        {
            string sidecar = iconPath + ".visual.json";
            try { if (File.Exists(sidecar)) { ApplicationVisualMetadata? cached = JsonSerializer.Deserialize<ApplicationVisualMetadata>(File.ReadAllText(sidecar)); if (cached is not null && cached.HighContrast == highContrast) return cached; } } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { }
            ApplicationVisualMetadata value = Inspect(iconPath, identity, highContrast);
            try { File.WriteAllText(sidecar, JsonSerializer.Serialize(value)); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            return value;
        });
    }

    private static ApplicationVisualMetadata Inspect(string path, string identity, bool highContrast)
    {
        if (highContrast) return new(true, "#FF202020", "#FFFFFFFF", true);
        try
        {
            using Bitmap bitmap = new(path);
            long red = 0, green = 0, blue = 0, count = 0; bool alpha = false;
            int step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 32);
            for (int y = 0; y < bitmap.Height; y += step) for (int x = 0; x < bitmap.Width; x += step)
            {
                Color pixel = bitmap.GetPixel(x, y); if (pixel.A < 250) alpha = true; if (pixel.A < 40) continue;
                red += pixel.R; green += pixel.G; blue += pixel.B; count++;
            }
            if (count == 0) { (string bg, string fg) = StableColors(identity, false); return new(alpha, bg, fg, false); }
            int r = Muted((int)(red / count)), g = Muted((int)(green / count)), b = Muted((int)(blue / count));
            string background = $"#FF{r:X2}{g:X2}{b:X2}"; double luminance = .2126 * r + .7152 * g + .0722 * b;
            return new(alpha, background, luminance > 142 ? "#FF10131A" : "#FFFFFFFF", false);
        }
        catch { (string bg, string fg) = StableColors(identity, false); return new(true, bg, fg, false); }
    }

    private static int Muted(int value) => Math.Clamp((int)(value * .58 + 30), 26, 142);
    private static bool IsCuratedMediaService(ApplicationVisualRequest request) =>
        request.CatalogueIconPath is not null && request.Category is "Streaming" or "Music";

    private static (string Background, string Foreground) StableColors(string identity, bool highContrast)
    {
        if (highContrast) return ("#FF202020", "#FFFFFFFF");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        int r = 34 + hash[0] % 58, g = 40 + hash[1] % 62, b = 55 + hash[2] % 68;
        return ($"#FF{r:X2}{g:X2}{b:X2}", "#FFFFFFFF");
    }

    private sealed record ApplicationVisualMetadata(bool HasTransparency, string Background, string Foreground, bool HighContrast);
}
