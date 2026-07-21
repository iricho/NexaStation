using System.Text.Json;
using NexaStation.Recovery;

namespace NexaStation.Updates;

public enum UpdatePreparationStatus { Ready, DeferredForActiveLaunch, Failed }

public sealed record UpdatePreparationRequest(string DatabasePath, string TargetVersion, bool ActiveLaunchSession);
public sealed record UpdatePreparationResult(UpdatePreparationStatus Status, string Message, string? BackupPath = null);
public sealed record PostUpdateValidationResult(bool Healthy, string Message, bool RecoveryRecommended);

public interface IProviderWriteGate
{
    Task<IAsyncDisposable> SuspendWritesAsync(CancellationToken cancellationToken = default);
}

public interface IPlannedRestartMarker
{
    Task WriteAsync(string targetVersion, string backupPath, CancellationToken cancellationToken = default);
    Task<PlannedUpdateRestart?> ReadAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed record PlannedUpdateRestart(int SchemaVersion, string TargetVersion, string BackupPath, DateTimeOffset PreparedAtUtc)
{
    public const int CurrentSchemaVersion = 1;
}

public interface IPostUpdateHealthProbe
{
    Task<(bool Healthy, string Message)> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Coordinates the safety boundary immediately before and after an installer-managed update.
/// It never installs packages itself and deliberately defers while a launch session is active.
/// </summary>
public sealed class UpdateReliabilityCoordinator(
    IDatabaseBackupService backups,
    IProviderWriteGate providerWrites,
    IPlannedRestartMarker restartMarker,
    IPostUpdateHealthProbe healthProbe)
{
    public async Task<UpdatePreparationResult> PrepareAsync(UpdatePreparationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ActiveLaunchSession)
            return new(UpdatePreparationStatus.DeferredForActiveLaunch, "The update is deferred until the active launch session ends.");
        if (string.IsNullOrWhiteSpace(request.DatabasePath) || string.IsNullOrWhiteSpace(request.TargetVersion))
            return new(UpdatePreparationStatus.Failed, "A database path and target version are required.");

        try
        {
            await using IAsyncDisposable suspension = await providerWrites.SuspendWritesAsync(cancellationToken).ConfigureAwait(false);
            BackupResult backup = await backups.CreateAsync(request.DatabasePath, $"before-update-{request.TargetVersion}", cancellationToken).ConfigureAwait(false);
            await restartMarker.WriteAsync(request.TargetVersion, backup.BackupPath, cancellationToken).ConfigureAwait(false);
            return new(UpdatePreparationStatus.Ready, "A validated pre-update backup was created and the planned restart was recorded.", backup.BackupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            return new(UpdatePreparationStatus.Failed, $"Update preparation failed safely: {exception.Message}");
        }
    }

    public async Task<PostUpdateValidationResult> ValidateAfterStartupAsync(CancellationToken cancellationToken = default)
    {
        PlannedUpdateRestart? planned = await restartMarker.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (planned is null) return new(true, "No planned update restart requires validation.", false);

        (bool healthy, string message) = await healthProbe.ValidateAsync(cancellationToken).ConfigureAwait(false);
        if (!healthy)
            return new(false, $"Post-update validation failed. The known-good backup remains at '{Path.GetFileName(planned.BackupPath)}'. {message}", true);

        await restartMarker.ClearAsync(cancellationToken).ConfigureAwait(false);
        return new(true, "Post-update startup validation passed; the previous known-good backup was retained.", false);
    }
}

public sealed class FilePlannedRestartMarker(string markerPath) : IPlannedRestartMarker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task WriteAsync(string targetVersion, string backupPath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(markerPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("The update marker has no parent directory."));
        string temporary = fullPath + ".tmp";
        await using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, new PlannedUpdateRestart(PlannedUpdateRestart.CurrentSchemaVersion, targetVersion, backupPath, DateTimeOffset.UtcNow), JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporary, fullPath, true);
    }

    public async Task<PlannedUpdateRestart?> ReadAsync(CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(markerPath);
        if (!File.Exists(fullPath)) return null;
        try
        {
            await using FileStream stream = File.OpenRead(fullPath);
            PlannedUpdateRestart? value = await JsonSerializer.DeserializeAsync<PlannedUpdateRestart>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return value?.SchemaVersion == PlannedUpdateRestart.CurrentSchemaVersion ? value : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(markerPath);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }
}
