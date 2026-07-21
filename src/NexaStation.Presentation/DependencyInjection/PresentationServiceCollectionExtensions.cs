using Microsoft.Extensions.DependencyInjection;
using NexaStation.Presentation.CardPresentation;
using NexaStation.Presentation.Collections;
using NexaStation.Presentation.Dashboard;
using NexaStation.Presentation.Focus;
using NexaStation.Presentation.LayoutContracts;
using NexaStation.Presentation.Layouts;
using NexaStation.Recovery;

namespace NexaStation.Presentation.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services) => services
        .AddSingleton<IInterfacePresentationService, InterfacePresentationService>()
        .AddSingleton<ISemanticFocusService, SemanticFocusService>()
        .AddSingleton<ICardPresentationService, CardPresentationService>()
        .AddSingleton<NexaStation.Core.IContentCardPresentationResolver, ContentCardPresentationResolver>()
        .AddSingleton<NexaStation.Core.ICollectionMosaicService>(provider => new CollectionMosaicService(provider.GetRequiredService<INexaStationPaths>()))
        .AddSingleton<IArtworkPresentationService, ArtworkPresentationService>()
        .AddSingleton<IDashboardSemanticService, DashboardSemanticService>()
        .AddSingleton<IHomeDashboardDensityPolicy, HomeDashboardDensityPolicy>()
        .AddSingleton<IHeroSelectionService, HeroSelectionService>();
}
