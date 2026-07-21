using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NexaStation.Onboarding;
using NexaStation.SystemIntegration.Services;
using NexaStation.Core;
using NexaStation.Input.Glyphs;

namespace NexaStation.App;

public sealed partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Description)[] Copy =
    [
        ("Welcome to NexaStation", "Set up a private, controller-first home for the games, applications, and local media already on this PC. Nothing is uploaded automatically."),
        ("Choose your dashboard", "Pick the presentation that feels right. You can change the layout, theme, density, and sounds later in Settings."),
        ("Balance quality and speed", "Balanced is recommended. The efficient profile reduces artwork memory and motion on lower-resource systems."),
        ("Choose how you navigate", "Controller navigation is enabled by default. Keyboard and mouse remain available at all times."),
        ("Connect game providers", "NexaStation can import your local Steam library and organize emulator entries. These steps remain optional."),
        ("Find installed applications", "Scan local shortcuts and the Windows Apps catalogue. Results stay on this device and can be hidden or edited."),
        ("Bring in local media", "Gallery indexing is opt-in and uses only folders you choose. Original media is never copied or uploaded."),
        ("Start with Windows", "This packaged startup task is off by default. Windows may require you to confirm the choice and can disable it later."),
        ("Privacy and diagnostics", "NexaStation has no mandatory telemetry. Diagnostics are created only when you export them and are redacted before packaging."),
        ("Review your setup", "Confirm these choices. Provider imports and folder selection continue inside the relevant NexaStation sections."),
    ];

    private readonly OnboardingFlowService _flow;
    private readonly IStartupTaskService _startupTask;
    private OnboardingState _state;
    private readonly IInputService _input;

    public OnboardingWindow(OnboardingFlowService flow, IStartupTaskService startupTask, OnboardingState state, IInputService input, IInputGlyphService glyphs, string? recoveryNotice = null)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _startupTask = startupTask ?? throw new ArgumentNullException(nameof(startupTask));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        InitializeComponent();
        InputHints.Configure(glyphs);
        _input.ActionTriggered += OnInputAction;
        Closed += OnClosed;
        if (!string.IsNullOrWhiteSpace(recoveryNotice))
        {
            RecoveryNotice.Text = recoveryNotice;
            RecoveryNotice.Visibility = Visibility.Visible;
        }

        ApplyPreferences(state.Preferences);
        Render();
    }

    public event EventHandler? SetupFinished;

    private async void NextClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
            if (_state.CurrentStep == OnboardingStep.Startup)
            {
                StartupTaskStatus status = await _startupTask.SetEnabledAsync(_state.Preferences.StartWithWindows);
                if (_state.Preferences.StartWithWindows && status.Availability != StartupTaskAvailability.Enabled)
                {
                    RecoveryNotice.Text = status.Message;
                    RecoveryNotice.Visibility = Visibility.Visible;
                }
            }

            if (_state.CurrentStep == OnboardingStep.Review)
            {
                _state = await _flow.CompleteAsync(_state);
                SetupFinished?.Invoke(this, EventArgs.Empty);
                Close();
                return;
            }

            _state = await _flow.MoveAsync(_state, 1);
            Render();
        }
        catch (Exception exception)
        {
            RecoveryNotice.Text = $"Setup could not save this step: {exception.Message}";
            RecoveryNotice.Visibility = Visibility.Visible;
        }
    }

    private async void BackClicked(object sender, RoutedEventArgs e)
    {
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
        _state = await _flow.MoveAsync(_state, -1);
        Render();
    }

    private async void SkipClicked(object sender, RoutedEventArgs e)
    {
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
        _state = await _flow.SkipAsync(_state);
        SetupFinished?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ExitClicked(object sender, RoutedEventArgs e) => Close();

    private void OnInputAction(object? sender, InputActionEventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (e.Action == InputAction.Cancel) { if ((int)_state.CurrentStep > 0) BackClicked(this, new RoutedEventArgs()); else Close(); return; }
        FocusNavigationDirection? direction = e.Action switch { InputAction.NavigateUp => FocusNavigationDirection.Up, InputAction.NavigateDown => FocusNavigationDirection.Down, InputAction.NavigateLeft => FocusNavigationDirection.Left, InputAction.NavigateRight => FocusNavigationDirection.Right, _ => null };
        if (direction is not null) FocusManager.TryMoveFocus(direction.Value);
    });

    private void OnClosed(object sender, WindowEventArgs args) { _input.ActionTriggered -= OnInputAction; Closed -= OnClosed; }

    private void Render()
    {
        int index = (int)_state.CurrentStep;
        ProgressBar.Value = index + 1;
        ProgressText.Text = $"Step {index + 1} of {Copy.Length}";
        StepTitle.Text = Copy[index].Title;
        StepDescription.Text = Copy[index].Description;
        BackButton.IsEnabled = index > 0;
        NextButton.Content = index == Copy.Length - 1 ? "Finish" : "Continue";
        SetChoiceVisibility(_state.CurrentStep);
        if (_state.CurrentStep == OnboardingStep.Review)
        {
            OnboardingPreferences preferences = ReadPreferences();
            ReviewSummary.Text = $"{preferences.Layout} layout • {preferences.PerformanceProfile} performance\nController: {YesNo(preferences.ControllerEnabled)} • Windows apps: {YesNo(preferences.WindowsAppScanRequested)}\nSteam guide: {YesNo(preferences.SteamImportRequested)} • Emulator guide: {YesNo(preferences.EmulationSetupRequested)}\nGallery guide: {YesNo(preferences.GallerySetupRequested)} • Start with Windows: {YesNo(preferences.StartWithWindows)}";
        }

        NextButton.Focus(FocusState.Programmatic);
    }

    private void SetChoiceVisibility(OnboardingStep step)
    {
        LayoutChoice.Visibility = step == OnboardingStep.Layout ? Visibility.Visible : Visibility.Collapsed;
        PerformanceChoice.Visibility = step == OnboardingStep.Performance ? Visibility.Visible : Visibility.Collapsed;
        ControllerChoice.Visibility = step == OnboardingStep.Input ? Visibility.Visible : Visibility.Collapsed;
        SteamChoice.Visibility = EmulationChoice.Visibility = step == OnboardingStep.Providers ? Visibility.Visible : Visibility.Collapsed;
        WindowsAppsChoice.Visibility = step == OnboardingStep.Applications ? Visibility.Visible : Visibility.Collapsed;
        GalleryChoice.Visibility = PicturesPrivateChoice.Visibility = PicturesHomeChoice.Visibility = PicturesSubfoldersChoice.Visibility = step == OnboardingStep.Gallery ? Visibility.Visible : Visibility.Collapsed;
        StartupChoice.Visibility = step == OnboardingStep.Startup ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsChoice.Visibility = step == OnboardingStep.Privacy ? Visibility.Visible : Visibility.Collapsed;
        ReviewSummary.Visibility = step == OnboardingStep.Review ? Visibility.Visible : Visibility.Collapsed;
    }

    private OnboardingPreferences ReadPreferences() => new(
        SelectedTag(LayoutChoice),
        SelectedTag(PerformanceChoice),
        ControllerChoice.IsChecked == true,
        SteamChoice.IsChecked == true,
        EmulationChoice.IsChecked == true,
        WindowsAppsChoice.IsChecked == true,
        GalleryChoice.IsChecked == true,
        GalleryChoice.IsChecked == true,
        PicturesPrivateChoice.IsChecked == true,
        PicturesHomeChoice.IsChecked == true,
        PicturesSubfoldersChoice.IsChecked == true,
        StartupChoice.IsChecked == true,
        DiagnosticsChoice.IsChecked == true);

    private void ApplyPreferences(OnboardingPreferences preferences)
    {
        SelectTag(LayoutChoice, preferences.Layout);
        SelectTag(PerformanceChoice, preferences.PerformanceProfile);
        ControllerChoice.IsChecked = preferences.ControllerEnabled;
        SteamChoice.IsChecked = preferences.SteamImportRequested;
        EmulationChoice.IsChecked = preferences.EmulationSetupRequested;
        WindowsAppsChoice.IsChecked = preferences.WindowsAppScanRequested;
        GalleryChoice.IsChecked = preferences.GallerySetupRequested;
        GalleryChoice.IsChecked = preferences.IncludePicturesFolder;
        PicturesPrivateChoice.IsChecked = preferences.PicturesPrivate;
        PicturesHomeChoice.IsChecked = preferences.ShowPicturesOnHome;
        PicturesSubfoldersChoice.IsChecked = preferences.PicturesIncludeSubfolders;
        StartupChoice.IsChecked = preferences.StartWithWindows;
        DiagnosticsChoice.IsChecked = preferences.DiagnosticsConsent;
    }

    private static string SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    private static void SelectTag(ComboBox comboBox, string tag) => comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) ?? comboBox.Items[0];
    private static string YesNo(bool value) => value ? "Yes" : "No";
}
