using System.Collections.Concurrent;
using NexaStation.Core;
using NexaStation.Domain;

namespace NexaStation.Infrastructure.Metadata;

public sealed class ProviderRateLimitCoordinator(TimeProvider? timeProvider = null) : IProviderRateLimitCoordinator
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);

    public async Task<RateLimitLease> AcquireAsync(string providerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        State state = _states.GetOrAdd(providerId, _ => new());
        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _time.GetUtcNow();
            DateTimeOffset allowed = state.RetryAfterUtc > state.NextRequestUtc ? state.RetryAfterUtc : state.NextRequestUtc;
            if (allowed > now) await Task.Delay(allowed - now, _time, cancellationToken).ConfigureAwait(false);
            state.NextRequestUtc = _time.GetUtcNow().AddMilliseconds(350);
            return new(providerId, _time.GetUtcNow(), () => state.Gate.Release());
        }
        catch
        {
            state.Gate.Release();
            throw;
        }
    }

    public Task ReportResponseAsync(string providerId, ProviderResponseMetadata metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State state = _states.GetOrAdd(providerId, _ => new());
        if (metadata.RetryAfterUtc is { } retry && retry > state.RetryAfterUtc) state.RetryAfterUtc = retry;
        if (metadata.AuthenticationFailed) state.RetryAfterUtc = _time.GetUtcNow().AddMinutes(5);
        return Task.CompletedTask;
    }

    private sealed class State
    {
        public SemaphoreSlim Gate { get; } = new(2, 2);
        public DateTimeOffset NextRequestUtc { get; set; }
        public DateTimeOffset RetryAfterUtc { get; set; }
    }
}
