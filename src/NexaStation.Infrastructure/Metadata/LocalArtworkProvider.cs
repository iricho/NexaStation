using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

public sealed class LocalArtworkProvider(IEnumerable<string> configuredRoots, IRemoteArtworkDownloader downloader) : IArtworkProvider
{
    private static readonly HashSet<string> Extensions = new([".jpg", ".jpeg", ".png", ".webp"], StringComparer.OrdinalIgnoreCase);
    private readonly string[] _roots = configuredRoots.Where(Path.IsPathFullyQualified).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private readonly ConcurrentDictionary<string, IReadOnlyList<RemoteArtworkAsset>> _matches = new(StringComparer.Ordinal);
    public string Id => "local";
    public string DisplayName => "Local Files";
    public ArtworkProviderCapabilities Capabilities => ArtworkProviderCapabilities.TitleSearch | ArtworkProviderCapabilities.PlatformSearch | ArtworkProviderCapabilities.CoverArtwork | ArtworkProviderCapabilities.WideArtwork | ArtworkProviderCapabilities.HeroArtwork | ArtworkProviderCapabilities.LogoArtwork | ArtworkProviderCapabilities.IconArtwork;
    public Task<ArtworkProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();bool hasFiles=_roots.Where(Directory.Exists).Any(root=>HasSupportedFile(root,cancellationToken));return Task.FromResult(hasFiles?new ArtworkProviderStatus(true,true,Message:$"{_roots.Length} configured local artwork folder(s)."):new ArtworkProviderStatus(false,true,"local_source_empty","The local artwork import folder contains no supported images."));
    }

    public Task<IReadOnlyList<ArtworkMatchCandidate>> SearchAsync(ArtworkSearchRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string title = GameTitleNormalizer.Normalize(request.SearchTitle ?? request.Identity.OriginalTitle);
        List<RemoteArtworkAsset> assets = [];
        foreach (string root in _roots)
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> directories = new[] { root };
            if (!string.IsNullOrWhiteSpace(request.PlatformId ?? request.Identity.PlatformId))
            {
                string platform = Path.Combine(root, SafeSegment(request.PlatformId ?? request.Identity.PlatformId!));
                if (Directory.Exists(platform)) directories = new[] { platform, root };
            }
            foreach (string directory in directories)
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).Take(1000).ToArray(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { continue; }
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Extensions.Contains(Path.GetExtension(file))) continue;
                    (string candidateTitle, ArtworkKind kind) = Classify(Path.GetFileNameWithoutExtension(file));
                    if (!string.Equals(GameTitleNormalizer.Normalize(candidateTitle), title, StringComparison.Ordinal)) continue;
                    string id = Hash(file);
                    ImageFileInspection inspection = ImageFileInspector.Inspect(file);
                    if (!inspection.Valid) continue;
                    assets.Add(new(Id, id, id + ":" + (int)kind, kind, new Uri(file), null, inspection.Width, inspection.Height, new FileInfo(file).Length, inspection.MimeType));
                }
            }
        }
        if (assets.Count == 0) return Task.FromResult<IReadOnlyList<ArtworkMatchCandidate>>([]);
        string gameId = Hash(string.Join('|', assets.Select(asset => asset.DownloadUri.LocalPath).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
        _matches[gameId] = assets;
        IReadOnlyList<ArtworkMatchCandidate> matches = [new(Id, gameId, request.Identity.OriginalTitle, request.PlatformId ?? request.Identity.PlatformId, request.Region ?? request.Identity.Region, null, 1000, MatchDecision.AutoApply, ["Exact normalized local filename."], [])];
        return Task.FromResult(matches);
    }

    public Task<IReadOnlyList<RemoteArtworkAsset>> GetAssetsAsync(ArtworkAssetRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RemoteArtworkAsset> result = _matches.TryGetValue(request.ProviderGameId, out IReadOnlyList<RemoteArtworkAsset>? assets)
            ? assets.Where(asset => request.AssetTypes.Contains(asset.Type)).ToArray() : [];
        return Task.FromResult(result);
    }

    public Task<ArtworkDownloadResult> DownloadAsync(RemoteArtworkAsset asset, ArtworkDownloadRequest request, CancellationToken cancellationToken = default) =>
        asset.DownloadUri.IsFile ? downloader.ImportLocalAsync(asset.DownloadUri.LocalPath, asset.Type, request, cancellationToken) : Task.FromResult(new ArtworkDownloadResult(false, null, null, 0, null, null, "not_local", "The local provider refused a remote source."));

    private static (string Title, ArtworkKind Type) Classify(string stem)
    {
        (string Suffix, ArtworkKind Type)[] suffixes = [("-background", ArtworkKind.HeroBackground), ("-hero", ArtworkKind.HeroBackground), ("-cover", ArtworkKind.CoverPortrait), ("-grid", ArtworkKind.GridWide), ("-logo", ArtworkKind.Logo), ("-icon", ArtworkKind.Icon)];
        foreach ((string suffix, ArtworkKind type) in suffixes) if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return (stem[..^suffix.Length], type);
        return (stem, ArtworkKind.CoverPortrait);
    }
    private static string SafeSegment(string value) => new(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();
    private static bool HasSupportedFile(string root,CancellationToken token)
    {
        try{foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.TopDirectoryOnly).Take(1000)){token.ThrowIfCancellationRequested();if(Extensions.Contains(Path.GetExtension(file)))return true;}foreach(string folder in Directory.EnumerateDirectories(root).Take(100)){foreach(string file in Directory.EnumerateFiles(folder,"*",SearchOption.TopDirectoryOnly).Take(100)){token.ThrowIfCancellationRequested();if(Extensions.Contains(Path.GetExtension(file)))return true;}}}catch(Exception exception)when(exception is IOException or UnauthorizedAccessException){}return false;
    }
}
