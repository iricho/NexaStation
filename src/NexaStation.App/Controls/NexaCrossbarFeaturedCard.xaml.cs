using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;

namespace NexaStation.App.Controls;

public sealed partial class NexaCrossbarFeaturedCard : UserControl
{
    public NexaCrossbarFeaturedCard(){InitializeComponent();Loaded+=OnLoaded;Unloaded+=OnUnloaded;}
    public event EventHandler? OptionsRequested;
    private void Options_Click(object sender,RoutedEventArgs e)=>OptionsRequested?.Invoke(this,EventArgs.Empty);
    public CardViewModel? Card{get=>(CardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}
    public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(CardViewModel),typeof(NexaCrossbarFeaturedCard),new PropertyMetadata(null));
    private async void OnLoaded(object sender,RoutedEventArgs e){if(Card is not null)try{await Card.EnsureArtworkAsync();}catch(OperationCanceledException){}}
    private void OnUnloaded(object sender,RoutedEventArgs e)=>Card?.CancelArtworkRequest();
}
