using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using NexaStation.Domain;
using Microsoft.UI.Xaml;

namespace NexaStation.App.ViewModels;

public sealed class DashboardSectionViewModel(
    NavigationSection section,
    string title,
    string glyph,
    IReadOnlyList<DashboardRowViewModel> rows) : ObservableObject
{
    private bool _isSelected;
    private Visibility _visibility = Visibility.Visible;

    public NavigationSection Section { get; } = section;

    public string Id => Section.ToString();

    public string Title { get; } = title;

    public string Glyph { get; } = glyph;

    public ObservableCollection<DashboardRowViewModel> Rows { get; } = new(rows);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public Visibility Visibility
    {
        get => _visibility;
        set => SetProperty(ref _visibility, value);
    }
}
