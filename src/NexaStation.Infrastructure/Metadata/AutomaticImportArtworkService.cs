using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

public sealed class AutomaticImportArtworkService(
    ISettingsService settings,
    IArtworkJobService jobs,
    IArtworkJobPreflightService preflight,
    IArtworkJobProcessor processor,
    IAppLogger logger) : IAutomaticImportArtworkService
{
    public async Task QueueMissingArtworkAsync(
        string providerId,
        IReadOnlyCollection<string> libraryItemIds,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(providerId, "epic", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(providerId, "gog", StringComparison.OrdinalIgnoreCase)) return;
        if (!await settings.GetAsync("Metadata.AutomaticArtworkAfterImport", true, cancellationToken).ConfigureAwait(false)) return;

        string[] itemIds = libraryItemIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (itemIds.Length == 0) return;

        ArtworkJobRequest request = new(
            $"Artwork for imported {ProviderName(providerId)} games",
            itemIds,
            [ArtworkKind.CoverPortrait, ArtworkKind.HeroBackground],
            FillMissingOnly: true,
            NeverReplaceUserArtwork: true,
            AutoApplyThreshold: DeterministicGameMatchScorer.AutoApplyThreshold,
            MaximumConcurrency: 2);
        try
        {
            ArtworkJobPreflightResult readiness = await preflight.CheckAsync(request, cancellationToken).ConfigureAwait(false);
            if (!readiness.CanStart)
            {
                logger.Information("AutomaticImportArtworkSkipped",
                    "Imported games were kept, but automatic artwork is not currently available.",
                    new Dictionary<string, object?>
                    {
                        ["ProviderId"] = providerId,
                        ["ItemCount"] = itemIds.Length,
                        ["Reasons"] = string.Join(" ", readiness.BlockingReasons),
                    });
                return;
            }

            string jobId = await jobs.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            await jobs.StartAsync(jobId, cancellationToken).ConfigureAwait(false);
            logger.Information("AutomaticImportArtworkQueued",
                $"Queued missing artwork for {itemIds.Length} imported {ProviderName(providerId)} game(s).",
                new Dictionary<string, object?> { ["ProviderId"] = providerId, ["ItemCount"] = itemIds.Length, ["JobId"] = jobId });
            _ = RunSafelyAsync(jobId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError("AutomaticImportArtworkFailed",
                "Imported games were kept, but their automatic artwork job could not be started.",
                exception,
                new Dictionary<string, object?> { ["ProviderId"] = providerId, ["ItemCount"] = itemIds.Length });
        }
    }

    private async Task RunSafelyAsync(string jobId)
    {
        try { await processor.RunAsync(jobId).ConfigureAwait(false); }
        catch (Exception exception)
        {
            logger.LogError("AutomaticImportArtworkBackgroundFailed",
                "The automatic artwork job stopped unexpectedly.",
                exception,
                new Dictionary<string, object?> { ["JobId"] = jobId });
        }
    }

    private static string ProviderName(string providerId) =>
        string.Equals(providerId, "epic", StringComparison.OrdinalIgnoreCase) ? "Epic Games" : "GOG";
}
