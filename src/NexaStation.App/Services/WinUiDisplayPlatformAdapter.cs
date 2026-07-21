using System.Globalization;
using Microsoft.UI.Windowing;
using NexaStation.Display.Monitors;

namespace NexaStation.App.Services;

public sealed class WinUiDisplayPlatformAdapter(Func<MainWindow?> window) : IDisplayPlatformAdapter
{
    public Task<IReadOnlyList<DisplayDescriptor>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MainWindow? currentWindow = window();
        DisplayArea? current = currentWindow is null ? null : DisplayArea.GetFromWindowId(currentWindow.AppWindow.Id, DisplayAreaFallback.Primary);
        IReadOnlyList<DisplayArea> areas = DisplayArea.FindAll();
        List<DisplayDescriptor> result = new(areas.Count);
        // The WinRT projection's IEnumerable adapter can fail on some Windows builds; indexed access is part of IReadOnlyList and is reliable.
        for (int index = 0; index < areas.Count; index++)
        {
            DisplayArea area = areas[index];
            string id = area.DisplayId.Value.ToString(CultureInfo.InvariantCulture);
            bool isCurrent = current is not null && current.DisplayId.Value == area.DisplayId.Value;
            result.Add(new(
                id,
                area.IsPrimary ? $"Display {index + 1} · Windows primary" : $"Display {index + 1}",
                new(area.OuterBounds.X, area.OuterBounds.Y, area.OuterBounds.Width, area.OuterBounds.Height),
                new(area.WorkArea.X, area.WorkArea.Y, area.WorkArea.Width, area.WorkArea.Height),
                1,
                (area.OuterBounds.Width, area.OuterBounds.Height),
                area.IsPrimary,
                isCurrent,
                true));
        }
        return Task.FromResult<IReadOnlyList<DisplayDescriptor>>(result);
    }

    public Task<bool> MoveCurrentWindowAsync(DisplayDescriptor display, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(display);
        cancellationToken.ThrowIfCancellationRequested();
        MainWindow? currentWindow = window();
        if (currentWindow is null) return Task.FromResult(false);
        AppWindow appWindow = currentWindow.AppWindow;
        AppWindowPresenterKind previous = appWindow.Presenter.Kind;
        try
        {
            if (previous == AppWindowPresenterKind.FullScreen) appWindow.SetPresenter(AppWindowPresenterKind.Default);
            appWindow.Move(new(display.WorkArea.X, display.WorkArea.Y));
            if (previous == AppWindowPresenterKind.FullScreen) appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            return Task.FromResult(true);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false);
        }
    }
}
