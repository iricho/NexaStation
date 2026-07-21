namespace NexaStation.App.ViewModels;

public sealed record DashboardRowViewModel(string Title,IReadOnlyList<CardViewModel> Items,string RowKey="built-in",bool ShowTitle=true);
