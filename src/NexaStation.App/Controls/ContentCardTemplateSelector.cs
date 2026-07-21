using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
using NexaStation.Domain;

namespace NexaStation.App.Controls;

public sealed class ContentCardTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GameTemplate{get;set;}
    public DataTemplate? ApplicationTemplate{get;set;}
    public DataTemplate? MediaTemplate{get;set;}
    protected override DataTemplate? SelectTemplateCore(object item) => item is CardViewModel card
        ? card.PresentationType switch{NexaStation.Core.ContentCardPresentationType.GameWide or NexaStation.Core.ContentCardPresentationType.GameCover=>GameTemplate,NexaStation.Core.ContentCardPresentationType.ApplicationSquare or NexaStation.Core.ContentCardPresentationType.ApplicationWide or NexaStation.Core.ContentCardPresentationType.WebApplication or NexaStation.Core.ContentCardPresentationType.StreamingService=>ApplicationTemplate,_=>card.ContentType==ContentType.Game?GameTemplate:MediaTemplate??ApplicationTemplate}
        : base.SelectTemplateCore(item);
    protected override DataTemplate? SelectTemplateCore(object item,DependencyObject container)=>SelectTemplateCore(item);
}
