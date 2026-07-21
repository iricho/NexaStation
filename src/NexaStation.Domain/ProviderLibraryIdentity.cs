using System.Security.Cryptography;
using System.Text;

namespace NexaStation.Domain;

public static class ProviderLibraryIdentity
{
    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
    }

    public static string CreateId(string providerId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        string normalized = NormalizePath(path);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        string provider = new(providerId.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return $"{provider}-library-{Convert.ToHexString(hash[..8]).ToLowerInvariant()}";
    }

    public static string CanonicalRootFromManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        DirectoryInfo? manifestDirectory = Directory.GetParent(Path.GetFullPath(manifestPath));
        return manifestDirectory is not null && manifestDirectory.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase)
            ? manifestDirectory.Parent?.FullName ?? manifestDirectory.FullName
            : manifestDirectory?.FullName ?? Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
    }

    public static bool IsLegacyChildPath(string canonicalRoot, string possibleChild)
    {
        string root = NormalizePath(canonicalRoot);
        string child = NormalizePath(possibleChild);
        return child.Equals(Path.Combine(root, "STEAMAPPS"), StringComparison.OrdinalIgnoreCase);
    }
}
