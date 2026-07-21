using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaCollectionCard:UserControl { public NexaCollectionCard()=>InitializeComponent();public CollectionCardViewModel? Card{get=>(CollectionCardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(CollectionCardViewModel),typeof(NexaCollectionCard),new PropertyMetadata(null)); }
