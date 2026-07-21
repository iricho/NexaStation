using NexaStation.TextInput.Keyboard;
using NexaStation.TextInput.Validation;

namespace NexaStation.TextInput.Sessions;

public sealed class OnScreenKeyboardService(ITextInputPresenter presenter, IKeyboardLayoutCatalog layouts, TextInputValidator validator) : IOnScreenKeyboardService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _activeCancellation;
    public bool IsOpen { get; private set; }
    public event EventHandler<OnScreenKeyboardStateChangedEventArgs>? StateChanged;

    public async Task<TextInputResult> ShowAsync(TextInputRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumLength is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(request), "MaximumLength must be between 1 and 4096.");
        if ((request.IsSensitive || request.InputType == TextInputType.Pin) && request.SuggestionsSource != SuggestionsSource.None) request = request with { SuggestionsSource = SuggestionsSource.None };
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOpen) await CloseCoreAsync(TextInputCloseReason.Replaced, cancellationToken).ConfigureAwait(false);
            _activeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsOpen = true; StateChanged?.Invoke(this, new(true, request));
            TextInputBuffer buffer = new(request.InitialText, request.MaximumLength, request.IsSensitive || request.InputType == TextInputType.Pin);
            TextInputSession session = new(request, buffer, layouts.GetDefault(request.InputType));
            TextInputResult result = await presenter.PresentAsync(session, _activeCancellation.Token).ConfigureAwait(false);
            if (result.Submitted)
            {
                string text = result.Text ?? buffer.Text;
                TextInputValidationResult validation = validator.Validate(request, text);
                if (!validation.IsValid) return new(false, null, TextInputCloseReason.Cancelled, validation.Error);
                result = result with { Text = text };
            }
            if (buffer.IsSensitive) buffer.Clear();
            return result;
        }
        finally
        {
            _activeCancellation?.Dispose(); _activeCancellation = null; IsOpen = false; StateChanged?.Invoke(this, new(false, null)); _gate.Release();
        }
    }

    public async Task CloseAsync(TextInputCloseReason reason, CancellationToken cancellationToken = default)
    {
        if (!IsOpen) return;
        await presenter.CloseAsync(reason, cancellationToken).ConfigureAwait(false);
        _activeCancellation?.Cancel();
    }

    private async Task CloseCoreAsync(TextInputCloseReason reason, CancellationToken cancellationToken)
    {
        await presenter.CloseAsync(reason, cancellationToken).ConfigureAwait(false);
        _activeCancellation?.Cancel();
    }

    public void Dispose()
    {
        _activeCancellation?.Cancel();
        _activeCancellation?.Dispose();
        _gate.Dispose();
    }
}
