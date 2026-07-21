using System.Collections.Concurrent;
using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Providers.Emulation.Discovery;

public sealed class EmulatorCapabilityProbe : IEmulatorCapabilityProbe
{
    private readonly ConcurrentDictionary<string, EmulatorProbeResult> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<EmulatorProbeResult> ProbeAsync(string executablePath, EmulatorProbeOptions options, CancellationToken cancellationToken = default)
    {
        string normalized = Path.GetFullPath(executablePath);
        if (!File.Exists(normalized)) return Failure("The selected emulator executable does not exist.");
        if (_cache.TryGetValue(normalized, out EmulatorProbeResult? cached)) return cached;
        if (options.ApprovedArguments.Count == 0) return Failure("This adapter has no approved probe arguments.");

        List<string> warnings = [];
        foreach (string argument in options.ApprovedArguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (argument.Any(char.IsWhiteSpace) || argument.Contains('&') || argument.Contains('|') || argument.Contains(';'))
            {
                warnings.Add("An unsafe probe argument was rejected.");
                continue;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = normalized,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(normalized)!,
            };
            startInfo.ArgumentList.Add(argument);
            using Process process = new() { StartInfo = startInfo };
            try
            {
                if (!process.Start()) continue;
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(options.Timeout);
                try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: false); } catch (InvalidOperationException) { }
                    return new(false, null, null, EmulatorCapability.None, [], ["The capability probe timed out."], null, true);
                }
                string output = Limit((await stdout.ConfigureAwait(false)) + Environment.NewLine + (await stderr.ConfigureAwait(false)), options.MaximumOutputCharacters);
                if (output.Trim().Length == 0) { warnings.Add($"Probe argument '{argument}' returned no output."); continue; }
                string? version = ExtractVersion(output);
                EmulatorCapability capabilities = EmulatorCapability.HelpOutput;
                if (version is not null) capabilities |= EmulatorCapability.VersionReporting;
                EmulatorProbeResult result = new(true, FileVersionInfo.GetVersionInfo(normalized).ProductName ?? Path.GetFileNameWithoutExtension(normalized), version, capabilities, [], warnings, output);
                _cache[normalized] = result;
                return result;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                warnings.Add(exception.Message);
            }
        }
        return new(false, null, null, EmulatorCapability.None, [], warnings.Count == 0 ? ["The selected executable did not respond to safe capability probes."] : warnings);
    }

    private static EmulatorProbeResult Failure(string message) => new(false, null, null, EmulatorCapability.None, [], [message]);
    private static string Limit(string output, int maximum) => output.Length <= Math.Max(256, maximum) ? output : output[..Math.Max(256, maximum)];
    private static string? ExtractVersion(string output)
    {
        foreach (string token in output.Split([' ', '\r', '\n', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
            if (token.Count(character => character == '.') is >= 1 and <= 3 && token.Any(char.IsDigit) && token.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')) return token.Trim('.', '-', '_');
        return null;
    }
}
