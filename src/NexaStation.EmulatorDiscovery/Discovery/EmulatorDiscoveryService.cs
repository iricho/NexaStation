using System.Diagnostics;
using NexaStation.Core;
using NexaStation.Domain;
using NexaStation.EmulatorDiscovery.Definitions;

namespace NexaStation.EmulatorDiscovery.Discovery;

public sealed class EmulatorDiscoveryService(IEmulationPersistence emulation, IEmulatorAdapterRegistry adapters, IEnumerable<IEmulatorDiscoverySource> sources) : IEmulatorDiscoveryService
{
    private readonly IEmulatorDiscoverySource[] _sources = sources.ToArray();
    private IReadOnlyList<EmulatorInstallation> _existing = [];
    private IReadOnlyList<EmulatorDefinition> _definitions = BuiltInEmulatorDefinitions.All;

    public async Task<EmulatorDiscoveryResult> DiscoverAsync(EmulatorDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); Stopwatch timer = Stopwatch.StartNew(); _existing = await emulation.GetInstallationsAsync(cancellationToken).ConfigureAwait(false); _definitions = request.KnownDefinitions.Where(definition => definition.Enabled || request.IncludeDisabledDefinitions).ToArray();
        List<EmulatorSourceCandidate> raw = [];
        if (request.DetectionSources.HasFlag(EmulatorDetectionSources.ExistingRecords)) raw.AddRange(_existing.Select(value => new EmulatorSourceCandidate(value.ExecutablePath, EmulatorCandidateSourceType.ExistingRecord, value.Id, value.AdapterId, true)));
        if (!string.IsNullOrWhiteSpace(request.UserSelectedExecutable) && request.DetectionSources.HasFlag(EmulatorDetectionSources.UserSelectedExecutable)) raw.Add(new(request.UserSelectedExecutable, EmulatorCandidateSourceType.UserSelectedExecutable, UserApproved: true));
        foreach (IEmulatorDiscoverySource source in _sources) { cancellationToken.ThrowIfCancellationRequested(); if (!Enabled(source.SourceType, request.DetectionSources)) continue; raw.AddRange(await source.DiscoverAsync(request, _definitions, cancellationToken).ConfigureAwait(false)); }
        List<EmulatorCandidate> candidates = [];
        foreach (EmulatorSourceCandidate source in raw.GroupBy(value => Normalize(value.ExecutablePath), StringComparer.OrdinalIgnoreCase).Select(group => group.OrderByDescending(value => value.UserApproved).First()))
        {
            cancellationToken.ThrowIfCancellationRequested(); EmulatorDefinition? definition = MatchDefinition(source);
            if (definition is null) continue;
            EmulatorCandidate initial = new(Guid.NewGuid().ToString("N"), definition.Id, definition.DisplayName, source.ExecutablePath, Normalize(source.ExecutablePath), null, source.SourceType, source.UserApproved ? .8 : .55, EmulatorCandidateStatus.ReviewRequired, source.ExistingEmulatorId, ["Known executable name matched."], source.SourceType == EmulatorCandidateSourceType.ApprovedPortableFolder);
            EmulatorValidationResult validation = await ValidateAsync(initial, cancellationToken).ConfigureAwait(false);
            candidates.Add(initial with { Version = validation.Version, Confidence = validation.Confidence, Status = validation.Status, ExistingEmulatorId = validation.ExistingEmulatorId ?? initial.ExistingEmulatorId, Evidence = validation.Evidence });
        }
        timer.Stop(); return new(candidates.OrderByDescending(value => value.Confidence).ThenBy(value => value.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray(), raw.Count, timer.Elapsed, false, []);
    }

    public Task<EmulatorValidationResult> ValidateAsync(EmulatorCandidate candidate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); List<string> evidence = [];
        if (!File.Exists(candidate.ExecutablePath)) return Task.FromResult(new EmulatorValidationResult(false, EmulatorCandidateStatus.Missing, 0, "The emulator executable is missing.", ["Executable does not exist."], ExistingEmulatorId: candidate.ExistingEmulatorId));
        string name = Path.GetFileName(candidate.ExecutablePath);
        if (name.Contains("install", StringComparison.OrdinalIgnoreCase) || name.Contains("unins", StringComparison.OrdinalIgnoreCase) || name.Contains("setup", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new EmulatorValidationResult(false, EmulatorCandidateStatus.InvalidInstallation, 0, "Installers and uninstallers are rejected.", ["Installer-like executable name."], ExistingEmulatorId: candidate.ExistingEmulatorId));
        EmulatorDefinition? definition = _definitions.FirstOrDefault(value => value.Id == candidate.DefinitionId); if (definition is null) return Task.FromResult(new EmulatorValidationResult(false, EmulatorCandidateStatus.DetectedUnsupported, .2, "No known definition matches this candidate.", evidence));
        double confidence = definition.KnownExecutableNames.Contains(name) ? .55 : .2; evidence.Add("Executable filename matches a known definition.");
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(candidate.ExecutablePath); string? detectedVersion = string.IsNullOrWhiteSpace(version.FileVersion) ? null : version.FileVersion;
        if (!string.IsNullOrWhiteSpace(version.ProductName) && definition.KnownProductNames.Any(product => version.ProductName.Contains(product, StringComparison.OrdinalIgnoreCase))) { confidence += .25; evidence.Add("File product metadata matches."); }
        string directory = Path.GetDirectoryName(candidate.ExecutablePath)!; bool companionMissing = definition.RequiredCompanionFiles.Any(file => !File.Exists(Path.Combine(directory, file))); if (companionMissing) return Task.FromResult(new EmulatorValidationResult(false, EmulatorCandidateStatus.InvalidInstallation, Math.Min(confidence, .45), "Required companion files are missing.", evidence, detectedVersion, candidate.ExistingEmulatorId));
        if (definition.RequiredCompanionFiles.Count > 0) { confidence += .1; evidence.Add("Required companion files are present."); }
        EmulatorInstallation? duplicate = _existing.FirstOrDefault(value => string.Equals(Normalize(value.ExecutablePath), candidate.NormalizedExecutablePath, StringComparison.OrdinalIgnoreCase)); if (duplicate is not null) { confidence = Math.Max(confidence, .95); evidence.Add("Matches an existing NexaStation emulator record."); }
        bool supported = adapters.GetById(definition.AdapterId) is not null; EmulatorCandidateStatus status = !supported ? EmulatorCandidateStatus.DetectedUnsupported : confidence >= .75 ? EmulatorCandidateStatus.DetectedAndSupported : EmulatorCandidateStatus.ReviewRequired;
        return Task.FromResult(new EmulatorValidationResult(true, status, Math.Min(1, confidence), status == EmulatorCandidateStatus.ReviewRequired ? "The filename matches, but this candidate requires review." : "The emulator candidate passed conservative validation.", evidence, detectedVersion, duplicate?.Id ?? candidate.ExistingEmulatorId));
    }

    private EmulatorDefinition? MatchDefinition(EmulatorSourceCandidate source) => _definitions.FirstOrDefault(value => string.Equals(value.Id, source.DefinitionHint, StringComparison.OrdinalIgnoreCase) || string.Equals(value.AdapterId, source.DefinitionHint, StringComparison.OrdinalIgnoreCase)) ?? _definitions.FirstOrDefault(value => value.KnownExecutableNames.Contains(Path.GetFileName(source.ExecutablePath)));
    private static bool Enabled(EmulatorCandidateSourceType source, EmulatorDetectionSources requested) => source switch { EmulatorCandidateSourceType.StartMenu => requested.HasFlag(EmulatorDetectionSources.StartMenu), EmulatorCandidateSourceType.Registry => requested.HasFlag(EmulatorDetectionSources.Registry), EmulatorCandidateSourceType.KnownPath => requested.HasFlag(EmulatorDetectionSources.KnownPaths), EmulatorCandidateSourceType.ApprovedPortableFolder => requested.HasFlag(EmulatorDetectionSources.ApprovedPortableFolders), EmulatorCandidateSourceType.InstalledApplication => requested.HasFlag(EmulatorDetectionSources.InstalledApplications), EmulatorCandidateSourceType.PackagedApplication => requested.HasFlag(EmulatorDetectionSources.PackagedApplications), _ => true };
    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
}
