namespace NexaStation.QuickMenu.Safety;

public sealed class HoldSafetyCoordinator:IDisposable
{
    private CancellationTokenSource? _pending;public bool IsHolding=>_pending is not null;
    public async Task<bool> HoldAsync(int durationMilliseconds,CancellationToken cancellationToken=default){Cancel();CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);_pending=linked;try{await Task.Delay(Math.Max(1,durationMilliseconds),linked.Token);return ReferenceEquals(_pending,linked);}catch(OperationCanceledException){return false;}finally{if(ReferenceEquals(_pending,linked))_pending=null;linked.Dispose();}}
    public void Cancel()=>Interlocked.Exchange(ref _pending,null)?.Cancel();public void OnButtonReleased()=>Cancel();public void OnFocusChanged()=>Cancel();public void OnControllerDisconnected()=>Cancel();public void OnLostForeground()=>Cancel();public void Dispose()=>Cancel();
}
