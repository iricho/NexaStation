using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace NexaStation.App;

public static class Program
{
    private static App? _application;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _application = new App();
        });
    }
}
