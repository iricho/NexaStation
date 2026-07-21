using NexaStation.TextInput.Keyboard;
using NexaStation.TextInput.Sessions;
using NexaStation.TextInput.Suggestions;
using NexaStation.TextInput.Validation;
using Xunit;

namespace NexaStation.TextInput.Tests;

public sealed class TextInputTests
{
    [Fact] public void BufferSupportsInsertBackspaceAndCursorMovement() { TextInputBuffer buffer = new("ac", 8); buffer.MoveCursor(-1); Assert.True(buffer.Insert("b")); Assert.Equal("abc", buffer.Text); Assert.True(buffer.Backspace()); Assert.Equal("ac", buffer.Text); }
    [Fact] public void MaximumLengthIsEnforced() { TextInputBuffer buffer = new(string.Empty, 3); Assert.True(buffer.Insert("abcd")); Assert.Equal("abc", buffer.Text); Assert.False(buffer.Insert("z")); }
    [Fact] public void SensitiveBufferCanBeCleared() { TextInputBuffer buffer = new("1234", 8, true); buffer.Clear(); Assert.Empty(buffer.Text); Assert.Equal(0, buffer.Length); }

    [Fact] public void BuiltInLayoutsAreDeclarativeAndComplete()
    {
        BuiltInKeyboardLayoutCatalog catalog = new(); string[] expected = ["en-qwerty", "en-azerty", "ar", "he", "numeric", "symbols"]; Assert.Equal(expected, catalog.Layouts.Select(layout => layout.Id)); Assert.All(catalog.Layouts, layout => Assert.True(layout.BuiltIn));
    }
    [Fact] public void ArabicAndHebrewAreRightToLeft() { BuiltInKeyboardLayoutCatalog catalog = new(); Assert.Equal(KeyboardTextDirection.RightToLeft, catalog.GetById("ar").Direction); Assert.Equal(KeyboardTextDirection.RightToLeft, catalog.GetById("he").Direction); }
    [Fact] public void UrlUsesLeftToRightDefault() { BuiltInKeyboardLayoutCatalog catalog = new(); Assert.Equal(KeyboardTextDirection.LeftToRight, catalog.GetDefault(TextInputType.Url).Direction); }
    [Fact] public void NumericInputUsesNumericLayout() => Assert.Equal("numeric", new BuiltInKeyboardLayoutCatalog().GetDefault(TextInputType.Pin).Id);

    [Theory][InlineData("1234", true)][InlineData("123", false)][InlineData("123456789", false)][InlineData("12a4", false)] public void PinValidationIsStrict(string value, bool valid) => Assert.Equal(valid, new TextInputValidator().Validate(new() { Title = "PIN", InputType = TextInputType.Pin, IsSensitive = true, MaximumLength = 8, AllowEmpty = false }, value).IsValid);
    [Theory][InlineData("https://example.com", true)][InlineData("ftp://example.com", false)][InlineData("example.com", false)] public void UrlValidationIsSafe(string value, bool valid) => Assert.Equal(valid, new TextInputValidator().Validate(new() { Title = "URL", InputType = TextInputType.Url }, value).IsValid);
    [Fact] public void PathDisplayOnlyCannotSubmit() => Assert.False(new TextInputValidator().Validate(new() { Title = "Path", InputType = TextInputType.PathDisplayOnly }, "C:\\Games").IsValid);
    [Fact] public void EmptyValidationApplies() => Assert.False(new TextInputValidator().Validate(new() { Title = "Required", AllowEmpty = false }, string.Empty).IsValid);

    [Fact] public async Task SuggestionsAreLocalBoundedAndExcludePrivateGallery()
    {
        LocalSuggestionService service = new(new Source([new("Halo", LocalSuggestionKind.LibraryTitle), new("Holiday Private", LocalSuggestionKind.GalleryAlbum, true), new("Hades", LocalSuggestionKind.LibraryTitle)])); IReadOnlyList<string> result = await service.SuggestAsync("Ha", SuggestionsSource.AllLocal, 2); Assert.Equal(["Hades", "Halo"], result); Assert.DoesNotContain("Holiday Private", result);
    }

    [Fact] public async Task KeyboardServiceOpensClosesAndDisablesPinSuggestions()
    {
        Presenter presenter = new(); OnScreenKeyboardService service = new(presenter, new BuiltInKeyboardLayoutCatalog(), new TextInputValidator()); int states = 0; service.StateChanged += (_, _) => states++; TextInputResult result = await service.ShowAsync(new() { Title = "PIN", InputType = TextInputType.Pin, IsSensitive = true, SuggestionsSource = SuggestionsSource.AllLocal, MaximumLength = 8, AllowEmpty = false }); Assert.True(result.Submitted); Assert.Equal("1234", result.Text); Assert.False(service.IsOpen); Assert.Equal(2, states); Assert.Equal(SuggestionsSource.None, presenter.LastSession!.Request.SuggestionsSource);
    }

    [Fact] public async Task InvalidSubmissionIsNotReturned()
    {
        Presenter presenter = new("12"); OnScreenKeyboardService service = new(presenter, new BuiltInKeyboardLayoutCatalog(), new TextInputValidator()); TextInputResult result = await service.ShowAsync(new() { Title = "PIN", InputType = TextInputType.Pin, IsSensitive = true, MaximumLength = 8, AllowEmpty = false }); Assert.False(result.Submitted); Assert.NotNull(result.ValidationError);
    }

    private sealed class Source(IReadOnlyList<LocalSuggestionCandidate> candidates) : ILocalSuggestionSource { public Task<IReadOnlyList<LocalSuggestionCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(candidates); }
    private sealed class Presenter(string value = "1234") : ITextInputPresenter
    {
        public TextInputSession? LastSession { get; private set; }
        public Task<TextInputResult> PresentAsync(TextInputSession session, CancellationToken cancellationToken = default) { LastSession = session; session.Buffer.Insert(value); return Task.FromResult(new TextInputResult(true, session.Buffer.Text, TextInputCloseReason.Submitted)); }
        public Task CloseAsync(TextInputCloseReason reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
