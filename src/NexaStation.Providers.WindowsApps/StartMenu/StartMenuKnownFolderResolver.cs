using NexaStation.Core;

namespace NexaStation.Providers.WindowsApps.StartMenu;

public sealed class StartMenuKnownFolderResolver : IStartMenuKnownFolderResolver
{
    public IReadOnlyList<(string ProviderSourceId, string Path)> Resolve(bool currentUser, bool common)
    {
        List<(string, string)> result = [];
        Add(result, currentUser, "current-user-programs", Environment.GetFolderPath(Environment.SpecialFolder.Programs));
        Add(result, common, "common-programs", Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
        return result;
    }

    private static void Add(List<(string, string)> result, bool enabled, string id, string path)
    {
        if (enabled && !string.IsNullOrWhiteSpace(path)) result.Add((id, Path.GetFullPath(path)));
    }
}
