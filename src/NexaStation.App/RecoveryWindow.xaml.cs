using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using NexaStation.Recovery;
using Windows.Storage;
using Windows.Storage.Pickers;
using NexaStation.Core;
using NexaStation.Input.Glyphs;

namespace NexaStation.App;

public sealed partial class RecoveryWindow : Window
{
    private readonly INexaStationPaths _paths;
    private readonly IDatabaseBackupService _backups;
    private readonly IDiagnosticsExportService _diagnostics;
    private readonly IStartupHealthService _startupHealth;
    private readonly string _appVersion;
    private bool _resetConfirmationArmed;
    private readonly IInputService _input;

    public RecoveryWindow(
        string problem,
        INexaStationPaths paths,
        IDatabaseBackupService backups,
        IDiagnosticsExportService diagnostics,
        IStartupHealthService startupHealth,
        string appVersion,
        IInputService input,
        IInputGlyphService glyphs)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _backups = backups ?? throw new ArgumentNullException(nameof(backups));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _startupHealth = startupHealth ?? throw new ArgumentNullException(nameof(startupHealth));
        _appVersion = appVersion;
        _input = input ?? throw new ArgumentNullException(nameof(input));
        InitializeComponent();
        InputHints.Configure(glyphs);
        _input.ActionTriggered += OnInputAction;
        Closed += OnClosed;
        ProblemText.Text = problem;
    }

    public event EventHandler? RestartRequested;

    private void RetryClicked(object sender, RoutedEventArgs e) => RequestRestart();

    private async void SafeModeClicked(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await _startupHealth.RequestSafeModeAsync(true);
            StatusText.Text = "Safe mode is ready. Restarting with providers and background indexing disabled.";
            RequestRestart();
        });
    }

    private async void BackupClicked(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            BackupResult result = await _backups.CreateAsync(_paths.Database, "manual-recovery");
            StatusText.Text = $"Backup created: {result.BackupPath}";
        });
    }

    private async void RestoreClicked(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".nxbackup");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        StorageFile? selected = await picker.PickSingleFileAsync();
        if (selected is null) return;
        await RunAsync(async () =>
        {
            await _backups.RestoreAsync(selected.Path, _paths.Database);
            await _startupHealth.RequestSafeModeAsync(false);
            StatusText.Text = "The backup passed validation and was restored. Restarting NexaStation.";
            RequestRestart();
        });
    }

    private async void ExportClicked(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            string path = await _diagnostics.ExportAsync(_appVersion, "Development", new());
            StatusText.Text = $"Redacted diagnostics exported: {path}";
        });
    }

    private async void ResetClicked(object sender, RoutedEventArgs e)
    {
        if (!_resetConfirmationArmed)
        {
            _resetConfirmationArmed = true;
            ResetButton.Content = "Confirm reset local library";
            StatusText.Text = "Select Confirm reset to preserve a backup and create a new empty local library.";
            return;
        }

        await RunAsync(async () =>
        {
            if (File.Exists(_paths.Database))
            {
                await _backups.CreateAsync(_paths.Database, "before-library-reset");
                string preservedPath = _paths.Database + $".reset-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
                File.Move(_paths.Database, preservedPath, false);
                DeleteIfExists(_paths.Database + "-wal");
                DeleteIfExists(_paths.Database + "-shm");
            }

            await _startupHealth.RequestSafeModeAsync(false);
            StatusText.Text = "The previous library was preserved and a clean library will be created.";
            RequestRestart();
        });
    }

    private void ExitClicked(object sender, RoutedEventArgs e) => Close();

    private void OnInputAction(object? sender, InputActionEventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (e.Action == InputAction.Cancel) { Close(); return; }
        FocusNavigationDirection? direction = e.Action switch { InputAction.NavigateUp => FocusNavigationDirection.Up, InputAction.NavigateDown => FocusNavigationDirection.Down, InputAction.NavigateLeft => FocusNavigationDirection.Left, InputAction.NavigateRight => FocusNavigationDirection.Right, _ => null };
        if (direction is not null) FocusManager.TryMoveFocus(direction.Value);
    });

    private void OnClosed(object sender, WindowEventArgs args) { _input.ActionTriggered -= OnInputAction; Closed -= OnClosed; }

    private async Task RunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Recovery action failed: {exception.Message}";
        }
    }

    private void RequestRestart() => RestartRequested?.Invoke(this, EventArgs.Empty);
    private static void DeleteIfExists(string path) { if (File.Exists(path)) File.Delete(path); }
}
