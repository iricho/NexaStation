namespace NexaStation.Core;

public interface IFrontendWindowService
{
    Task HideAsync();

    Task RestoreFullscreenAsync();

    Task BringToForegroundAsync();
}
