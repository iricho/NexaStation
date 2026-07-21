using Microsoft.Extensions.DependencyInjection;
using NexaStation.SystemIntegration.Audio;
using NexaStation.SystemIntegration.Controllers;
using NexaStation.SystemIntegration.Network;
using NexaStation.SystemIntegration.Power;
using NexaStation.SystemIntegration.Services;
using NexaStation.SystemIntegration.Sessions;
using NexaStation.SystemIntegration.Time;

namespace NexaStation.SystemIntegration.DependencyInjection;

public static class SystemIntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddSystemIntegration(this IServiceCollection services)=>services
        .AddSingleton<ISystemVolumeService,CoreAudioVolumeService>()
        .AddSingleton<IAudioOutputDeviceService,CoreAudioOutputDeviceService>()
        .AddSingleton<ISystemMediaControlService,WindowsSystemMediaControlService>()
        .AddSingleton<IControllerStatusService,WindowsControllerStatusService>()
        .AddSingleton<INetworkStatusService,WindowsNetworkStatusService>()
        .AddSingleton<ITimeService,LocalTimeService>()
        .AddSingleton<ICurrentActivityService,CurrentActivityService>()
        .AddSingleton<ISystemPowerService,WindowsSystemPowerService>();
}
