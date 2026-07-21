using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexaStation.Compatibility.Profiles;
using NexaStation.Compatibility.Validation;

namespace NexaStation.Compatibility.Feeds;

public sealed record CompatibilityFeedSettings(bool Enabled = false, Uri? ManifestUri = null);
public sealed record CompatibilityFeedManifest(int SchemaVersion, string KeyId, string Algorithm, string PayloadSha256, string SignatureBase64);
public sealed record CompatibilityFeedPayload(int SchemaVersion, DateTimeOffset GeneratedAtUtc, IReadOnlyList<CompatibilityProfile> Profiles);
public sealed record CompatibilityFeedImportResult(bool Succeeded, string Message, int ImportedProfiles = 0);

public interface ICompatibilityFeedSignatureVerifier
{
    bool Verify(ReadOnlySpan<byte> payload, CompatibilityFeedManifest manifest);
}

public sealed class RsaCompatibilityFeedSignatureVerifier(IReadOnlyDictionary<string, string> publicKeys) : ICompatibilityFeedSignatureVerifier
{
    public bool Verify(ReadOnlySpan<byte> payload, CompatibilityFeedManifest manifest)
    {
        if (!string.Equals(manifest.Algorithm, "RSA-SHA256", StringComparison.Ordinal)) return false;
        if (!publicKeys.TryGetValue(manifest.KeyId, out string? pem)) return false;
        byte[] digest = SHA256.HashData(payload);
        if (!string.Equals(Convert.ToHexString(digest), manifest.PayloadSha256, StringComparison.OrdinalIgnoreCase)) return false;
        byte[] signature;
        try { signature = Convert.FromBase64String(manifest.SignatureBase64); }
        catch (FormatException) { return false; }
        using RSA rsa = RSA.Create();
        try { rsa.ImportFromPem(pem); }
        catch (CryptographicException) { return false; }
        return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}

public interface ICompatibilityFeedService
{
    CompatibilityFeedSettings Settings { get; }
    Task<CompatibilityFeedImportResult> ImportAsync(string manifestJson, string payloadJson, CancellationToken cancellationToken = default);
}

public sealed class CompatibilityFeedService(
    CompatibilityFeedSettings settings,
    ICompatibilityFeedSignatureVerifier verifier,
    ICompatibilityProfileValidator validator,
    ICompatibilityProfileRepository repository) : ICompatibilityFeedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    public CompatibilityFeedSettings Settings { get; } = settings;

    public async Task<CompatibilityFeedImportResult> ImportAsync(string manifestJson, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (!Settings.Enabled) return new(false, "Compatibility feed checks are disabled.");
        if (Settings.ManifestUri is null || Settings.ManifestUri.Scheme != Uri.UriSchemeHttps) return new(false, "Compatibility feeds require an HTTPS manifest URI.");
        try
        {
            CompatibilityFeedManifest? manifest = JsonSerializer.Deserialize<CompatibilityFeedManifest>(manifestJson, JsonOptions);
            if (manifest is null || manifest.SchemaVersion != 1) return new(false, "The feed manifest schema is unsupported.");
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            if (!verifier.Verify(payloadBytes, manifest)) return new(false, "The feed signature or payload digest is invalid; the previous feed remains active.");
            using JsonDocument document = JsonDocument.Parse(payloadJson, new JsonDocumentOptions { MaxDepth = 32 });
            CompatibilityValidationResult rawValidation = validator.ValidateFeedJson(document.RootElement);
            if (!rawValidation.IsValid) return new(false, string.Join(" ", rawValidation.Errors));
            CompatibilityFeedPayload? payload = JsonSerializer.Deserialize<CompatibilityFeedPayload>(payloadJson, JsonOptions);
            if (payload is null || payload.SchemaVersion != 1) return new(false, "The feed payload schema is unsupported.");
            if (payload.Profiles is null || payload.Profiles.Count > 10_000) return new(false, "The feed profile collection is missing or exceeds the safe limit.");
            CompatibilityProfile[] imported = payload.Profiles.Select(profile => profile with { Source = Models.CompatibilityProfileSource.ImportedSignedFeed, UserModified = false }).ToArray();
            CompatibilityValidationResult? invalid = imported.Select(validator.Validate).FirstOrDefault(result => !result.IsValid);
            if (invalid is not null) return new(false, string.Join(" ", invalid.Errors));
            await repository.ReplaceImportedFeedAsync(imported, cancellationToken).ConfigureAwait(false);
            return new(true, "The signed compatibility feed was validated and imported.", imported.Length);
        }
        catch (JsonException exception)
        {
            return new(false, $"The feed JSON is invalid: {exception.Message}");
        }
    }
}

public sealed class RejectAllCompatibilityFeedSignatureVerifier : ICompatibilityFeedSignatureVerifier
{
    public bool Verify(ReadOnlySpan<byte> payload, CompatibilityFeedManifest manifest) => false;
}
