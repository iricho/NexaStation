namespace NexaStation.Core;

public sealed class RunningProcess : IAsyncDisposable
{
    private readonly Func<ValueTask>? _dispose;

    public RunningProcess(int processId, Task<int> exitTask, Func<ValueTask>? dispose = null)
    {
        ProcessId = processId;
        ExitTask = exitTask ?? throw new ArgumentNullException(nameof(exitTask));
        _dispose = dispose;
    }

    public int ProcessId { get; }

    public Task<int> ExitTask { get; }

    public Task<int> WaitForExitAsync() => ExitTask;

    public ValueTask DisposeAsync() => _dispose?.Invoke() ?? ValueTask.CompletedTask;
}
