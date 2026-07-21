using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace NexaStation.Release;

public enum ReleaseChannel
{
    Development,
    Alpha,
}

public readonly record struct SemanticVersion(int Major, int Minor, int Patch, int Revision = 0) : IComparable<SemanticVersion>
{
    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static SemanticVersion Parse(string value)
    {
        if (!TryParse(value, out SemanticVersion version))
        {
            throw new FormatException($"'{value}' is not a valid NexaStation version.");
        }

        return version;
    }

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        string normalized = value?.Split('-', '+')[0] ?? string.Empty;
        string[] parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 3 or > 4 || parts.Any(part => !int.TryParse(part, out int parsed) || parsed < 0))
        {
            return false;
        }

        version = new(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture), parts.Length == 4 ? int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture) : 0);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        return result != 0 ? result : Revision.CompareTo(other.Revision);
    }

    public override string ToString() => Revision == 0 ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}.{Revision}";
}

public sealed record ReleaseInfo(SemanticVersion Version, SemanticVersion PackageVersion, ReleaseChannel Channel, string InformationalVersion, string Architecture, int DiagnosticsFormatVersion)
{
    public string DisplayVersion => InformationalVersion;

    public static ReleaseInfo FromAssembly(Assembly assembly, ReleaseChannel channel)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        Dictionary<string, string> metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        SemanticVersion packageVersion = metadata.TryGetValue("NexaStationPackageVersion", out string? packageValue) && SemanticVersion.TryParse(packageValue, out SemanticVersion parsedPackage)
            ? parsedPackage
            : SemanticVersion.Parse(assembly.GetName().Version?.ToString() ?? informational);
        ReleaseChannel resolvedChannel = metadata.TryGetValue("NexaStationReleaseChannel", out string? channelValue) && Enum.TryParse(channelValue, true, out ReleaseChannel parsedChannel)
            ? parsedChannel
            : channel;
        string architecture = metadata.TryGetValue("NexaStationArchitecture", out string? architectureValue) && !string.IsNullOrWhiteSpace(architectureValue)
            ? architectureValue
            : System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        int diagnosticsFormatVersion = metadata.TryGetValue("NexaStationDiagnosticsFormatVersion", out string? diagnosticsValue) && int.TryParse(diagnosticsValue, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int parsedDiagnostics)
            ? parsedDiagnostics
            : 1;
        return new(SemanticVersion.Parse(informational), packageVersion, resolvedChannel, informational, architecture, diagnosticsFormatVersion);
    }
}

public enum ReleaseBlockerCode
{
    MissingLicenseDecision,
    MissingThirdPartyNotices,
    ManifestVersionMismatch,
    MissingPackageAsset,
    InvalidReleaseTag,
    VersionMetadataMismatch,
    PackageIdentityMismatch,
    UnsupportedCapability,
    InvalidPackageArchitecture,
}

public sealed record ReleaseBlocker(ReleaseBlockerCode Code, string Message);

public sealed record ReleaseValidationRequest(
    string RepositoryRoot,
    SemanticVersion ExpectedVersion,
    string? ReleaseTag = null,
    bool RequireReleaseTag = false);

public sealed record ReleaseMetadata(
    string SemanticVersion,
    string PackageVersion,
    string ReleaseChannel,
    string PackageName,
    string Publisher,
    string PublisherDisplayName,
    string Architecture,
    string MinimumWindowsVersion,
    int DiagnosticsFormatVersion);

public sealed record TesterReleaseManifest(
    string ProductName,
    string DisplayVersion,
    string PackageVersion,
    string ReleaseChannel,
    string Architecture,
    string GitCommit,
    DateTimeOffset BuildTimestampUtc,
    int DatabaseSchemaVersion,
    string MinimumWindowsVersion,
    string PackageFilename,
    string PackageSha256,
    string CertificateSubject,
    string CertificateThumbprint,
    [property: JsonPropertyName("signed")] bool IsSigned,
    [property: JsonPropertyName("selfContained")] bool IsSelfContained,
    IReadOnlyList<string> IncludedDependencies,
    IReadOnlyList<string> KnownLimitations,
    int DiagnosticsFormatVersion);

public sealed record TesterBundleFile(string RelativePath, byte[] Contents);

public sealed record TesterBundleRequest(
    string Version,
    string PackageVersion,
    string ReleaseChannel,
    string Architecture,
    string GitCommit,
    int DatabaseSchemaVersion,
    string MinimumWindowsVersion,
    string CertificateSubject,
    string CertificateThumbprint,
    bool IsPackageSigned,
    bool IsSelfContained,
    IReadOnlyList<string> IncludedDependencies,
    IReadOnlyList<string> KnownLimitations,
    byte[] MsixBytes,
    byte[] CertificateBytes,
    string? Changelog = null);

public sealed record TesterBundleResult(IReadOnlyList<TesterBundleFile> Files, TesterReleaseManifest Manifest, string Sha256Sums);

public static class ReleaseReadinessValidator
{
    private const string ExpectedPackageName = "NexaStation.Alpha";
    private const string ExpectedPublisher = "CN=NexaStation Alpha";
    private const string ExpectedPublisherDisplayName = "NexaStation Alpha";
    private static readonly string[] AllowedCapabilities = ["runFullTrust"];
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ReleaseBlocker> Validate(ReleaseValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string root = Path.GetFullPath(request.RepositoryRoot);
        List<ReleaseBlocker> blockers = [];
        string licensePath = Path.Combine(root, "LICENSE");
        if (!File.Exists(licensePath) || new FileInfo(licensePath).Length == 0)
        {
            blockers.Add(new(ReleaseBlockerCode.MissingLicenseDecision, "No project license has been selected. Packaging and public release must remain blocked until the owner makes an explicit license decision."));
        }

        string noticesPath = Path.Combine(root, "THIRD-PARTY-NOTICES.md");
        if (!File.Exists(noticesPath) || new FileInfo(noticesPath).Length == 0)
        {
            blockers.Add(new(ReleaseBlockerCode.MissingThirdPartyNotices, "THIRD-PARTY-NOTICES.md is missing or empty."));
        }

        ReleaseMetadata? metadata = ValidateVersionMetadata(root, request.ExpectedVersion, blockers);
        ValidateManifest(root, metadata, request.ExpectedVersion, blockers);
        ValidateAssets(root, blockers);
        if (request.RequireReleaseTag && !string.Equals(request.ReleaseTag, $"v{request.ExpectedVersion}", StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.InvalidReleaseTag, $"Release tag must be v{request.ExpectedVersion}."));
        }

        return blockers;
    }

    private static ReleaseMetadata? ValidateVersionMetadata(string root, SemanticVersion expectedVersion, List<ReleaseBlocker> blockers)
    {
        string versionJsonPath = Path.Combine(root, "eng", "release", "version.json");
        string versionPropsPath = Path.Combine(root, "Version.props");
        if (!File.Exists(versionJsonPath) || !File.Exists(versionPropsPath))
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, "Both Version.props and eng/release/version.json must exist."));
            return null;
        }

        ReleaseMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<ReleaseMetadata>(File.ReadAllText(versionJsonPath), MetadataJsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, $"eng/release/version.json could not be parsed: {exception.Message}"));
            return null;
        }

        if (metadata is null)
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, "eng/release/version.json is empty."));
            return null;
        }

        XDocument props = XDocument.Load(versionPropsPath, LoadOptions.None);
        string? semantic = Property(props, "NexaStationVersion");
        string? package = Property(props, "NexaStationPackageVersion");
        string? channel = Property(props, "NexaStationReleaseChannel");
        if (!string.Equals(metadata.SemanticVersion, semantic, StringComparison.Ordinal) ||
            !string.Equals(metadata.PackageVersion, package, StringComparison.Ordinal) ||
            !string.Equals(metadata.ReleaseChannel, channel, StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, "Version.props and eng/release/version.json disagree."));
        }

        if (!SemanticVersion.TryParse(metadata.SemanticVersion, out SemanticVersion semanticVersion) || semanticVersion != expectedVersion)
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, $"Semantic version '{metadata.SemanticVersion}' does not match expected '{expectedVersion}'."));
        }

        if (!string.Equals(metadata.ReleaseChannel, ReleaseChannel.Alpha.ToString(), StringComparison.Ordinal) ||
            !string.Equals(metadata.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(new(ReleaseBlockerCode.VersionMetadataMismatch, "Private Alpha releases must be channel Alpha and architecture x64."));
        }

        return metadata;
    }

    private static void ValidateManifest(string root, ReleaseMetadata? metadata, SemanticVersion expectedVersion, List<ReleaseBlocker> blockers)
    {
        string manifestPath = Path.Combine(root, "src", "NexaStation.App", "Package.appxmanifest");
        if (!File.Exists(manifestPath))
        {
            blockers.Add(new(ReleaseBlockerCode.ManifestVersionMismatch, "Package.appxmanifest is missing."));
            return;
        }

        XDocument document = XDocument.Load(manifestPath, LoadOptions.None);
        XElement? identity = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "Identity");
        XAttribute? version = identity?.Attribute("Version");
        string expected = $"{expectedVersion.Major}.{expectedVersion.Minor}.{expectedVersion.Patch}.{expectedVersion.Revision}";
        if (!string.Equals(version?.Value, expected, StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.ManifestVersionMismatch, $"Package version '{version?.Value ?? "missing"}' does not match '{expected}'."));
        }

        string expectedName = metadata?.PackageName ?? ExpectedPackageName;
        string expectedPublisher = metadata?.Publisher ?? ExpectedPublisher;
        if (!string.Equals(identity?.Attribute("Name")?.Value, expectedName, StringComparison.Ordinal) ||
            !string.Equals(identity?.Attribute("Publisher")?.Value, expectedPublisher, StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.PackageIdentityMismatch, "Package identity must remain NexaStation.Alpha with publisher CN=NexaStation Alpha."));
        }

        XElement? properties = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "Properties");
        string? publisherDisplayName = properties?.Elements().FirstOrDefault(element => element.Name.LocalName == "PublisherDisplayName")?.Value;
        if (!string.Equals(publisherDisplayName, metadata?.PublisherDisplayName ?? ExpectedPublisherDisplayName, StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.PackageIdentityMismatch, "PublisherDisplayName must match the Alpha signing identity."));
        }

        XElement? dependencies = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "Dependencies");
        XElement? targetFamily = dependencies?.Elements().FirstOrDefault(element => element.Name.LocalName == "TargetDeviceFamily");
        if (!string.Equals(targetFamily?.Attribute("MinVersion")?.Value, metadata?.MinimumWindowsVersion ?? "10.0.17763.0", StringComparison.Ordinal))
        {
            blockers.Add(new(ReleaseBlockerCode.PackageIdentityMismatch, "Minimum Windows version disagrees with release metadata."));
        }

        foreach (XElement capability in document.Descendants().Where(element => element.Name.LocalName == "Capability"))
        {
            string name = capability.Attribute("Name")?.Value ?? string.Empty;
            if (!AllowedCapabilities.Contains(name, StringComparer.Ordinal))
            {
                blockers.Add(new(ReleaseBlockerCode.UnsupportedCapability, $"Unsupported package capability '{name}' is declared."));
            }
        }

        string executable = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Application")?.Attribute("Executable")?.Value ?? string.Empty;
        if (!string.Equals(executable, "NexaStation.App.exe", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add(new(ReleaseBlockerCode.PackageIdentityMismatch, "The packaged application executable must be NexaStation.App.exe."));
        }
    }

    private static void ValidateAssets(string root, List<ReleaseBlocker> blockers)
    {
        string assets = Path.Combine(root, "src", "NexaStation.App", "Assets");
        foreach (string required in new[] { "Square44x44Logo.png", "Square150x150Logo.png", "Wide310x150Logo.png", "StoreLogo.png" })
        {
            string directPath = Path.Combine(assets, required);
            string stem = Path.GetFileNameWithoutExtension(required);
            bool hasScaleVariant = Directory.Exists(assets) && Directory.EnumerateFiles(assets, $"{stem}.scale-*.png").Any();
            if (!File.Exists(directPath) && !hasScaleVariant)
            {
                blockers.Add(new(ReleaseBlockerCode.MissingPackageAsset, $"Required package asset '{required}' is missing."));
            }
        }
    }

    private static string? Property(XDocument document, string name) =>
        document.Descendants().FirstOrDefault(element => element.Name.LocalName == name)?.Value;
}

public static class TesterBundleBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static TesterBundleResult Create(TesterBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string msixName = $"NexaStation-{request.Version}-alpha-x64.msix";
        TesterReleaseManifest manifest = new(
            "NexaStation",
            request.Version,
            request.PackageVersion,
            request.ReleaseChannel,
            request.Architecture,
            request.GitCommit,
            DateTimeOffset.UtcNow,
            request.DatabaseSchemaVersion,
            request.MinimumWindowsVersion,
            msixName,
            Sha256(request.MsixBytes),
            request.CertificateSubject,
            request.CertificateThumbprint,
            request.IsPackageSigned,
            request.IsSelfContained,
            request.IncludedDependencies,
            request.KnownLimitations,
            1);

        List<TesterBundleFile> files =
        [
            new(msixName, request.MsixBytes),
            new("NexaStation-Alpha-Test.cer", request.CertificateBytes),
            Text("INSTALL.txt", InstallText(request)),
            Text("UPDATE.txt", UpdateText(request)),
            Text("UNINSTALL.txt", UninstallText()),
            Text("TESTER-GUIDE.txt", TesterGuideText(request)),
            Text("KNOWN-ISSUES.txt", KnownIssuesText(request)),
            Text("CHANGELOG.txt", string.IsNullOrWhiteSpace(request.Changelog) ? $"NexaStation {request.Version} private Alpha tester build." : request.Changelog),
            Text("release-manifest.json", JsonSerializer.Serialize(manifest, JsonOptions)),
        ];
        string sums = Checksums(files);
        files.Add(Text("SHA256SUMS.txt", sums));
        return new(files, manifest, sums);
    }

    public static void WriteZip(TesterBundleResult result, string zipPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(zipPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
        foreach (TesterBundleFile file in result.Files)
        {
            if (IsForbiddenOutput(file.RelativePath))
            {
                throw new InvalidOperationException($"Forbidden tester bundle file '{file.RelativePath}'.");
            }

            System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry("NexaStation-Private-Alpha/" + file.RelativePath.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(file.Contents);
        }
    }

    public static IReadOnlyList<string> ValidateBundleFiles(IEnumerable<string> relativePaths)
    {
        string[] paths = relativePaths.ToArray();
        string[] required =
        [
            "INSTALL.txt", "UPDATE.txt", "UNINSTALL.txt", "TESTER-GUIDE.txt", "KNOWN-ISSUES.txt",
            "CHANGELOG.txt", "SHA256SUMS.txt", "release-manifest.json", "NexaStation-Alpha-Test.cer"
        ];
        List<string> errors = [];
        foreach (string requiredPath in required)
        {
            if (!paths.Contains(requiredPath, StringComparer.OrdinalIgnoreCase)) errors.Add($"Missing required bundle file '{requiredPath}'.");
        }

        if (!paths.Any(path => path.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))) errors.Add("Missing MSIX package.");
        foreach (string forbidden in paths.Where(IsForbiddenOutput)) errors.Add($"Forbidden bundle file '{forbidden}'.");
        return errors;
    }

    public static bool IsForbiddenOutput(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        return extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".snk", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".db", StringComparison.OrdinalIgnoreCase) ||
               relativePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
    }

    private static TesterBundleFile Text(string path, string content) => new(path, new UTF8Encoding(false).GetBytes(content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine)));

    private static string Checksums(IReadOnlyList<TesterBundleFile> files)
    {
        StringBuilder builder = new();
        foreach (TesterBundleFile file in files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(Sha256(file.Contents)).Append("  ").Append(file.RelativePath.Replace('\\', '/')).AppendLine();
        }

        return builder.ToString();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string InstallText(TesterBundleRequest request) => $"""
NexaStation Private Alpha installation

Version: {request.Version}
Package version: {request.PackageVersion}
Certificate subject: {request.CertificateSubject}
Certificate thumbprint: {request.CertificateThumbprint}

1. Extract this ZIP.
2. Verify the expected version and certificate thumbprint above.
3. Open NexaStation-Alpha-Test.cer.
4. Install the certificate for Current User.
5. Place it in the Trusted People certificate store.
6. Double-click the NexaStation .msix file.
7. Verify the publisher is NexaStation Alpha.
8. Select Install.
9. Launch NexaStation from the Start menu.

Certificate safety:
Install only the certificate included with the package received directly from the NexaStation developer. Verify its thumbprint against the release notes. Do not trust similarly named certificates from other sources. This certificate is only for private Alpha testing and may be removed when testing ends.

Troubleshooting:
Publisher could not be verified: confirm the certificate thumbprint and install it into Trusted People for Current User.
Certificate not trusted: close App Installer and reopen the .msix after trusting the certificate.
Older package already installed: install a package with a higher package version.
Architecture mismatch: this build is x64 only.
Package dependency failure: report the exact App Installer message and whether the machine is online.
App Installer unavailable: install or repair App Installer from Microsoft Store, then retry.
Windows policy blocking sideloading: ask the machine owner or administrator to allow trusted MSIX sideloading. Do not disable Windows security globally.
Installation already in progress: wait for Windows package installation to finish, then retry.
""";

    private static string UpdateText(TesterBundleRequest request) => $"""
NexaStation Private Alpha update

Install a newer NexaStation Alpha .msix with the same package identity and a higher package version. Windows should upgrade in place and preserve data under %LocalAppData%\NexaStation.

Before updating, open Settings > Data > Create Backup when available. NexaStation also creates pre-migration backups before schema-changing database updates.

Downgrades are not supported. If a newer database schema exists, older builds should refuse to start normally and Recovery should be used to restore a compatible backup.

The optional .appinstaller update channel is not active for this ZIP unless the release notes include real HTTPS package and update URLs.
""";

    private static string UninstallText() => """
NexaStation uninstall

Uninstall through Windows Settings > Apps > Installed Apps > NexaStation > Uninstall.

Windows package uninstall does not delete installed games, applications, ROMs, emulators, photos, videos, launcher manifests, Steam/Epic/GOG data, or external artwork.

Local NexaStation data may remain under %LocalAppData%\NexaStation so testers can reinstall or send diagnostics. Delete that folder only when you intentionally want to remove the local library database, backups, caches, settings, logs, and crash reports.

To remove the private Alpha certificate after testing, open certmgr.msc for Current User, go to Trusted People > Certificates, find the certificate with the documented NexaStation Alpha thumbprint, and delete only that certificate.
""";

    private static string TesterGuideText(TesterBundleRequest request) => $"""
NexaStation Private Alpha tester guide

NexaStation is a local-first Windows frontend for games, apps, emulators, and media. This private Alpha may be incomplete or incompatible with specific games, applications, controllers, or launchers.

NexaStation does not delete installed games, applications, ROMs, photos, or videos during normal library operations.

Use Settings for diagnostics, logs, data folder access, startup preferences, Safe Mode and Recovery. Diagnostics are created only when you export them and are not uploaded automatically.

When reporting a bug, include Windows version, controller type, display resolution, launcher/provider, reproduction steps, whether Safe Mode works, and the diagnostics correlation ID if one was created.

Do not upload ROMs, games, photos, videos, API keys, credentials, full databases, or private Gallery paths.

Version: {request.Version}
Channel: {request.ReleaseChannel}
Architecture: {request.Architecture}
""";

    private static string KnownIssuesText(TesterBundleRequest request)
    {
        StringBuilder builder = new();
        builder.AppendLine("NexaStation Private Alpha known issues");
        builder.AppendLine();
        if (request.KnownLimitations.Count == 0) builder.AppendLine("No release-specific known issues were supplied. Treat unverified hardware, controllers, launchers, and emulators as Alpha risk.");
        foreach (string limitation in request.KnownLimitations) builder.AppendLine("- " + limitation);
        builder.AppendLine("- Clean-machine installation, Windows App Certification Kit, update, and uninstall results must be reported only when actually performed.");
        return builder.ToString();
    }
}
