using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NexaStation.Core;
using NexaStation.MediaPlayer.Playback;
using NexaStation.Infrastructure;
using NexaStation.Input.Glyphs;
using Windows.System;

namespace NexaStation.App;

public sealed partial class NexaMediaPlayerPage : UserControl, IDisposable
{
    private INexaMediaPlayerService? _service;
    private IWindowsMediaPlayerAccessor? _engine;
    private WindowFullscreenService? _fullscreen;
    private MediaOpenRequest? _request;
    private DispatcherTimer? _hideTimer;
    private bool _updating;
    private bool _disposed;

    public NexaMediaPlayerPage() => InitializeComponent();
    public event EventHandler? CloseRequested;
    public event EventHandler? OpenWithRequested;
    public event EventHandler<int>? QueueMoveRequested;

    public void Configure(INexaMediaPlayerService service, IWindowsMediaPlayerAccessor engine, WindowFullscreenService fullscreen, IInputGlyphService glyphs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service)); _engine = engine ?? throw new ArgumentNullException(nameof(engine)); _fullscreen = fullscreen ?? throw new ArgumentNullException(nameof(fullscreen));
        VideoSurface.SetMediaPlayer(engine.MediaPlayer); _service.PlaybackChanged += OnPlaybackChanged;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) }; _hideTimer.Tick += OnHideTimer;
        ConfirmHint.Configure(glyphs,InputAction.Confirm,"Play/Pause",InputGlyphContext.MediaPlayer);CancelHint.Configure(glyphs,InputAction.Cancel,"Back",InputGlyphContext.MediaPlayer);ContextHint.Configure(glyphs,InputAction.ContextMenu,"Player actions",InputGlyphContext.MediaPlayer);OptionsHint.Configure(glyphs,InputAction.Options,"More Options",InputGlyphContext.MediaPlayer);DetailsHint.Configure(glyphs,InputAction.ToggleDetails,"Details",InputGlyphContext.MediaPlayer);
    }

    public async Task<MediaOpenResult> OpenAsync(MediaOpenRequest request, CancellationToken cancellationToken = default)
    {
        if (_service is null) throw new InvalidOperationException("The media player is not configured."); _request = request; TitleText.Text = request.PrivacyLevel == MediaPrivacyLevel.Private ? "Private video" : request.DisplayTitle; DetailText.Text = request.SourceType.ToString();
        MediaOpenResult result = await _service.OpenAsync(request, cancellationToken); Render(result.Snapshot); ShowControls(); if (result.Succeeded) PlayPauseButton.Focus(FocusState.Programmatic); return result;
    }

    public async Task HandleInputActionAsync(InputAction action)
    {
        if (_service is null) return; ShowControls();
        switch (action)
        {
            case InputAction.Cancel: CloseRequested?.Invoke(this, EventArgs.Empty); break;
            case InputAction.Confirm: if (FocusManager.GetFocusedElement(XamlRoot) is not Button) await TogglePlayAsync(); break;
            case InputAction.NavigateLeft when ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), Timeline): await _service.SeekAsync(_service.Current.Position - TimeSpan.FromSeconds(10)); break;
            case InputAction.NavigateRight when ReferenceEquals(FocusManager.GetFocusedElement(XamlRoot), Timeline): await _service.SeekAsync(_service.Current.Position + TimeSpan.FromSeconds(30)); break;
            case InputAction.PreviousPage: await _service.SeekAsync(_service.Current.Position - TimeSpan.FromSeconds(10)); break;
            case InputAction.NextPage: await _service.SeekAsync(_service.Current.Position + TimeSpan.FromSeconds(30)); break;
            case InputAction.ContextMenu: OpenWithRequested?.Invoke(this, EventArgs.Empty); break;
            case InputAction.Options: await ShowMoreAsync(); break;
            case InputAction.ToggleDetails: TopOverlay.Visibility = TopOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; break;
            case InputAction.NavigateUp: FocusManager.TryMoveFocus(FocusNavigationDirection.Up); break;
            case InputAction.NavigateDown: FocusManager.TryMoveFocus(FocusNavigationDirection.Down); break;
            case InputAction.NavigateLeft: FocusManager.TryMoveFocus(FocusNavigationDirection.Left); break;
            case InputAction.NavigateRight: FocusManager.TryMoveFocus(FocusNavigationDirection.Right); break;
        }
    }

    private void OnPlaybackChanged(object? sender, MediaPlaybackChangedEventArgs e) => DispatcherQueue.TryEnqueue(() => Render(e.Snapshot));
    private void Render(MediaPlaybackSnapshot snapshot)
    {
        _updating = true; Timeline.Maximum = Math.Max(1, snapshot.Duration.TotalSeconds); Timeline.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, Timeline.Maximum); PositionText.Text = Format(snapshot.Position); DurationText.Text = Format(snapshot.Duration); VolumeSlider.Value = snapshot.Volume; PlayPauseButton.Content = snapshot.State == MediaPlaybackState.Playing ? "Pause" : "Play"; MuteButton.Content = snapshot.Muted ? "Unmute" : "Mute"; ErrorOverlay.Visibility = snapshot.State == MediaPlaybackState.Failed ? Visibility.Visible : Visibility.Collapsed; ErrorText.Text = snapshot.ErrorMessage ?? string.Empty; VideoSurface.Stretch = snapshot.AspectRatio switch { MediaAspectRatioMode.Fill => Stretch.UniformToFill, MediaAspectRatioMode.Stretch => Stretch.Fill, MediaAspectRatioMode.Original => Stretch.None, _ => Stretch.Uniform }; _updating = false;
    }
    private void ShowControls() { TransportOverlay.Visibility = Visibility.Visible; _hideTimer?.Stop(); if (_service?.Current.State == MediaPlaybackState.Playing) _hideTimer?.Start(); }
    private void OnHideTimer(object? sender, object e) { _hideTimer?.Stop(); if (_service?.Current.State == MediaPlaybackState.Playing && FocusManager.GetFocusedElement(XamlRoot) is not Control) TransportOverlay.Visibility = Visibility.Collapsed; }
    private async Task TogglePlayAsync() { if (_service is null) return; if (_service.Current.State == MediaPlaybackState.Playing) await _service.PauseAsync(); else await _service.PlayAsync(); }
    private static string Format(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture) : value.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture);
    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await TogglePlayAsync();
    private async void Stop_Click(object sender, RoutedEventArgs e) { if (_service is not null) await _service.StopAsync(); }
    private async void SkipBack_Click(object sender, RoutedEventArgs e) { if (_service is not null) await _service.SeekAsync(_service.Current.Position - TimeSpan.FromSeconds(10)); }
    private async void SkipForward_Click(object sender, RoutedEventArgs e) { if (_service is not null) await _service.SeekAsync(_service.Current.Position + TimeSpan.FromSeconds(30)); }
    private async void Timeline_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) { if (!_updating && _service?.Current.CanSeek == true) await _service.SeekAsync(TimeSpan.FromSeconds(e.NewValue)); }
    private async void Volume_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) { if (!_updating && _service is not null) await _service.SetVolumeAsync(e.NewValue); }
    private async void Mute_Click(object sender, RoutedEventArgs e) { if (_service is not null) await _service.SetMutedAsync(!_service.Current.Muted); }
    private void Previous_Click(object sender, RoutedEventArgs e) => QueueMoveRequested?.Invoke(this, -1);
    private void Next_Click(object sender, RoutedEventArgs e) => QueueMoveRequested?.Invoke(this, 1);
    private void Back_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OpenWith_Click(object sender, RoutedEventArgs e) => OpenWithRequested?.Invoke(this, EventArgs.Empty);
    private async void Retry_Click(object sender, RoutedEventArgs e) { if (_request is not null) await OpenAsync(_request); }
    private async void Fullscreen_Click(object sender, RoutedEventArgs e) { if (_service is null || _fullscreen is null) return; await _fullscreen.ToggleFullscreenAsync(); await _service.SetFullscreenAsync(!_service.Current.Fullscreen); }
    private async void More_Click(object sender, RoutedEventArgs e) => await ShowMoreAsync();
    private async Task ShowMoreAsync()
    {
        if (_service is null) return; ComboBox speed = new() { Header = "Playback speed", ItemsSource = new[] { .5, .75, 1, 1.25, 1.5, 2 }, SelectedItem = _service.Current.PlaybackRate, MinWidth = 260 }; ComboBox aspect = new() { Header = "Aspect ratio", ItemsSource = Enum.GetValues<MediaAspectRatioMode>(), SelectedItem = _service.Current.AspectRatio, MinWidth = 260 }; StackPanel panel = new() { Spacing = 10 }; panel.Children.Add(speed); panel.Children.Add(aspect); ContentDialog dialog = new() { XamlRoot = XamlRoot, Title = "Player options", Content = panel, PrimaryButtonText = "Apply", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary }; if (await dialog.ShowAsync() == ContentDialogResult.Primary) { if (speed.SelectedItem is double rate) await _service.SetPlaybackRateAsync(rate); if (aspect.SelectedItem is MediaAspectRatioMode mode) await _service.SetAspectRatioAsync(mode); }
    }
    private async void Subtitles_Click(object sender, RoutedEventArgs e) { if (_service is null) return; MediaTrackDescriptor[] tracks = [new("off", "Off"), .. _service.Current.SubtitleTracks]; await ShowTrackPickerAsync("Subtitles", tracks, async id => await _service.SelectSubtitleTrackAsync(id == "off" ? null : id)); }
    private async void Audio_Click(object sender, RoutedEventArgs e) { if (_service is null) return; await ShowTrackPickerAsync("Audio tracks", _service.Current.AudioTracks, async id => await _service.SelectAudioTrackAsync(id)); }
    private async Task ShowTrackPickerAsync(string title, IReadOnlyList<MediaTrackDescriptor> tracks, Func<string, Task> select) { if (tracks.Count == 0) return; ListView list = new() { ItemsSource = tracks, DisplayMemberPath = "Label", SelectionMode = ListViewSelectionMode.Single, SelectedIndex = 0, MinWidth = 320 }; ContentDialog dialog = new() { XamlRoot = XamlRoot, Title = title, Content = list, PrimaryButtonText = "Select", CloseButtonText = "Cancel" }; if (await dialog.ShowAsync() == ContentDialogResult.Primary && list.SelectedItem is MediaTrackDescriptor track) await select(track.Id); }
    private void PlayerRoot_PointerMoved(object sender, PointerRoutedEventArgs e) => ShowControls();
    private async void PlayerRoot_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Escape) { e.Handled = true; CloseRequested?.Invoke(this, EventArgs.Empty); } else if (e.Key == VirtualKey.Space) { e.Handled = true; await TogglePlayAsync(); } }
    public void Dispose() { if (_disposed) return; _disposed = true; if (_service is not null) _service.PlaybackChanged -= OnPlaybackChanged; if (_hideTimer is not null) _hideTimer.Tick -= OnHideTimer; VideoSurface.SetMediaPlayer(null); _engine = null; }
}
