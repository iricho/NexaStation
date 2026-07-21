using Microsoft.UI.Xaml.Controls;

namespace NexaStation.App.ViewModels;

public sealed record DashboardNotificationViewModel(
    string Id,
    InfoBarSeverity Severity,
    string Title,
    string Message,
    string? PrimaryAction = null,
    string? SecondaryAction = null,
    DateTime? ExpiresAtUtc = null);
