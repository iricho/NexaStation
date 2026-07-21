using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using NexaStation.Core;

namespace NexaStation.Infrastructure;

public sealed class WindowFullscreenService : IFrontendWindowService
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private readonly Func<nint> _windowHandle;
    private readonly Func<AppWindow> _appWindow;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IInputService _inputService;
    private readonly IAppLogger _logger;

    public WindowFullscreenService(
        Func<nint> windowHandle,
        Func<AppWindow> appWindow,
        DispatcherQueue dispatcherQueue,
        IInputService inputService,
        IAppLogger logger)
    {
        _windowHandle = windowHandle;
        _appWindow = appWindow;
        _dispatcherQueue = dispatcherQueue;
        _inputService = inputService;
        _logger = logger;
    }

    public Task HideAsync() => DispatchAsync(() =>
    {
        _inputService.Stop();
        ShowWindow(_windowHandle(), SwHide);
        _logger.Information("FrontendHidden", "The NexaStation window was hidden.");
    });

    public Task BringToForegroundAsync() => DispatchAsync(() =>
    {
        nint handle = _windowHandle();
        ShowWindow(handle, SwShow);
        SetForegroundWindow(handle);
        _inputService.Start();
        _logger.Information("FrontendForeground", "The NexaStation window was brought to the foreground.");
    });

    public Task RestoreFullscreenAsync() => DispatchAsync(() =>
    {
        try
        {
            _appWindow().SetPresenter(AppWindowPresenterKind.FullScreen);
            _logger.Information("FullscreenChanged", "Fullscreen presentation was restored.");
            LogWindowMetrics(_appWindow());
        }
        catch (Exception exception)
        {
            _logger.LogError("FullscreenRestoreFailed", "Fullscreen restoration failed; using a maximized borderless window.", exception);
            AppWindow window = _appWindow();
            window.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (window.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.Maximize();
            }
        }
    });

    public Task ToggleFullscreenAsync() => DispatchAsync(() =>
    {
        AppWindow window = _appWindow();
        bool isFullscreen = window.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        window.SetPresenter(isFullscreen ? AppWindowPresenterKind.Overlapped : AppWindowPresenterKind.FullScreen);
        _logger.Information("FullscreenChanged", isFullscreen ? "Fullscreen exited." : "Fullscreen entered.");
        LogWindowMetrics(window);
    });

    public Task ExitFullscreenForDevelopmentAsync() => DispatchAsync(() =>
    {
        AppWindow window = _appWindow();
        window.SetPresenter(AppWindowPresenterKind.Overlapped);
        if (window.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, true);
            presenter.Maximize();
        }

        _logger.Information("DevelopmentDesktopExposed", "Development shortcut exposed the normal desktop.");
        LogWindowMetrics(window);
    });

    private void LogWindowMetrics(AppWindow window) => _logger.Information(
        "WindowPresentationMeasured",
        "The AppWindow presentation and bounds were measured.",
        new Dictionary<string, object?>
        {
            ["Presenter"] = window.Presenter.Kind.ToString(),
            ["X"] = window.Position.X,
            ["Y"] = window.Position.Y,
            ["Width"] = window.Size.Width,
            ["Height"] = window.Size.Height,
        });

    private Task DispatchAsync(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetException(new InvalidOperationException("The UI dispatcher is unavailable."));
        }

        return completion.Task;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);
}
