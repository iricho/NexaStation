using Microsoft.Extensions.DependencyInjection;
using NexaStation.Core;
using NexaStation.Providers.WindowsApps.AppsFolder;
using NexaStation.Providers.WindowsApps.Categorization;
using NexaStation.Providers.WindowsApps.Icons;
using NexaStation.Providers.WindowsApps.Launching;
using NexaStation.Providers.WindowsApps.PackagedApps;
using NexaStation.Providers.WindowsApps.ProgressiveWebApps;
using NexaStation.Providers.WindowsApps.StartMenu;
using NexaStation.Recovery;

namespace NexaStation.Providers.WindowsApps.DependencyInjection;

public static class WindowsApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsApplicationProviders(this IServiceCollection services)
    {
        services.AddSingleton<IStartMenuKnownFolderResolver,StartMenuKnownFolderResolver>();services.AddSingleton<IShellLinkReader,ShellLinkReader>();services.AddSingleton<IStartMenuApplicationScanner,StartMenuApplicationScanner>();
        services.AddSingleton<IAppsFolderApplicationReader,PackagedApplicationReader>();services.AddSingleton<IAppsFolderScanner,AppsFolderScanner>();services.AddSingleton<IPwaDetector,PwaDetector>();services.AddSingleton<IWindowsApplicationClassifier,WindowsApplicationClassifier>();
        services.AddSingleton<IPackagedApplicationActivator,PackagedApplicationActivator>();services.AddSingleton<IWindowsApplicationActivationService,WindowsApplicationActivationService>();services.AddSingleton<IApplicationIconExtractor>(provider=>new ApplicationIconExtractor(provider.GetRequiredService<INexaStationPaths>()));services.AddSingleton<IApplicationVisualResolver,ApplicationVisualResolver>();services.AddSingleton<IWindowsApplicationLibraryService,WindowsApplicationLibraryService>();
        services.AddSingleton<ILibraryProvider,WindowsStartMenuProvider>();services.AddSingleton<ILibraryProvider,WindowsAppsFolderProvider>();services.AddSingleton<ILibraryProvider,WindowsPwaProvider>();return services;
    }
}
