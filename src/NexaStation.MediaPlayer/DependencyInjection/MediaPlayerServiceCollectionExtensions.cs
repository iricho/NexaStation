using Microsoft.Extensions.DependencyInjection;
using NexaStation.MediaPlayer.ExternalPlayers;
using NexaStation.MediaPlayer.Playback;

namespace NexaStation.MediaPlayer.DependencyInjection;

public static class MediaPlayerServiceCollectionExtensions
{
    public static IServiceCollection AddNexaMediaPlayer(this IServiceCollection services)
    {
        services.AddSingleton<WindowsMediaPlaybackEngine>();
        services.AddSingleton<IMediaPlaybackEngine>(provider => provider.GetRequiredService<WindowsMediaPlaybackEngine>());
        services.AddSingleton<IWindowsMediaPlayerAccessor>(provider => provider.GetRequiredService<WindowsMediaPlaybackEngine>());
        services.AddSingleton<INexaMediaPlayerService, NexaMediaPlayerService>();
        services.AddSingleton<IExternalMediaPlayerRegistrationStore, EmptyExternalMediaPlayerRegistrationStore>();
        services.AddSingleton<IExternalMediaPlayerService, ExternalMediaPlayerService>();
        return services;
    }
}
