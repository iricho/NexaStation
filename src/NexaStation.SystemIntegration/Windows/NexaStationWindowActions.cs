using NexaStation.SystemIntegration.Services;

namespace NexaStation.SystemIntegration.Windows;

public sealed class NexaStationWindowActions(Func<Task> minimize,Func<Task> returnToDesktop,Func<Task> restore,Func<Task> restart):INexaStationWindowActions
{
    public Task MinimizeAsync()=>minimize();public Task ReturnToDesktopAsync()=>returnToDesktop();public Task RestoreAsync()=>restore();public Task RestartApplicationAsync()=>restart();
}
