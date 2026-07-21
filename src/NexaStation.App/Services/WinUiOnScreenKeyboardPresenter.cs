using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NexaStation.TextInput.Keyboard;
using NexaStation.TextInput.Sessions;
using NexaStation.TextInput.Suggestions;
using NexaStation.TextInput.Validation;

namespace NexaStation.App.Services;

public sealed class WinUiOnScreenKeyboardPresenter : ITextInputPresenter
{
    private readonly Func<Window?> _window;
    private readonly IKeyboardLayoutCatalog _layouts;
    private readonly ILocalSuggestionService _suggestions;
    private readonly TextInputValidator _validator;
    private ContentDialog? _activeDialog;

    public WinUiOnScreenKeyboardPresenter(Func<Window?> window, IKeyboardLayoutCatalog layouts, ILocalSuggestionService suggestions, TextInputValidator validator)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window)); _layouts = layouts ?? throw new ArgumentNullException(nameof(layouts)); _suggestions = suggestions ?? throw new ArgumentNullException(nameof(suggestions)); _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<TextInputResult> PresentAsync(TextInputSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        Window window = _window() ?? throw new InvalidOperationException("The NexaStation window is not ready for text entry.");
        XamlRoot root = (window.Content as FrameworkElement)?.XamlRoot ?? throw new InvalidOperationException("The NexaStation visual root is not ready for text entry.");
        TextBox? textBox = null; PasswordBox? passwordBox = null;
        if (session.Request.IsSensitive || session.Request.InputType == TextInputType.Pin) passwordBox = new() { Password = session.Buffer.Text, PlaceholderText = session.Request.Placeholder ?? string.Empty, MaxLength = session.Request.MaximumLength, MinHeight = 52 };
        else textBox = new() { Text = session.Buffer.Text, PlaceholderText = session.Request.Placeholder ?? string.Empty, MaxLength = session.Request.MaximumLength, MinHeight = 52, FlowDirection = session.Request.InputType == TextInputType.Url ? FlowDirection.LeftToRight : session.Layout.Direction == KeyboardTextDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };

        TextBlock validation = new() { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NexaWarningBrush"], TextWrapping = TextWrapping.Wrap };
        StackPanel suggestions = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        StackPanel keyRows = new() { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        ComboBox layoutSelector = new() { Header = "Keyboard layout", ItemsSource = _layouts.Layouts, DisplayMemberPath = "DisplayName", SelectedItem = session.Layout, MinWidth = 240 };
        Grid content = new() { MinWidth = 760, MaxWidth = 1180, RowSpacing = 10 };
        content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
        if (!string.IsNullOrWhiteSpace(session.Request.Description)) { TextBlock description = new() { Text = session.Request.Description, TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["NexaMutedTextBrush"] }; content.Children.Add(description); Grid.SetRow(description, 0); }
        Control editor = (Control?)passwordBox ?? textBox!; content.Children.Add(editor); Grid.SetRow(editor, 1); content.Children.Add(suggestions); Grid.SetRow(suggestions, 2); content.Children.Add(keyRows); Grid.SetRow(keyRows, 3); StackPanel tools = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center }; tools.Children.Add(layoutSelector); content.Children.Add(tools); Grid.SetRow(tools, 4); content.Children.Add(validation); Grid.SetRow(validation, 5); content.RowDefinitions.Add(new() { Height = GridLength.Auto });

        string CurrentText() => passwordBox?.Password ?? textBox?.Text ?? string.Empty;
        void SetText(string value)
        {
            if (passwordBox is not null) passwordBox.Password = value; else if (textBox is not null) { textBox.Text = value; textBox.SelectionStart = session.Buffer.Cursor; }
        }
        void SyncBuffer()
        {
            string value = CurrentText(); session.Buffer.Clear(); session.Buffer.Insert(value);
            if (textBox is not null) session.Buffer.MoveCursor(textBox.SelectionStart - session.Buffer.Length);
        }
        void RenderKeys()
        {
            keyRows.Children.Clear(); IReadOnlyList<KeyboardRow> rows = session.Shifted && session.Layout.ShiftLayer.Count > 0 ? session.Layout.ShiftLayer : session.Layout.Rows;
            foreach (KeyboardRow row in rows)
            {
                StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = session.Layout.Direction == KeyboardTextDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
                foreach (KeyboardKey key in row.Keys)
                {
                    Button button = new() { Content = key.Label, Tag = key, MinWidth = 48 * key.Width, MinHeight = 48, FontSize = 18 };
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, key.AccessibilityLabel ?? key.Label); button.Click += (_, _) => { SyncBuffer(); session.Buffer.Insert(session.Shifted && key.ShiftValue is not null ? key.ShiftValue : key.Value); SetText(session.Buffer.Text); };
                    panel.Children.Add(button);
                }
                keyRows.Children.Add(panel);
            }
            StackPanel actions = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            AddAction(actions, "Backspace", () => { SyncBuffer(); session.Buffer.Backspace(); SetText(session.Buffer.Text); });
            AddAction(actions, "Shift", () => { session.ToggleShift(); RenderKeys(); });
            AddAction(actions, "Space", () => { SyncBuffer(); session.Buffer.Insert(" "); SetText(session.Buffer.Text); }, 180);
            AddAction(actions, "◀", () => { SyncBuffer(); session.Buffer.MoveCursor(-1); SetText(session.Buffer.Text); });
            AddAction(actions, "▶", () => { SyncBuffer(); session.Buffer.MoveCursor(1); SetText(session.Buffer.Text); });
            AddAction(actions, "Clear", () => { session.Buffer.Clear(); SetText(string.Empty); });
            keyRows.Children.Add(actions);
        }
        static void AddAction(Panel panel, string label, Action action, double width = 110) { Button button = new() { Content = label, MinWidth = width, MinHeight = 48 }; button.Click += (_, _) => action(); panel.Children.Add(button); }

        CancellationTokenSource suggestionCancellation = new();
        async Task RefreshSuggestionsAsync()
        {
            if (session.Request.IsSensitive || session.Request.SuggestionsSource == SuggestionsSource.None) { suggestions.Children.Clear(); return; }
            suggestionCancellation.Cancel(); suggestionCancellation.Dispose(); suggestionCancellation = new(); CancellationToken token = suggestionCancellation.Token;
            try
            {
                await Task.Delay(140, token); IReadOnlyList<string> values = await _suggestions.SuggestAsync(CurrentText(), session.Request.SuggestionsSource, 8, token); suggestions.Children.Clear();
                foreach (string value in values) { Button button = new() { Content = value, MinHeight = 40 }; button.Click += (_, _) => { session.Buffer.Clear(); session.Buffer.Insert(value); SetText(value); }; suggestions.Children.Add(button); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
        if (textBox is not null) textBox.TextChanged += (_, _) => _ = RefreshSuggestionsAsync();
        layoutSelector.SelectionChanged += (_, _) => { if (layoutSelector.SelectedItem is KeyboardLayout layout) { session.SetLayout(layout); if (textBox is not null && session.Request.InputType != TextInputType.Url) textBox.FlowDirection = layout.Direction == KeyboardTextDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; RenderKeys(); } };
        RenderKeys(); await RefreshSuggestionsAsync();

        ContentDialog dialog = new() { XamlRoot = root, Title = session.Request.Title, Content = new ScrollViewer { Content = content, MaxHeight = 690 }, PrimaryButtonText = session.Request.SubmitLabel, CloseButtonText = session.Request.CancelLabel, DefaultButton = ContentDialogButton.Primary };
        dialog.Closing += (_, args) => { if (args.Result != ContentDialogResult.Primary) return; SyncBuffer(); TextInputValidationResult result = _validator.Validate(session.Request, session.Buffer.Text); if (!result.IsValid) { args.Cancel = true; validation.Text = result.Error; } };
        _activeDialog = dialog;
        using CancellationTokenRegistration registration = cancellationToken.Register(() => window.DispatcherQueue.TryEnqueue(dialog.Hide));
        try
        {
            ContentDialogResult result = await dialog.ShowAsync(); SyncBuffer(); return result == ContentDialogResult.Primary ? new(true, session.Buffer.Text, TextInputCloseReason.Submitted) : TextInputResult.Cancelled();
        }
        finally { suggestionCancellation.Cancel(); suggestionCancellation.Dispose(); if (ReferenceEquals(_activeDialog, dialog)) _activeDialog = null; }
    }

    public Task CloseAsync(TextInputCloseReason reason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _activeDialog?.Hide(); return Task.CompletedTask;
    }
}
