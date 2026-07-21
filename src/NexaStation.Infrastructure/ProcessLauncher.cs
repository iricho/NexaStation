using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure;

public sealed class ProcessLauncher(IAppLogger logger) : IProcessLauncher
{
    public Task<RunningProcess> StartAsync(LaunchTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        ProcessStartInfo startInfo = CreateStartInfo(target);
        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnExited(object? sender, EventArgs args)
        {
            try
            {
                exited.TrySetResult(process.ExitCode);
            }
            catch (InvalidOperationException exception)
            {
                exited.TrySetException(exception);
            }
        }

        process.Exited += OnExited;

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Windows did not start the requested process.");
            }

            logger.Information(
                "ProcessStarted",
                "A launch process started.",
                new Dictionary<string, object?>
                {
                    ["ProcessId"] = process.Id,
                    ["LaunchType"] = target.LaunchType.ToString(),
                });

            if (process.HasExited)
            {
                exited.TrySetResult(process.ExitCode);
            }

            return Task.FromResult(
                new RunningProcess(
                    process.Id,
                    exited.Task,
                    () =>
                    {
                        process.Exited -= OnExited;
                        process.Dispose();
                        return ValueTask.CompletedTask;
                    }));
        }
        catch
        {
            process.Exited -= OnExited;
            process.Dispose();
            throw;
        }
    }

    private static ProcessStartInfo CreateStartInfo(LaunchTarget target)
    {
        string fileName = target.LaunchType switch
        {
            LaunchType.Uri or LaunchType.WebApplication => target.Uri!.AbsoluteUri,
            LaunchType.WindowsShortcut => target.ShortcutPath!,
            _ => target.ExecutablePath!,
        };

        ProcessStartInfo result = new()
        {
            FileName = fileName,
            UseShellExecute = target.LaunchType is LaunchType.Uri or LaunchType.WebApplication or LaunchType.WindowsShortcut or LaunchType.SystemCommand,
        };

        if (target.ArgumentList.Count > 0)
        {
            foreach (string argument in target.ArgumentList) result.ArgumentList.Add(argument);
        }
        else
        {
            result.Arguments = target.Arguments ?? string.Empty;
        }

        if (!result.UseShellExecute)
            foreach ((string key, string value) in target.EnvironmentOverrides) result.Environment[key] = value;

        if (!string.IsNullOrWhiteSpace(target.WorkingDirectory))
        {
            result.WorkingDirectory = target.WorkingDirectory;
        }

        return result;
    }
}
