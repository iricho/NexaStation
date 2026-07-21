using NexaStation.EmulatorDiscovery.Definitions;
using NexaStation.EmulatorDiscovery.Discovery;

namespace NexaStation.EmulatorDiscovery.Sources;

public sealed class BoundedFolderEmulatorSource : IEmulatorDiscoverySource
{
    public EmulatorCandidateSourceType SourceType => EmulatorCandidateSourceType.ApprovedPortableFolder;

    public Task<IReadOnlyList<EmulatorSourceCandidate>> DiscoverAsync(EmulatorDiscoveryRequest request, IReadOnlyList<EmulatorDefinition> definitions, CancellationToken cancellationToken)
    {
        if (!request.IncludePortableApplications || request.ApprovedFolders.Count == 0) return Task.FromResult<IReadOnlyList<EmulatorSourceCandidate>>([]);
        HashSet<string> names = definitions.SelectMany(value => value.KnownExecutableNames).ToHashSet(StringComparer.OrdinalIgnoreCase); List<EmulatorSourceCandidate> result = []; int inspected = 0;
        foreach (string rootValue in request.ApprovedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested(); string root = Path.GetFullPath(rootValue); if (!Directory.Exists(root) || Path.GetPathRoot(root)?.Equals(root, StringComparison.OrdinalIgnoreCase) == true) continue;
            Queue<(string Path, int Depth)> pending = new(); pending.Enqueue((root, 0));
            while (pending.Count > 0 && inspected < request.MaximumFilesInspected)
            {
                cancellationToken.ThrowIfCancellationRequested(); (string directory, int depth) = pending.Dequeue();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)) { if (++inspected > request.MaximumFilesInspected) break; if (names.Contains(Path.GetFileName(file))) result.Add(new(file, SourceType, UserApproved: true)); }
                    if (depth >= request.MaximumRecursionDepth) continue;
                    foreach (string child in Directory.EnumerateDirectories(directory)) { FileAttributes attributes = File.GetAttributes(child); if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Enqueue((child, depth + 1)); }
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or IOException) { }
            }
        }
        return Task.FromResult<IReadOnlyList<EmulatorSourceCandidate>>(result);
    }
}
