using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaGameCard:UserControl
{
    public NexaGameCard(){InitializeComponent();Loaded+=OnLoaded;Unloaded+=OnUnloaded;}
    public CardViewModel? Card{get=>(CardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(CardViewModel),typeof(NexaGameCard),new PropertyMetadata(null));
    public event EventHandler<string>? ActionRequested;
    private void Play_Click(object sender,RoutedEventArgs e)=>ActionRequested?.Invoke(this,"play");
    private void Favorite_Click(object sender,RoutedEventArgs e)=>ActionRequested?.Invoke(this,"favorite");
    private void Options_Click(object sender,RoutedEventArgs e)=>ActionRequested?.Invoke(this,"options");
    private async void OnLoaded(object sender,RoutedEventArgs e){if(Card is not null)try{await Card.EnsureArtworkAsync();}catch(OperationCanceledException){}}
    private void OnUnloaded(object sender,RoutedEventArgs e)=>Card?.CancelArtworkRequest();
}
