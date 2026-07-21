using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaVideoCard:UserControl { public NexaVideoCard()=>InitializeComponent();public GalleryCardViewModel? Card{get=>(GalleryCardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(GalleryCardViewModel),typeof(NexaVideoCard),new PropertyMetadata(null)); }
