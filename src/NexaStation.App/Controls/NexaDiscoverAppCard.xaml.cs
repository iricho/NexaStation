using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaDiscoverAppCard:UserControl { public NexaDiscoverAppCard()=>InitializeComponent(); public DiscoverAppCardViewModel? Card{get=>(DiscoverAppCardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(DiscoverAppCardViewModel),typeof(NexaDiscoverAppCard),new PropertyMetadata(null)); }
