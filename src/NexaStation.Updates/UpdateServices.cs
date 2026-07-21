using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexaStation.Release;

namespace NexaStation.Updates;

public enum UpdateCheckStatus
{
    Disabled,
    UpToDate,
    UpdateAvailable,
    Failed,
}

public sealed record UpdateManifest(string Version, string Channel, Uri InstallerUri, string? ReleaseNotesUri = null);
public sealed record UpdateCheckResult(UpdateCheckStatus Status, SemanticVersion CurrentVersion, SemanticVersion? AvailableVersion, Uri? InstallerUri, string Message);

public sealed record UpdatePreferences(bool AutomaticChecksEnabled, ReleaseChannel Channel)
{
    public static UpdatePreferences Default => new(false, ReleaseChannel.Development);
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(SemanticVersion currentVersion, UpdatePreferences preferences, CancellationToken cancellationToken = default);
}

public sealed class ManifestUpdateService(HttpClient httpClient, Uri manifestUri) : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    public async Task<UpdateCheckResult> CheckAsync(SemanticVersion currentVersion, UpdatePreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        if (!preferences.AutomaticChecksEnabled && preferences.Channel == ReleaseChannel.Development)
        {
            return new(UpdateCheckStatus.Disabled, currentVersion, null, null, "Update checks are disabled for development builds.");
        }

        try
        {
            UpdateManifest? manifest = await httpClient.GetFromJsonAsync<UpdateManifest>(manifestUri, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (manifest is null || !SemanticVersion.TryParse(manifest.Version, out SemanticVersion available))
            {
                return new(UpdateCheckStatus.Failed, currentVersion, null, null, "The update manifest is missing or invalid.");
            }

            if (!Enum.TryParse(manifest.Channel, true, out ReleaseChannel channel) || channel != preferences.Channel)
            {
                return new(UpdateCheckStatus.UpToDate, currentVersion, null, null, "No update is available for the selected channel.");
            }

            return available.CompareTo(currentVersion) > 0
                ? new(UpdateCheckStatus.UpdateAvailable, currentVersion, available, manifest.InstallerUri, $"NexaStation {available} is available.")
                : new(UpdateCheckStatus.UpToDate, currentVersion, available, null, "NexaStation is up to date.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException or TaskCanceledException)
        {
            return new(UpdateCheckStatus.Failed, currentVersion, null, null, $"The update check failed: {exception.Message}");
        }
    }
}
