using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NexaStation.Core;
using NexaStation.Input.Glyphs;
using NexaStation.Onboarding;

namespace NexaStation.App;

public sealed partial class OnboardingWindow : Window, IDisposable
{
    private static readonly (string Eyebrow, string Title, string Description)[] Copy =
    [
        ("WELCOME", "Make NexaStation yours", "A short, local-only setup for your profile, appearance, games, applications, and media."),
        ("PROFILE & APPEARANCE", "Choose how it looks and feels", "Confirm a local owner identity, then use the recommended appearance or make deliberate choices."),
        ("GAMES & APPLICATIONS", "Choose what NexaStation may scan", "Select only the sources you want to set up. Anything left unselected is skipped."),
        ("GALLERY & SYSTEM", "Choose local folders and system behavior", "Enable only the options you want. Anything left unselected stays off."),
        ("REVIEW & APPLY", "Check everything before it changes", "Review the choices below, confirm them, and watch each setup task finish before entering Home."),
    ];

    private readonly OnboardingFlowService _flow;
    private readonly OnboardingSetupCoordinator _coordinator;
    private readonly IInputService _input;
    private readonly Dictionary<string, bool> _sourceReady = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _previewNames = [];
    private OnboardingState _state;
    private bool _loading;
    private bool _applying;
    private bool _applyFinished;
    private CancellationTokenSource? _applyCancellation;

    public OnboardingWindow(OnboardingFlowService flow, OnboardingSetupCoordinator coordinator, OnboardingState state, IInputService input, IInputGlyphService glyphs, string? recoveryNotice = null)
    {
        _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        InitializeComponent();
        InputHints.Configure(glyphs);
        _input.ActionTriggered += OnInputAction;
        Closed += OnClosed;
        if (!string.IsNullOrWhiteSpace(recoveryNotice)) ShowNotice(recoveryNotice);
        ApplyPreferences(_state.Preferences);
        Render();
    }

    public event EventHandler? SetupFinished;

    private async void NextClicked(object sender, RoutedEventArgs e)
    {
        if (_applying) return;
        try
        {
            _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
            if (_state.CurrentStep == OnboardingStep.ReviewAndApply)
            {
                if (_applyFinished)
                {
                    _state = await _flow.CompleteAsync(_state);
                    SetupFinished?.Invoke(this, EventArgs.Empty);
                    Close();
                    return;
                }
                await ApplySetupAsync();
                return;
            }
            if (_state.CurrentStep == OnboardingStep.GamesAndApplications && !SourcesCanContinue(out string sourceMessage))
            {
                ShowNotice(sourceMessage);
                UpdateNavigation();
                return;
            }
            _state = await _flow.MoveAsync(_state, 1);
            Render();
            if (_state.CurrentStep == OnboardingStep.GamesAndApplications) await ResumeSelectedScansAsync();
        }
        catch (Exception exception) { ShowNotice($"Setup could not continue: {exception.Message}"); }
    }

    private async Task ApplySetupAsync()
    {
        if (ApplyConfirmation.IsChecked != true) { ShowNotice("Confirm Apply these choices before setup begins."); return; }
        _applying = true;
        CancellationTokenSource applyCancellation = new();
        _applyCancellation = applyCancellation;
        ApplyProgress.IsActive = true;
        CancelApplyButton.Visibility = Visibility.Visible;
        ApplyStatus.Text = "Starting setup…";
        UpdateNavigation();
        Progress<string> progress = new(message => DispatcherQueue.TryEnqueue(() => ApplyStatus.Text = message));
        try
        {
            IReadOnlyList<OnboardingTaskResult> results = await _coordinator.ApplyAsync(_state.Preferences, progress, async result =>
            {
                List<OnboardingTaskResult> current = _state.Preferences.Results.Where(existing => !string.Equals(existing.Id, result.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                current.Add(result);
                _state = await _flow.SavePreferencesAsync(_state, _state.Preferences with { TaskResults = current });
                TaskResultsList.ItemsSource = current.Select(ResultText).ToArray();
            }, applyCancellation.Token);
            _state = await _flow.SavePreferencesAsync(_state, _state.Preferences with { TaskResults = results, ApplyConfirmed = true });
            TaskResultsList.ItemsSource = results.Select(ResultText).ToArray();
            bool failed = results.Any(result => result.Status == OnboardingTaskStatus.Failed);
            AcceptFailures.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
            _applyFinished = !failed;
            ApplyStatus.Text = failed ? "Setup finished with issues. Retry later in the relevant section, or accept them to continue." : "Setup complete. NexaStation is ready.";
            NextButton.Content = "Go to Home";
        }
        catch (OperationCanceledException) { ApplyStatus.Text = "Setup was cancelled. Completed tasks were kept safely."; }
        finally { _applying = false; ApplyProgress.IsActive = false; CancelApplyButton.Visibility = Visibility.Collapsed; applyCancellation.Dispose(); if (ReferenceEquals(_applyCancellation, applyCancellation)) _applyCancellation = null; UpdateNavigation(); }
    }

    private void CancelApplyClicked(object sender, RoutedEventArgs e) => _applyCancellation?.Cancel();

    private async void BackClicked(object sender, RoutedEventArgs e)
    {
        if (_applying) return;
        try { _state = await _flow.SavePreferencesAsync(_state, ReadPreferences()); _state = await _flow.MoveAsync(_state, -1); Render(); }
        catch (Exception exception) { ShowNotice(exception.Message); }
    }

    private async void SkipClicked(object sender, RoutedEventArgs e)
    {
        if (_applying) return;
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
        _state = await _flow.SkipAsync(_state);
        SetupFinished?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ExitClicked(object sender, RoutedEventArgs e) => Close();

    private async void RecommendedAppearanceClicked(object sender, RoutedEventArgs e)
    {
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences() with
        {
            AppearanceDecision = OnboardingDecisionState.OptedOut,
            Layout = "Modern", ThemeId = "nexa-dark", PerformanceProfile = "Balanced",
        });
        CustomAppearancePanel.Visibility = Visibility.Collapsed;
        UpdateNavigation();
    }

    private async void CustomizeAppearanceClicked(object sender, RoutedEventArgs e)
    {
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences() with { AppearanceDecision = OnboardingDecisionState.OptedIn });
        CustomAppearancePanel.Visibility = Visibility.Visible;
        UpdateNavigation();
    }

    private async void KeepAppearanceClicked(object sender, RoutedEventArgs e)
    {
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences() with { AppearanceDecision = OnboardingDecisionState.KeepExisting });
        CustomAppearancePanel.Visibility = Visibility.Collapsed;
        UpdateNavigation();
    }

    private async void SourceDecisionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        string[] parts = tag.Split(':'); string id = parts[0]; bool optedIn = parts[1] == "in";
        OnboardingDecisionState decision = parts[1] == "keep" ? OnboardingDecisionState.KeepExisting : optedIn ? OnboardingDecisionState.OptedIn : OnboardingDecisionState.OptedOut;
        OnboardingPreferences preferences = ReadPreferences();
        preferences = id switch
        {
            "steam" => preferences with { SteamDecision = decision, SteamImportRequested = optedIn },
            "launchers" => preferences with { OtherLaunchersDecision = decision, OtherLaunchersImportRequested = optedIn },
            "windows" => preferences with { WindowsAppsDecision = decision, WindowsAppScanRequested = optedIn },
            "emulation" => preferences with { EmulationDecision = decision, EmulationSetupRequested = optedIn },
            _ => preferences,
        };
        _state = await _flow.SavePreferencesAsync(_state, preferences);
        if (decision != OnboardingDecisionState.OptedIn)
        {
            _sourceReady[id] = true;
            SetSourceStatus(id, decision == OnboardingDecisionState.KeepExisting ? "Keep existing selected. Nothing will be rescanned." : "This source will not be scanned or imported.");
        }
        else await ScanSourceAsync(id);
        UpdateNavigation();
    }

    private async Task ScanSourceAsync(string id)
    {
        _sourceReady[id] = false;
        SetSourceStatus(id, "Scanning approved local locations…");
        UpdateNavigation();
        OnboardingSourcePreview preview = await _coordinator.PreviewAsync(id);
        _sourceReady[id] = preview.Succeeded;
        _previewNames = preview.CandidateNames;
        SourcePreviewList.ItemsSource = _previewNames;
        string names = preview.CandidateNames.Count == 0 ? string.Empty : $" Preview: {string.Join(", ", preview.CandidateNames.Take(5))}{(preview.CandidateNames.Count > 5 ? "…" : string.Empty)}";
        SetSourceStatus(id, preview.Message + names + (preview.Succeeded ? string.Empty : " Retry Set up or choose not to scan this source."));
        UpdateNavigation();
    }

    private void SourceSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string term = sender.Text.Trim();
        SourcePreviewList.ItemsSource = term.Length == 0 ? _previewNames : _previewNames.Where(name => name.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private async Task ResumeSelectedScansAsync()
    {
        OnboardingPreferences p = _state.Preferences;
        foreach ((string id, OnboardingDecisionState decision) in new[] { ("steam", p.SteamDecision), ("launchers", p.OtherLaunchersDecision), ("windows", p.WindowsAppsDecision), ("emulation", p.EmulationDecision) })
        {
            if (decision == OnboardingDecisionState.OptedIn && !_sourceReady.ContainsKey(id)) await ScanSourceAsync(id);
            else if (decision is OnboardingDecisionState.OptedOut or OnboardingDecisionState.KeepExisting) { _sourceReady[id] = true; SetSourceStatus(id, decision == OnboardingDecisionState.KeepExisting ? "Keep existing selected." : "This source will not be scanned."); }
        }
    }

    private async void SystemDecisionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        string[] parts = tag.Split(':'); string id = parts[0]; bool optedIn = parts[1] == "in";
        OnboardingDecisionState decision = parts[1] == "keep" ? OnboardingDecisionState.KeepExisting : optedIn ? OnboardingDecisionState.OptedIn : OnboardingDecisionState.OptedOut;
        OnboardingPreferences p = ReadPreferences();
        p = id switch
        {
            "gallery" => p with { GalleryDecision = decision, GallerySetupRequested = decision == OnboardingDecisionState.KeepExisting ? p.GallerySetupRequested : optedIn, PicturesDecision = decision == OnboardingDecisionState.KeepExisting ? OnboardingDecisionState.KeepExisting : optedIn ? p.PicturesDecision : OnboardingDecisionState.OptedOut, IncludePicturesFolder = decision == OnboardingDecisionState.KeepExisting ? p.IncludePicturesFolder : optedIn && p.IncludePicturesFolder },
            "pictures" => p with { PicturesDecision = decision, IncludePicturesFolder = optedIn },
            "startup" => p with { StartupDecision = decision, StartWithWindows = decision == OnboardingDecisionState.KeepExisting ? p.StartWithWindows : optedIn },
            "diagnostics" => p with { DiagnosticsDecision = decision, DiagnosticsConsent = decision == OnboardingDecisionState.KeepExisting ? p.DiagnosticsConsent : optedIn },
            _ => p,
        };
        _state = await _flow.SavePreferencesAsync(_state, p);
        ApplyPreferences(p);
        UpdateSystemSummary();
        UpdateNavigation();
    }

    private async void PreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _state.Status != OnboardingStatus.InProgress) return;
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences());
        UpdateNavigation();
    }

    private async void ApplyConfirmationChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _applying) return;
        if (AcceptFailures.IsChecked == true) _applyFinished = true;
        _state = await _flow.SavePreferencesAsync(_state, ReadPreferences() with { ApplyConfirmed = ApplyConfirmation.IsChecked == true });
        UpdateNavigation();
    }

    private void Render()
    {
        int index = (int)_state.CurrentStep;
        ProgressBar.Value = index + 1;
        ProgressText.Text = $"{index + 1} of {Copy.Length}";
        StepEyebrow.Text = Copy[index].Eyebrow;
        StepTitle.Text = Copy[index].Title;
        StepDescription.Text = Copy[index].Description;
        WelcomePanel.Visibility = _state.CurrentStep == OnboardingStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        ProfilePanel.Visibility = _state.CurrentStep == OnboardingStep.ProfileAndAppearance ? Visibility.Visible : Visibility.Collapsed;
        SourcesPanel.Visibility = _state.CurrentStep == OnboardingStep.GamesAndApplications ? Visibility.Visible : Visibility.Collapsed;
        GalleryPanel.Visibility = _state.CurrentStep == OnboardingStep.GalleryAndSystem ? Visibility.Visible : Visibility.Collapsed;
        ReviewPanel.Visibility = _state.CurrentStep == OnboardingStep.ReviewAndApply ? Visibility.Visible : Visibility.Collapsed;
        if (_state.CurrentStep == OnboardingStep.ReviewAndApply) RenderReview();
        RecoveryNotice.Visibility = Visibility.Collapsed;
        UpdateSystemSummary();
        UpdateNavigation();
        NextButton.Focus(FocusState.Programmatic);
    }

    private void RenderReview()
    {
        OnboardingPreferences p = ReadPreferences();
        ReviewSummary.Text = $"Owner: {p.OwnerName} · Avatar: {p.AvatarKey}\nAppearance: {AppearanceDecision(p.AppearanceDecision)} · {p.Layout} · {p.ThemeId} · {p.PerformanceProfile}\nSteam: {SourceDecision(p.SteamDecision)} · Other launchers: {SourceDecision(p.OtherLaunchersDecision)}\nWindows apps: {SourceDecision(p.WindowsAppsDecision)} · Emulation: {SourceDecision(p.EmulationDecision)}\nGallery: {SystemDecision(p.GalleryDecision)} · Pictures: {SystemDecision(p.PicturesDecision)}\nLaunch at sign-in: {SystemDecision(p.StartupDecision)} · Diagnostic logs: {SystemDecision(p.DiagnosticsDecision)}";
        ApplyConfirmation.IsChecked = p.ApplyConfirmed;
        if (p.Results.Count > 0) TaskResultsList.ItemsSource = p.Results.Select(ResultText).ToArray();
    }

    private void UpdateNavigation()
    {
        OnboardingValidationResult validation = OnboardingFlowService.Validate(_state with { Preferences = ReadPreferences() });
        bool canContinue = validation.CanContinue && !_applying;
        string message = validation.AccessibleMessage;
        if (_state.CurrentStep == OnboardingStep.GamesAndApplications && !SourcesCanContinue(out string sourceMessage)) { canContinue = false; message = sourceMessage; }
        if (_state.CurrentStep == OnboardingStep.ReviewAndApply)
        {
            if (_applyFinished) canContinue = AcceptFailures.Visibility != Visibility.Visible || AcceptFailures.IsChecked == true;
            else canContinue = ApplyConfirmation.IsChecked == true && !_applying;
        }
        NextButton.IsEnabled = canContinue;
        BackButton.IsEnabled = (int)_state.CurrentStep > 0 && !_applying;
        SkipButton.Visibility = _state.CurrentStep == OnboardingStep.Welcome ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = _state.CurrentStep switch { OnboardingStep.Welcome => "Start setup", OnboardingStep.ReviewAndApply when _applyFinished => "Go to Home", OnboardingStep.ReviewAndApply => "Apply choices", _ => "Continue" };
        ValidationMessage.Text = canContinue ? string.Empty : message;
        AutomationProperties.SetHelpText(NextButton, canContinue ? "Continue to the next setup screen." : message);
    }

    private bool SourcesCanContinue(out string message)
    {
        OnboardingPreferences p = ReadPreferences();
        List<string> pending = [];
        Add("Steam", "steam", p.SteamDecision); Add("other launchers", "launchers", p.OtherLaunchersDecision); Add("Windows applications", "windows", p.WindowsAppsDecision); Add("emulation", "emulation", p.EmulationDecision);
        message = pending.Count == 0 ? "All selected scans are ready." : $"Wait for the selected scans: {string.Join(", ", pending)}.";
        return pending.Count == 0;
        void Add(string name, string id, OnboardingDecisionState decision) { if (decision == OnboardingDecisionState.OptedIn && (!_sourceReady.TryGetValue(id, out bool ready) || !ready)) pending.Add(name); }
    }

    private OnboardingPreferences ReadPreferences()
    {
        OnboardingPreferences p = _state.Preferences;
        return p with
        {
            OwnerName = OwnerNameBox.Text.Trim(), AvatarKey = SelectedTag(AvatarChoice, p.AvatarKey),
            Layout = SelectedTag(LayoutChoice, p.Layout), ThemeId = SelectedTag(ThemeChoice, p.ThemeId), PerformanceProfile = SelectedTag(PerformanceChoice, p.PerformanceProfile),
            PicturesPrivate = PicturesPrivateChoice.IsChecked == true, ShowPicturesOnHome = PicturesHomeChoice.IsChecked == true,
            PicturesIncludeSubfolders = PicturesSubfoldersChoice.IsChecked == true, ApplyConfirmed = ApplyConfirmation.IsChecked == true,
        };
    }

    private void ApplyPreferences(OnboardingPreferences p)
    {
        _loading = true;
        OwnerNameBox.Text = p.OwnerName; SelectTag(AvatarChoice, p.AvatarKey); SelectTag(LayoutChoice, p.Layout); SelectTag(ThemeChoice, p.ThemeId); SelectTag(PerformanceChoice, p.PerformanceProfile);
        PicturesPrivateChoice.IsChecked = p.PicturesPrivate; PicturesHomeChoice.IsChecked = p.ShowPicturesOnHome; PicturesSubfoldersChoice.IsChecked = p.PicturesIncludeSubfolders;
        CustomAppearancePanel.Visibility = p.AppearanceDecision == OnboardingDecisionState.OptedIn ? Visibility.Visible : Visibility.Collapsed;
        Visibility upgrade = _state.IsUpgrade ? Visibility.Visible : Visibility.Collapsed;
        KeepAppearanceButton.Visibility = upgrade; KeepSteam.Visibility = upgrade; KeepLaunchers.Visibility = upgrade; KeepWindows.Visibility = upgrade; KeepEmulation.Visibility = upgrade; KeepGallery.Visibility = upgrade; KeepStartup.Visibility = upgrade; KeepDiagnostics.Visibility = upgrade;
        PicturesDecisionPanel.Visibility = p.GalleryDecision == OnboardingDecisionState.OptedOut ? Visibility.Collapsed : Visibility.Visible;
        GalleryOptionsPanel.Visibility = p.GalleryDecision == OnboardingDecisionState.OptedIn && p.PicturesDecision == OnboardingDecisionState.OptedIn ? Visibility.Visible : Visibility.Collapsed;
        ApplyConfirmation.IsChecked = p.ApplyConfirmed;
        _loading = false;
    }

    private void UpdateSystemSummary()
    {
        OnboardingPreferences p = _state.Preferences;
        SystemDecisionSummary.Text = $"Gallery: {SystemDecision(p.GalleryDecision)} · Pictures: {SystemDecision(p.PicturesDecision)} · Startup: {SystemDecision(p.StartupDecision)} · Diagnostic logs: {SystemDecision(p.DiagnosticsDecision)}";
    }

    private void SetSourceStatus(string id, string text)
    {
        TextBlock block = id switch { "steam" => SteamStatus, "launchers" => LaunchersStatus, "windows" => WindowsStatus, _ => EmulationStatus };
        block.Text = text;
    }

    private void ShowNotice(string message) { RecoveryNotice.Text = message; RecoveryNotice.Visibility = Visibility.Visible; }
    private static string SelectedTag(ComboBox box, string fallback) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
    private static void SelectTag(ComboBox box, string tag) => box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) ?? box.Items.FirstOrDefault();
    private static string AppearanceDecision(OnboardingDecisionState value) => value switch { OnboardingDecisionState.OptedIn => "Customized", OnboardingDecisionState.OptedOut => "Recommended", OnboardingDecisionState.KeepExisting => "Keep existing", _ => "No choice" };
    private static string SourceDecision(OnboardingDecisionState value) => value switch { OnboardingDecisionState.OptedIn => "Set up", OnboardingDecisionState.OptedOut => "Not scanned", OnboardingDecisionState.KeepExisting => "Keep existing", _ => "No choice" };
    private static string SystemDecision(OnboardingDecisionState value) => value switch { OnboardingDecisionState.OptedIn => "Enabled", OnboardingDecisionState.OptedOut => "Disabled", OnboardingDecisionState.KeepExisting => "Keep existing", _ => "No choice" };
    private static string ResultText(OnboardingTaskResult result) => result.Status == OnboardingTaskStatus.Failed ? $"{result.Id}: failed · {result.Error}" : $"{result.Id}: {result.Status} · {result.Summary}";

    private void OnInputAction(object? sender, InputActionEventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (e.Action == InputAction.Cancel) { if ((int)_state.CurrentStep > 0) BackClicked(this, new RoutedEventArgs()); else Close(); return; }
        FocusNavigationDirection? direction = e.Action switch { InputAction.NavigateUp => FocusNavigationDirection.Up, InputAction.NavigateDown => FocusNavigationDirection.Down, InputAction.NavigateLeft => FocusNavigationDirection.Left, InputAction.NavigateRight => FocusNavigationDirection.Right, _ => null };
        if (direction is not null) FocusManager.TryMoveFocus(direction.Value);
    });

    public void Dispose() { _applyCancellation?.Cancel(); _applyCancellation?.Dispose(); _applyCancellation = null; }
    private void OnClosed(object sender, WindowEventArgs args) { _input.ActionTriggered -= OnInputAction; Closed -= OnClosed; Dispose(); }
}
