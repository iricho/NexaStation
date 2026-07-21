using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaAdaptiveLibraryGrid:UserControl
{
    public NexaAdaptiveLibraryGrid()=>InitializeComponent();
    public IReadOnlyList<CardViewModel>? Items{get=>(IReadOnlyList<CardViewModel>?)GetValue(ItemsProperty);set=>SetValue(ItemsProperty,value);}
    public static readonly DependencyProperty ItemsProperty=DependencyProperty.Register(nameof(Items),typeof(IReadOnlyList<CardViewModel>),typeof(NexaAdaptiveLibraryGrid),new PropertyMetadata(null));
    public event EventHandler<CardViewModel>? ItemInvoked;
    private void Grid_ItemClick(object sender,ItemClickEventArgs e){if(e.ClickedItem is CardViewModel card)ItemInvoked?.Invoke(this,card);}
    private void Grid_SizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(Items is not {Count:>0}||e.NewSize.Width<240)return;
        int columns=Math.Max(2,(int)Math.Round(e.NewSize.Width/250d,MidpointRounding.AwayFromZero));
        double width=Math.Max(180,(e.NewSize.Width-columns*18)/columns);
        foreach(CardViewModel card in Items)
        {
            double ratio = card.Width <= 0 ? 0.56 : card.Height / card.Width;
            card.SetDisplaySize(width,Math.Clamp(width*ratio,112,320));
        }
    }
}
