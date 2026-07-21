using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NexaStation.Core;
using NexaStation.Input.Glyphs;

namespace NexaStation.App.Controls;

public sealed partial class NexaContextMenu : ContentDialog
{
    private readonly IInputGlyphService _glyphs;
    private IReadOnlyList<ItemActionDescriptor> _rootActions = [];
    private ItemActionDescriptor? _selected;

    public NexaContextMenu(IInputGlyphService glyphs)
    {
        _glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        InitializeComponent();
        Opened += OnOpened;
    }

    public async Task<ItemActionDescriptor?> ShowForAsync(string title, IReadOnlyList<ItemActionDescriptor> actions)
    {
        Title = title;
        _rootActions = actions ?? throw new ArgumentNullException(nameof(actions));
        _selected = null;
        ShowLevel(_rootActions, null);
        await ShowAsync();
        return _selected;
    }

    private void ShowLevel(IReadOnlyList<ItemActionDescriptor> actions, string? levelTitle)
    {
        BackButton.Visibility = levelTitle is null ? Visibility.Collapsed : Visibility.Visible;
        LevelTitle.Text = levelTitle ?? "Choose an action";
        ActionList.ItemsSource = actions.Select(action => new NexaContextActionRow(action, Shortcut(action))).ToArray();
        DispatcherQueue.TryEnqueue(() => (ActionList.ContainerFromIndex(0) as ListViewItem)?.Focus(FocusState.Programmatic));
    }

    private InputGlyph? Shortcut(ItemActionDescriptor action)
    {
        if (action.AdaptiveInputAction is not { } semantic) return null;
        return _glyphs.Resolve(semantic, InputGlyphContext.Dialog);
    }

    private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => (ActionList.ContainerFromIndex(0) as ListViewItem)?.Focus(FocusState.Programmatic));

    private void Back_Click(object sender, RoutedEventArgs e) => ShowLevel(_rootActions, null);

    private void ActionList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not NexaContextActionRow row) return;
        if (!row.Action.Enabled) return;
        if (row.Action.ChildActions.Count != 0)
        {
            ShowLevel(row.Action.ChildActions, row.Action.Label);
            return;
        }
        _selected = row.Action;
        Hide();
    }
}

public sealed class NexaContextActionRow
{
    public NexaContextActionRow(ItemActionDescriptor action, InputGlyph? shortcut)
    {
        Action = action;
        ShortcutHint = action.ShortcutHint ?? shortcut?.TextFallback ?? string.Empty;
        string? assetUri = shortcut is null ? null : InputGlyphAssetUri.Resolve(shortcut);
        ShortcutAsset = assetUri is null ? null : new BitmapImage(new Uri(assetUri));
    }

    public ItemActionDescriptor Action { get; }
    public string Label => Action.Label;
    public string SupportingText => Action.Enabled ? Action.Description ?? string.Empty : Action.DisabledReason ?? "Unavailable";
    public string AccessibilityName => string.IsNullOrWhiteSpace(SupportingText) ? Label : $"{Label}. {SupportingText}";
    public string ShortcutHint { get; }
    public ImageSource? ShortcutAsset { get; }
    public Visibility ShortcutAssetVisibility => ShortcutAsset is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShortcutTextVisibility => ShortcutAsset is null ? Visibility.Visible : Visibility.Collapsed;
    public double Opacity => Action.Enabled ? 1 : 0.52;
    public Visibility SupportingVisibility => string.IsNullOrWhiteSpace(SupportingText) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ChildIndicatorVisibility => Action.ChildActions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string Glyph => Action.IconKey switch
    {
        "Play" => "\uE768",
        "Details" => "\uE946",
        "Edit" => "\uE70F",
        "Favorite" => "\uE734",
        "Hide" => "\uED1A",
        "Collection" => "\uE8B7",
        "Artwork" => "\uEB9F",
        "Search" => "\uE721",
        "Folder" => "\uE8B7",
        "Reset" => "\uE777",
        "Compatibility" => "\uE9D9",
        "Provider" => "\uE774",
        "Settings" => "\uE713",
        "Pin" => "\uE718",
        "Delete" => "\uE74D",
        _ => "\uE712",
    };
}
