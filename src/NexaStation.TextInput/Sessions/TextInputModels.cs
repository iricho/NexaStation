using System.Globalization;

namespace NexaStation.TextInput.Sessions;

public enum TextInputType { Search, GeneralText, Title, Url, FileName, Number, Pin, PathDisplayOnly }
[Flags] public enum SuggestionsSource { None = 0, Library = 1, Applications = 2, Collections = 4, Platforms = 8, Providers = 16, GalleryAlbums = 32, RecentSearches = 64, AllLocal = Library | Applications | Collections | Platforms | Providers | GalleryAlbums }
public enum TextInputCloseReason { Submitted, Cancelled, Replaced, WindowClosed }

public sealed record TextInputValidationRule(string Id, string Message, string? Pattern = null, int? MinimumLength = null, int? MaximumLength = null);

public sealed record TextInputRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string InitialText { get; init; } = string.Empty;
    public string? Placeholder { get; init; }
    public TextInputType InputType { get; init; } = TextInputType.GeneralText;
    public int MaximumLength { get; init; } = 256;
    public bool AllowEmpty { get; init; } = true;
    public SuggestionsSource SuggestionsSource { get; init; } = SuggestionsSource.None;
    public bool IsSensitive { get; init; }
    public IReadOnlyList<TextInputValidationRule> ValidationRules { get; init; } = [];
    public string SubmitLabel { get; init; } = "Submit";
    public string CancelLabel { get; init; } = "Cancel";
}

public sealed record TextInputResult(bool Submitted, string? Text, TextInputCloseReason CloseReason, string? ValidationError = null)
{
    public static TextInputResult Cancelled(TextInputCloseReason reason = TextInputCloseReason.Cancelled) => new(false, null, reason);
}

public sealed class OnScreenKeyboardStateChangedEventArgs(bool isOpen, TextInputRequest? request) : EventArgs
{
    public bool IsOpen { get; } = isOpen;
    public TextInputRequest? Request { get; } = request;
}

public interface IOnScreenKeyboardService
{
    bool IsOpen { get; }
    event EventHandler<OnScreenKeyboardStateChangedEventArgs>? StateChanged;
    Task<TextInputResult> ShowAsync(TextInputRequest request, CancellationToken cancellationToken = default);
    Task CloseAsync(TextInputCloseReason reason, CancellationToken cancellationToken = default);
}

public interface ITextInputPresenter
{
    Task<TextInputResult> PresentAsync(TextInputSession session, CancellationToken cancellationToken = default);
    Task CloseAsync(TextInputCloseReason reason, CancellationToken cancellationToken = default);
}

public sealed class TextInputSession
{
    public TextInputSession(TextInputRequest request, TextInputBuffer buffer, Keyboard.KeyboardLayout layout)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public TextInputRequest Request { get; }
    public TextInputBuffer Buffer { get; }
    public Keyboard.KeyboardLayout Layout { get; private set; }
    public bool Shifted { get; private set; }
    public void SetLayout(Keyboard.KeyboardLayout layout) { Layout = layout ?? throw new ArgumentNullException(nameof(layout)); Shifted = false; }
    public void ToggleShift() => Shifted = !Shifted;
    public string DisplayText => Request.IsSensitive || Request.InputType == TextInputType.Pin ? new string('•', Buffer.Length) : Buffer.Text;
    public CultureInfo Culture => string.IsNullOrWhiteSpace(Layout.LanguageTag) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(Layout.LanguageTag);
}
