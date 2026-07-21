using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Artwork;

public static class LocalGameArtworkDiscovery
{
    public static IReadOnlyList<ArtworkCandidate> Find(string gamePath)
    {
        string directory = Path.GetDirectoryName(gamePath) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(gamePath);
        if (!Directory.Exists(directory)) return [];
        List<ArtworkCandidate> result = [];
        foreach ((string suffix, AssetType type, int priority) in new[] { ("", AssetType.Cover, 100), ("-cover", AssetType.Cover, 90), ("-background", AssetType.Background, 80) })
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                string candidate = Path.Combine(directory, stem + suffix + extension);
                if (File.Exists(candidate)) result.Add(new(candidate, type, priority));
            }
        return result;
    }
}
