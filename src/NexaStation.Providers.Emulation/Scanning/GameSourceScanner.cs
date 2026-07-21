using System.Collections.Concurrent;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.Providers.Emulation.Platforms;

namespace NexaStation.Providers.Emulation.Scanning;

public sealed class GameSourceScanner(IGameIdentifierReader identifier) : IGameSourceScanner
{
    private const int MaxParallelIdentification = 8;

    public async Task<GameSourceScanResult> ScanAsync(GameSource source, IProgress<GameSourceScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow;
        List<string> warnings = [];
        List<string> errors = [];
        if (!source.Enabled) return new(source, started, DateTime.UtcNow, [], ["This game source is disabled."], [], false, true);
        if (!Directory.Exists(source.FolderPath)) return new(source, started, DateTime.UtcNow, [], [], ["The configured game folder is unavailable."], false, false);

        EmulatorPlatform? platform = EmulatorPlatformCatalog.Find(source.PlatformId);
        if (platform is null) return new(source, started, DateTime.UtcNow, [], [], [$"Platform '{source.PlatformId}' is not configured."], false, false);
        HashSet<string> extensions = platform.DefaultExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ConcurrentBag<DiscoveredGame> games = [];
        int enumerated = 0;

        try
        {
            EnumerationOptions options = new()
            {
                RecurseSubdirectories = source.IncludeSubfolders,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = source.ScanHiddenFiles ? FileAttributes.System : FileAttributes.Hidden | FileAttributes.System,
            };
            List<string> files = [];
            foreach (string path in Directory.EnumerateFiles(source.FolderPath, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                enumerated++;
                if (!source.FollowSymbolicLinks && IsSymbolicLink(path)) continue;
                if (!extensions.Contains(Path.GetExtension(path))) continue;
                files.Add(path);
                if ((enumerated & 127) == 0) progress?.Report(new(enumerated, files.Count, Path.GetDirectoryName(path)));
            }

            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelIdentification, CancellationToken = cancellationToken }, async (path, ct) =>
            {
                try
                {
                    FileInfo info = new(path);
                    GameIdentificationResult identified = await identifier.IdentifyAsync(new(source.PlatformId, path, info.Extension), ct).ConfigureAwait(false);
                    games.Add(new(source.PlatformId, path, Normalize(path), identified.SuggestedTitle, identified.OriginalFileName, info.Extension.ToLowerInvariant(), info.Length, info.LastWriteTimeUtc, identified));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    lock (warnings) warnings.Add($"Skipped one unreadable game file: {exception.Message}");
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new(source, started, DateTime.UtcNow, games.OrderBy(game => game.SuggestedTitle).ToArray(), warnings, errors, true, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            errors.Add(exception.Message);
            return new(source, started, DateTime.UtcNow, games.ToArray(), warnings, errors, false, false);
        }

        progress?.Report(new(enumerated, games.Count, null));
        return new(source, started, DateTime.UtcNow, games.OrderBy(game => game.SuggestedTitle, StringComparer.OrdinalIgnoreCase).ThenBy(game => game.GamePath, StringComparer.OrdinalIgnoreCase).ToArray(), warnings, errors, false, true);
    }

    private static bool IsSymbolicLink(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return true; }
    }

    public static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
}
