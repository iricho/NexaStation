namespace NexaStation.Core;

public interface IApplicationStateService : IAsyncDisposable
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    void SetDebounced(string key, string value);
    Task FlushAsync(CancellationToken cancellationToken = default);
}

public sealed class ApplicationStateService(IApplicationStateRepository repository, TimeSpan? delay = null) : IApplicationStateService, IDisposable
{
    private readonly TimeSpan _delay = delay ?? TimeSpan.FromMilliseconds(350);
    private readonly Dictionary<string, string> _pending = [];
    private CancellationTokenSource? _debounce;
    private readonly SemaphoreSlim _gate = new(1,1);

    public Task<string?> GetAsync(string key,CancellationToken cancellationToken=default)=>repository.GetAsync(key,cancellationToken);

    public void SetDebounced(string key,string value)
    {
        lock(_pending)_pending[key]=value;
        _debounce?.Cancel();_debounce?.Dispose();_debounce=new CancellationTokenSource();
        _=WriteAfterDelayAsync(_debounce.Token);
    }

    public async Task FlushAsync(CancellationToken cancellationToken=default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            KeyValuePair<string,string>[] values;lock(_pending){values=_pending.ToArray();_pending.Clear();}
            foreach(var value in values)await repository.SetAsync(value.Key,value.Value,cancellationToken).ConfigureAwait(false);
        }
        finally{_gate.Release();}
    }

    private async Task WriteAfterDelayAsync(CancellationToken token){try{await Task.Delay(_delay,token).ConfigureAwait(false);await FlushAsync(token).ConfigureAwait(false);}catch(OperationCanceledException){}}
    public async ValueTask DisposeAsync(){_debounce?.Cancel();_debounce?.Dispose();await FlushAsync().ConfigureAwait(false);_gate.Dispose();}
    public void Dispose(){_debounce?.Cancel();_debounce?.Dispose();FlushAsync().GetAwaiter().GetResult();_gate.Dispose();}
}
