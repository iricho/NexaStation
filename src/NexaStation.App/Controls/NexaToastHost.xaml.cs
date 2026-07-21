using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace NexaStation.App.Controls;
public sealed partial class NexaToastHost:UserControl
{
    public NexaToastHost()=>InitializeComponent();public event RoutedEventHandler? DismissClick;public event RoutedEventHandler? PrimaryClick;public event RoutedEventHandler? SecondaryClick;
    public string Title{get=>(string)GetValue(TitleProperty);set=>SetValue(TitleProperty,value);}public static readonly DependencyProperty TitleProperty=DependencyProperty.Register(nameof(Title),typeof(string),typeof(NexaToastHost),new PropertyMetadata(string.Empty));
    public string Message{get=>(string)GetValue(MessageProperty);set=>SetValue(MessageProperty,value);}public static readonly DependencyProperty MessageProperty=DependencyProperty.Register(nameof(Message),typeof(string),typeof(NexaToastHost),new PropertyMetadata(string.Empty));
    public InfoBarSeverity Severity{get=>(InfoBarSeverity)GetValue(SeverityProperty);set=>SetValue(SeverityProperty,value);}public static readonly DependencyProperty SeverityProperty=DependencyProperty.Register(nameof(Severity),typeof(InfoBarSeverity),typeof(NexaToastHost),new PropertyMetadata(InfoBarSeverity.Informational));
    public string? PrimaryText{get=>(string?)GetValue(PrimaryTextProperty);set=>SetValue(PrimaryTextProperty,value);}public static readonly DependencyProperty PrimaryTextProperty=DependencyProperty.Register(nameof(PrimaryText),typeof(string),typeof(NexaToastHost),new PropertyMetadata(null,OnActionsChanged));
    public string? SecondaryText{get=>(string?)GetValue(SecondaryTextProperty);set=>SetValue(SecondaryTextProperty,value);}public static readonly DependencyProperty SecondaryTextProperty=DependencyProperty.Register(nameof(SecondaryText),typeof(string),typeof(NexaToastHost),new PropertyMetadata(null,OnActionsChanged));
    private static void OnActionsChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args){if(sender is NexaToastHost host&&host.PrimaryButton is not null){host.PrimaryButton.Visibility=string.IsNullOrWhiteSpace(host.PrimaryText)?Visibility.Collapsed:Visibility.Visible;host.SecondaryButton.Visibility=string.IsNullOrWhiteSpace(host.SecondaryText)?Visibility.Collapsed:Visibility.Visible;}}
    private void InfoBar_CloseButtonClick(InfoBar sender,object args)=>DismissClick?.Invoke(this,new RoutedEventArgs());
    private void PrimaryButton_Click(object sender,RoutedEventArgs args)=>PrimaryClick?.Invoke(this,args);
    private void SecondaryButton_Click(object sender,RoutedEventArgs args)=>SecondaryClick?.Invoke(this,args);
}
