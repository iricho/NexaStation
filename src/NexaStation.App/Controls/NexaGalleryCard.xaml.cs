using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NexaStation.App.ViewModels;
namespace NexaStation.App.Controls;
public sealed partial class NexaGalleryCard:UserControl{public NexaGalleryCard()=>InitializeComponent();public GalleryCardViewModel? Card{get=>(GalleryCardViewModel?)GetValue(CardProperty);set=>SetValue(CardProperty,value);}public static readonly DependencyProperty CardProperty=DependencyProperty.Register(nameof(Card),typeof(GalleryCardViewModel),typeof(NexaGalleryCard),new PropertyMetadata(null));private async void ControlRoot_Loaded(object sender,RoutedEventArgs e){if(Card is not null)await Card.EnsureThumbnailAsync();}private void ControlRoot_Unloaded(object sender,RoutedEventArgs e)=>Card?.CancelThumbnailRequest();}
