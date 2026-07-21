using System.Diagnostics;

namespace NexaStation.MediaPlayer.ExternalPlayers;

public enum ExternalPlayerDetectionSource { NexaStationApplication, StartMenu, WindowsRegistration, UserAdded, DiscoverCatalogue }
public enum ExternalPlayerPreferenceScope { UseOnce, FileExtension, MediaItem, PreferredExternalPlayer }
public sealed record ExternalMediaPlayerDescriptor(string Id, string DisplayName, string? ExecutablePath, string? ApplicationIdentity, string? IconPath, ExternalPlayerDetectionSource DetectionSource, IReadOnlySet<string> SupportedExtensions, bool UserAdded, bool Available, bool Preferred, string ArgumentsTemplate = "{MEDIA_PATH}");
public sealed record ExternalPlayerLaunchRequest(ExternalMediaPlayerDescriptor Player, string MediaPath, string? MediaItemId = null, ExternalPlayerPreferenceScope PreferenceScope = ExternalPlayerPreferenceScope.UseOnce);
public sealed record ExternalPlayerLaunchResult(bool Succeeded, int? ProcessId = null, string? Error = null);

public interface IExternalMediaPlayerRegistrationStore { Task<IReadOnlyList<ExternalMediaPlayerDescriptor>> GetAsync(CancellationToken cancellationToken = default); }
public interface IExternalMediaPlayerService
{
    Task<IReadOnlyList<ExternalMediaPlayerDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<ExternalPlayerLaunchResult> OpenAsync(ExternalPlayerLaunchRequest request, CancellationToken cancellationToken = default);
}

public sealed class ExternalMediaPlayerService(IExternalMediaPlayerRegistrationStore registrations) : IExternalMediaPlayerService
{
    private static readonly HashSet<string> ShellNames = new(StringComparer.OrdinalIgnoreCase) { "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe" };

    public async Task<IReadOnlyList<ExternalMediaPlayerDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default) =>
        (await registrations.GetAsync(cancellationToken).ConfigureAwait(false)).Select(player => player with { Available = ValidateExecutable(player.ExecutablePath) }).OrderByDescending(player => player.Preferred).ThenBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public Task<ExternalPlayerLaunchResult> OpenAsync(ExternalPlayerLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(request.MediaPath)) return Task.FromResult(new ExternalPlayerLaunchResult(false, Error: "The media file could not be located."));
        if (!ValidateExecutable(request.Player.ExecutablePath)) return Task.FromResult(new ExternalPlayerLaunchResult(false, Error: "The external player is unavailable or unsafe."));
        try
        {
            ProcessStartInfo start = new(request.Player.ExecutablePath!) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(request.Player.ExecutablePath) };
            foreach (string argument in RenderArguments(request.Player.ArgumentsTemplate, Path.GetFullPath(request.MediaPath))) start.ArgumentList.Add(argument);
            Process? process = Process.Start(start);
            return Task.FromResult(process is null ? new ExternalPlayerLaunchResult(false, Error: "Windows did not start the selected player.") : new ExternalPlayerLaunchResult(true, process.Id));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException) { return Task.FromResult(new ExternalPlayerLaunchResult(false, Error: exception.Message)); }
    }

    public static bool ValidateExecutable(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path) && !ShellNames.Contains(Path.GetFileName(path));
    public static IReadOnlyList<string> RenderArguments(string template, string mediaPath)
    {
        if (string.IsNullOrWhiteSpace(template)) template = "{MEDIA_PATH}";
        if (!template.Contains("{MEDIA_PATH}", StringComparison.Ordinal) || template.IndexOfAny(['\r', '\n', '&', '|', '>', '<', ';', '`']) >= 0) throw new InvalidOperationException("The external-player argument template is unsafe.");
        List<string> result = []; System.Text.StringBuilder current = new(); bool quoted = false;
        foreach (char character in template)
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(character) && !quoted) { if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); } continue; }
            current.Append(character);
        }
        if (quoted) throw new InvalidOperationException("The external-player argument template has an unmatched quote.");
        if (current.Length > 0) result.Add(current.ToString());
        return result.Select(argument => argument.Replace("{MEDIA_PATH}", mediaPath, StringComparison.Ordinal)).ToArray();
    }
}

public sealed class EmptyExternalMediaPlayerRegistrationStore : IExternalMediaPlayerRegistrationStore
{
    public Task<IReadOnlyList<ExternalMediaPlayerDescriptor>> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ExternalMediaPlayerDescriptor>>([]);
}
