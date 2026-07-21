using NexaStation.Customization.Models;
using NexaStation.Presentation.LayoutContracts;

namespace NexaStation.Presentation.CardPresentation;

public sealed class CardPresentationService:ICardPresentationService
{
    public NexaStation.Customization.Models.CardPresentation Resolve(CardStyle style,CardSize size,InterfaceLayoutType layout,bool reducedMotion=false)
    {
        double scale=size switch{CardSize.Small=>.82,CardSize.Large=>1.2,_=>1};double ratio=style switch{CardStyle.VerticalCover=>2d/3d,CardStyle.Wide=>16d/9d,CardStyle.Square=>1,CardStyle.Compact=>16d/8d,CardStyle.Icon=>1,_=>3.6};bool minimal=layout==InterfaceLayoutType.ClassicCrossbar;return new(style,size,ratio,!minimal||style!=CardStyle.List,true,style is not (CardStyle.Icon),!minimal,!minimal,true,true,true,!minimal,reducedMotion?AnimationIntensity.None:minimal?AnimationIntensity.Low:AnimationIntensity.Balanced,style==CardStyle.List?8:16*scale);
    }
}
