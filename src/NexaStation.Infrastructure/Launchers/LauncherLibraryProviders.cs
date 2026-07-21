using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Launchers;

public sealed class EpicLibraryProvider : ILibraryProvider
{
    public string Id => "epic";
    public string DisplayName => "Epic Games";
    private static string ManifestRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
    public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); bool detected = Directory.Exists(ManifestRoot);
        return Task.FromResult(new ProviderAvailabilityResult(Id, detected, detected ? ManifestRoot : null, detected ? "Epic Games installation metadata was found." : "Epic Games was not detected.", !detected, []));
    }
    public async Task<ProviderScanResult> ScanAsync(ProviderScanRequest request, CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow; string root = request.ConfiguredInstallationPath ?? ManifestRoot;
        if (!Directory.Exists(root)) return new(Id, started, DateTime.UtcNow, null, [], [], ["Epic Games manifest metadata was not found."], []);
        List<DiscoveredLibraryItem> items = []; List<string> warnings = [];
        foreach (string manifest in Directory.EnumerateFiles(root, "*.item", SearchOption.TopDirectoryOnly).Take(20_000))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using FileStream stream = new(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 32 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false); JsonElement value = document.RootElement;
                string? appName = Text(value, "AppName"); string? catalogId = Text(value, "CatalogItemId"); string? title = Text(value, "DisplayName"); string? install = Text(value, "InstallLocation");
                if (appName is null || title is null || install is null) continue;
                string providerId = catalogId ?? appName; bool installed = Directory.Exists(install);
                Dictionary<string, string> data = new(StringComparer.OrdinalIgnoreCase) { ["AppName"] = appName };
                if (catalogId is not null) data["CatalogItemId"] = catalogId;
                if (Text(value, "LaunchExecutable") is { } executable) data["LaunchExecutable"] = executable;
                Uri launch = new($"com.epicgames.launcher://apps/{Uri.EscapeDataString(appName)}?action=launch&silent=true");
                items.Add(new(Id, providerId, title, title, ContentType.Game, install, manifest, launch, installed, File.GetLastWriteTimeUtc(manifest), data, []));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) { warnings.Add($"Skipped unreadable Epic manifest {Path.GetFileName(manifest)}."); }
        }
        ProviderLibrarySource source = new(ProviderLibraryIdentity.CreateId(Id, root), Id, root, ProviderLibraryIdentity.NormalizePath(root), true, true);
        return new(Id, started, DateTime.UtcNow, root, [source], items, warnings, [], false);
    }
    public Task<ProviderValidationResult> ValidateAsync(ProviderSource source, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); bool valid = source.Installed && source.ProviderData.ContainsKey("AppName"); return Task.FromResult(new ProviderValidationResult(valid, valid ? "Epic launch metadata is available." : "The Epic installation is unavailable.", [])); }
    public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); if (!source.ProviderData.TryGetValue("AppName", out string? app)) throw new InvalidOperationException("Epic launch identity is missing."); return Task.FromResult(new ResolvedLaunchTarget(Target(source, new($"com.epicgames.launcher://apps/{Uri.EscapeDataString(app)}?action=launch&silent=true"), 60), source)); }
    private static string? Text(JsonElement value, string property) => value.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    private static LaunchTarget Target(ProviderSource source, Uri uri, int timeout) => new(LaunchType.Uri, null, source.InstallationPath, null, uri, null, true, true) { Id = $"{source.ProviderId}-{source.ProviderItemId}", LibraryItemId = source.LibraryItemId, Name = "Epic Games", ProviderId = source.ProviderId, DetectionTimeoutSeconds = timeout, TrackingMode = ApplicationTrackingMode.ForegroundWindow };
}

public sealed record RegistryLauncherProfile(string Id, string DisplayName, IReadOnlyCollection<string> PublisherMarkers, Func<string, Uri> LaunchUri, int DetectionTimeoutSeconds = 60);

public sealed class RegistryLauncherProvider(RegistryLauncherProfile profile) : ILibraryProvider
{
    public string Id => profile.Id;
    public string DisplayName => profile.DisplayName;
    public Task<ProviderAvailabilityResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RegistryGame> games = Discover(cancellationToken);
        return Task.FromResult(new ProviderAvailabilityResult(Id, games.Count > 0, games.Count > 0 ? games[0].InstallLocation : null, games.Count > 0 ? $"Found {games.Count} installed {DisplayName} game(s)." : $"{DisplayName} games were not detected.", games.Count == 0, []));
    }
    public Task<ProviderScanResult> ScanAsync(ProviderScanRequest request, CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow; IReadOnlyList<RegistryGame> games = Discover(cancellationToken);
        DiscoveredLibraryItem[] items = games.Select(game => new DiscoveredLibraryItem(Id, game.ProviderItemId, game.Title, game.Title, ContentType.Game, game.InstallLocation, game.RegistryIdentity, profile.LaunchUri(game.ProviderItemId), Directory.Exists(game.InstallLocation), game.UpdatedAtUtc, new Dictionary<string, string> { ["Publisher"] = game.Publisher, ["RegistryIdentityHash"] = Hash(game.RegistryIdentity) }, [])).ToArray();
        return Task.FromResult(new ProviderScanResult(Id, started, DateTime.UtcNow, null, [], items, [], [], false));
    }
    public Task<ProviderValidationResult> ValidateAsync(ProviderSource source, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); bool valid = source.Installed && source.InstallationPath is { } path && Directory.Exists(path); return Task.FromResult(new ProviderValidationResult(valid, valid ? "Launcher-managed installation is available." : "The launcher-managed installation is unavailable.", [])); }
    public Task<ResolvedLaunchTarget> ResolveLaunchTargetAsync(ProviderSource source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); Uri uri = profile.LaunchUri(source.ProviderItemId);
        LaunchTarget target = new(LaunchType.Uri, null, source.InstallationPath, null, uri, null, true, true) { Id = $"{Id}-{source.ProviderItemId}", LibraryItemId = source.LibraryItemId, Name = DisplayName, ProviderId = Id, DetectionTimeoutSeconds = profile.DetectionTimeoutSeconds, ExitGracePeriodSeconds = 5, TrackingMode = ApplicationTrackingMode.ForegroundWindow };
        return Task.FromResult(new ResolvedLaunchTarget(target, source));
    }
    private RegistryGame[] Discover(CancellationToken token)
    {
        Dictionary<string, RegistryGame> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((RegistryHive hive, RegistryView view) in new[] { (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Registry64), (RegistryHive.CurrentUser, RegistryView.Registry32) })
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view); using RegistryKey? uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"); if (uninstall is null) continue;
                foreach (string name in uninstall.GetSubKeyNames().Take(20_000))
                {
                    token.ThrowIfCancellationRequested(); using RegistryKey? key = uninstall.OpenSubKey(name); if (key is null) continue;
                    string publisher = Convert.ToString(key.GetValue("Publisher"), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!profile.PublisherMarkers.Any(marker => publisher.Contains(marker, StringComparison.OrdinalIgnoreCase))) continue;
                    string? title = Convert.ToString(key.GetValue("DisplayName"), System.Globalization.CultureInfo.InvariantCulture); string? location = Convert.ToString(key.GetValue("InstallLocation"), System.Globalization.CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location) || !Path.IsPathFullyQualified(location)) continue;
                    string providerItemId = StableProductId(name, key); string registryIdentity = $"{hive}:{view}:{name}";
                    result.TryAdd(providerItemId, new(providerItemId, title, Path.GetFullPath(location), publisher, registryIdentity, DateTime.UtcNow));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException) { }
        }
        return result.Values.OrderBy(value => value.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static string StableProductId(string keyName, RegistryKey key)
    {
        foreach (string field in new[] { "ProductID", "GameID" })
            if (Convert.ToString(key.GetValue(field), System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } value && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')) return value;
        return keyName.Length <= 128 && keyName.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '{' or '}') ? keyName.Trim('{', '}') : Hash(keyName);
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();
    private sealed record RegistryGame(string ProviderItemId, string Title, string InstallLocation, string Publisher, string RegistryIdentity, DateTime UpdatedAtUtc);
}

public static class BuiltInLauncherProfiles
{
    public static RegistryLauncherProfile Gog { get; } = new("gog", "GOG", ["GOG.com", "GOG Sp. z o.o."], id => new($"goggalaxy://openGameView/{Uri.EscapeDataString(id)}"));
    public static RegistryLauncherProfile Ubisoft { get; } = new("ubisoft", "Ubisoft Connect", ["Ubisoft"], id => new($"uplay://launch/{Uri.EscapeDataString(id)}/0"), 75);
    public static RegistryLauncherProfile Ea { get; } = new("ea", "EA app", ["Electronic Arts", "EA Swiss"], id => new($"origin2://game/launch?offerIds={Uri.EscapeDataString(id)}"), 90);
    public static RegistryLauncherProfile BattleNet { get; } = new("battlenet", "Battle.net", ["Blizzard Entertainment"], id => new($"battlenet://launch/{Uri.EscapeDataString(id)}"), 90);
}
