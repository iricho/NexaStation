using System.Text;
using System.Text.RegularExpressions;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Identification;

public sealed partial class FilenameGameIdentifier : IGameIdentifierReader
{
    private static readonly HashSet<string> Regions = new(StringComparer.OrdinalIgnoreCase) { "USA", "Europe", "Japan", "World", "Korea", "Australia" };

    public bool CanIdentify(string platformId, string fileExtension) => !string.IsNullOrWhiteSpace(platformId) && !string.IsNullOrWhiteSpace(fileExtension);

    public async Task<GameIdentificationResult> IdentifyAsync(GameIdentificationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string original = Path.GetFileName(request.GamePath);
        string stem = Path.GetFileNameWithoutExtension(original).Trim();
        string? region = null;
        string? revision = null;
        int? disc = null;
        List<string> languages = [];

        foreach (Match match in RawTokenRegex().Matches(stem).Cast<Match>().Reverse())
        {
            string token = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
            if (Regions.Contains(token)) region ??= token;
            else if (RevisionRegex().Match(token) is { Success: true } revisionMatch) revision ??= revisionMatch.Groups[1].Value;
            else if (DiscRegex().Match(token) is { Success: true } discMatch && int.TryParse(discMatch.Groups[1].Value, out int parsedDisc)) disc ??= parsedDisc;
            else if (LanguageRegex().IsMatch(token)) languages.AddRange(token.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            else if (!string.Equals(token, "!", StringComparison.Ordinal)) continue;
            stem = stem.Remove(match.Index, match.Length).Trim();
        }

        string? gameIdentifier = null;
        if (string.Equals(request.PlatformId, "playstation-2", StringComparison.OrdinalIgnoreCase))
        {
            Match filenameSerial = Ps2SerialRegex().Match(stem);
            gameIdentifier = filenameSerial.Success
                ? NormalizePs2Serial(filenameSerial.Value)
                : await TryReadPs2SerialAsync(request.GamePath, request.MaximumReadBytes ?? 1_048_576, cancellationToken).ConfigureAwait(false);
            if (filenameSerial.Success) stem = stem.Remove(filenameSerial.Index, filenameSerial.Length).Trim(' ', '-', '_', '.');
        }

        stem = WhitespaceRegex().Replace(stem, " ").Trim(' ', '-', '_', '.');
        if (string.IsNullOrWhiteSpace(stem)) stem = Path.GetFileNameWithoutExtension(original).Trim();
        GameIdentificationConfidence confidence = gameIdentifier is null ? GameIdentificationConfidence.FilenameOnly : GameIdentificationConfidence.DiscSerial;
        return new(stem, original, gameIdentifier, region, revision, disc, null, confidence, languages.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static async Task<string?> TryReadPs2SerialAsync(string path, long maximumReadBytes, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || maximumReadBytes <= 0) return null;
        long remaining = Math.Min(maximumReadBytes, 4L * 1024 * 1024);
        byte[] buffer = new byte[64 * 1024];
        string overlap = string.Empty;
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                string text = overlap + Encoding.ASCII.GetString(buffer, 0, read);
                Match match = Ps2SerialRegex().Match(text);
                if (match.Success) return NormalizePs2Serial(match.Value);
                overlap = text.Length > 32 ? text[^32..] : text;
                remaining -= read;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private static string NormalizePs2Serial(string value)
    {
        string compact = NonAlphaNumericRegex().Replace(value, string.Empty).ToUpperInvariant();
        return compact.Length >= 9 ? $"{compact[..4]}-{compact[4..]}" : compact;
    }

    [GeneratedRegex(@"(?:\(([^()]*)\)|\[([^\[\]]*)\])", RegexOptions.CultureInvariant)] private static partial Regex RawTokenRegex();
    [GeneratedRegex(@"^Rev(?:ision)?\s*([A-Za-z0-9.]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RevisionRegex();
    [GeneratedRegex(@"^(?:Disc|Disk|CD)\s*(\d+)(?:\s*(?:of|/)\s*(\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex DiscRegex();
    [GeneratedRegex(@"^[A-Za-z]{2,3}(?:\s*,\s*[A-Za-z]{2,3})+$", RegexOptions.CultureInvariant)] private static partial Regex LanguageRegex();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"\b(?:SCUS|SLUS|SCES|SLES|SCPS|SLPS|SCPM|SLPM|SCAJ|SCKA|TCES|TCUS|TLUS|PBPX)[\s_.-]*\d{3}[\s_.-]*\d{2}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex Ps2SerialRegex();
    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)] private static partial Regex NonAlphaNumericRegex();
}
