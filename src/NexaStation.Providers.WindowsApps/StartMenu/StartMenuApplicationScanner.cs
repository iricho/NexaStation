using System.Collections.Concurrent;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.WindowsApps.StartMenu;

public sealed class StartMenuApplicationScanner(IStartMenuKnownFolderResolver folders, IShellLinkReader links) : IStartMenuApplicationScanner
{
    public async Task<WindowsApplicationScanResult> ScanAsync(StartMenuScanRequest request, CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow; ConcurrentBag<WindowsApplicationCandidate> candidates = []; ConcurrentBag<string> warnings = [];
        List<(string SourceId, string Root, string Path)> paths = [];
        foreach ((string sourceId, string root) in folders.Resolve(request.ScanCurrentUser, request.ScanCommon))
        {
            if (!Directory.Exists(root)) { warnings.Add($"Start Menu source unavailable: {sourceId}."); continue; }
            try
            {
                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsSupported)) paths.Add((sourceId, root, path));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { warnings.Add($"Could not enumerate {sourceId}: {exception.Message}"); }
        }
        int processed = 0; request.Progress?.Report(new(paths.Count, 0));
        try
        {
            await Parallel.ForEachAsync(paths, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (entry, token) =>
            {
                ShellLinkReadResult result = await links.ReadAsync(entry.Path, token).ConfigureAwait(false);
                if (!result.Success) warnings.Add($"{Path.GetFileName(entry.Path)}: {result.Error}");
                else candidates.Add(ToCandidate(entry.SourceId, entry.Root, result));
                int count = Interlocked.Increment(ref processed); if (count == paths.Count || count % 10 == 0) request.Progress?.Report(new(paths.Count, count, Path.GetFileName(entry.Path)));
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new("windows-start-menu", started, DateTime.UtcNow, candidates.OrderBy(value => value.DisplayName).ToArray(), warnings.ToArray(), [], false, true); }
        return new("windows-start-menu", started, DateTime.UtcNow, candidates.OrderBy(value => value.DisplayName).ToArray(), warnings.ToArray(), []);
    }

    private static WindowsApplicationCandidate ToCandidate(string sourceId, string root, ShellLinkReadResult value)
    {
        string? folder = Path.GetDirectoryName(value.ShortcutPath) is string directory ? Path.GetRelativePath(root, directory) : null;
        bool url = value.ShortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase);
        Uri? uri = url && Uri.TryCreate(value.ShellItemIdentity, UriKind.Absolute, out Uri? parsed) ? parsed : null;
        string providerItem = Path.GetFullPath(value.ShortcutPath).ToUpperInvariant();
        return new()
        {
            ProviderId = "windows-start-menu", ProviderItemId = providerItem, DisplayName = value.DisplayName ?? Path.GetFileNameWithoutExtension(value.ShortcutPath),
            SourceType = url ? WindowsApplicationSourceType.UriShortcut : WindowsApplicationSourceType.ShellLink,
            LaunchMethod = url ? WindowsLaunchMethod.Uri : WindowsLaunchMethod.ShellLink,
            ShortcutPath = value.ShortcutPath, TargetPath = value.TargetPath, Arguments = value.Arguments ?? [], UnresolvedArguments = value.RawArguments,
            WorkingDirectory = value.WorkingDirectory, Description = value.Description, IconLocation = value.IconLocation, AppUserModelId = value.AppUserModelId,
            ProtocolUri = uri, StartMenuFolder = folder is "." ? null : folder, CategoryHint = folder is "." ? null : folder?.Split(Path.DirectorySeparatorChar)[0],
            DiscoveryConfidence = value.TargetPath is not null || uri is not null ? ApplicationDiscoveryConfidence.High : ApplicationDiscoveryConfidence.Medium,
            Installed = value.TargetPath is null || !Path.IsPathFullyQualified(value.TargetPath) || File.Exists(value.TargetPath),
            ProviderData = new Dictionary<string, string> { ["knownFolderSource"] = sourceId, ["rawArguments"] = value.RawArguments ?? string.Empty, ["shellItemIdentity"] = value.ShellItemIdentity ?? string.Empty },
        };
    }

    private static bool IsSupported(string path) => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase);
}
