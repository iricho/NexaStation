using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaStreamingServiceTile:UserControl { public NexaStreamingServiceTile()=>InitializeComponent(); public DiscoverAppCardViewModel? Card{get=>(DiscoverAppCardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(DiscoverAppCardViewModel),typeof(NexaStreamingServiceTile),new PropertyMetadata(null)); }
