using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
using NexaStation.Core;
using NexaStation.Presentation.Dashboard;
namespace NexaStation.App.Controls;
public sealed partial class NexaAdaptiveLibraryGrid:UserControl
{
    public NexaAdaptiveLibraryGrid()=>InitializeComponent();
    public IReadOnlyList<CardViewModel>? Items{get=>(IReadOnlyList<CardViewModel>?)GetValue(ItemsProperty);set=>SetValue(ItemsProperty,value);}
    public static readonly DependencyProperty ItemsProperty=DependencyProperty.Register(nameof(Items),typeof(IReadOnlyList<CardViewModel>),typeof(NexaAdaptiveLibraryGrid),new PropertyMetadata(null,OnItemsChanged));
    public bool IsScrollEnabled{get=>(bool)GetValue(IsScrollEnabledProperty);set=>SetValue(IsScrollEnabledProperty,value);}
    public static readonly DependencyProperty IsScrollEnabledProperty=DependencyProperty.Register(nameof(IsScrollEnabled),typeof(bool),typeof(NexaAdaptiveLibraryGrid),new PropertyMetadata(false,OnScrollModeChanged));
    public int ColumnCount { get; private set; } = 1;
    private static void OnScrollModeChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        NexaAdaptiveLibraryGrid grid=(NexaAdaptiveLibraryGrid)sender;
        ScrollViewer.SetVerticalScrollMode(grid.LibraryGrid,grid.IsScrollEnabled?ScrollMode.Enabled:ScrollMode.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(grid.LibraryGrid,grid.IsScrollEnabled?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled);
    }
    public event EventHandler<CardViewModel>? ItemInvoked;
    public event EventHandler<string>? ActionRequested;
    private void GameCard_ActionRequested(object? sender,string action)=>ActionRequested?.Invoke(sender,action);
    private void Grid_ItemClick(object sender,ItemClickEventArgs e){if(e.ClickedItem is CardViewModel card)ItemInvoked?.Invoke(this,card);}
    private static void OnItemsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        NexaAdaptiveLibraryGrid grid=(NexaAdaptiveLibraryGrid)sender;
        if(grid.LibraryGrid is not null)grid.UpdateCardSizes(grid.LibraryGrid.ActualWidth);
    }
    private void Grid_SizeChanged(object sender,SizeChangedEventArgs e)=>UpdateCardSizes(e.NewSize.Width);
    private void UpdateCardSizes(double availableWidth)
    {
        if(Items is not {Count:>0}||availableWidth<1)return;
        LibraryGridMetrics metrics=LibraryGridMetrics.Resolve(availableWidth,Items.Any(card=>card.PresentationType==ContentCardPresentationType.GameCover),Items.All(card=>card.PresentationType==ContentCardPresentationType.CompactList));
        ColumnCount=metrics.Columns;
        foreach(CardViewModel card in Items)
        {
            card.SetDisplaySize(metrics.CardWidth,metrics.CardHeight);
        }
        if(LibraryGrid.ItemsPanelRoot is ItemsWrapGrid panel)
        {
            panel.ItemWidth=metrics.CardWidth+18;
            panel.ItemHeight=metrics.CardHeight+18;
        }
    }
}
