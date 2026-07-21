using Microsoft.Win32;
using System.Globalization;
using NexaStation.EmulatorDiscovery.Definitions;
using NexaStation.EmulatorDiscovery.Discovery;

namespace NexaStation.EmulatorDiscovery.Sources;

public sealed class RegistryEmulatorSource : IEmulatorDiscoverySource
{
    public EmulatorCandidateSourceType SourceType => EmulatorCandidateSourceType.Registry;
    public Task<IReadOnlyList<EmulatorSourceCandidate>> DiscoverAsync(EmulatorDiscoveryRequest request, IReadOnlyList<EmulatorDefinition> definitions, CancellationToken cancellationToken)
    {
        List<EmulatorSourceCandidate> result = [];
        foreach ((RegistryHive hive, RegistryView view) in new[] { (RegistryHive.CurrentUser, RegistryView.Default), (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32) })
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view); using RegistryKey? uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"); if (uninstall is null) continue;
                foreach (string subName in uninstall.GetSubKeyNames()) { using RegistryKey? entry = uninstall.OpenSubKey(subName); string displayName = Convert.ToString(entry?.GetValue("DisplayName"),CultureInfo.InvariantCulture) ?? string.Empty; EmulatorDefinition? definition = definitions.FirstOrDefault(value => value.KnownProductNames.Any(name => displayName.Contains(name, StringComparison.OrdinalIgnoreCase))); if (definition is null) continue; string? install = Convert.ToString(entry?.GetValue("InstallLocation"),CultureInfo.InvariantCulture); if (string.IsNullOrWhiteSpace(install)) continue; foreach (string executable in definition.KnownExecutableNames) { string candidate = Path.Combine(install, executable); if (File.Exists(candidate)) result.Add(new(candidate, SourceType, DefinitionHint: definition.Id)); } }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        }
        return Task.FromResult<IReadOnlyList<EmulatorSourceCandidate>>(result);
    }
}
