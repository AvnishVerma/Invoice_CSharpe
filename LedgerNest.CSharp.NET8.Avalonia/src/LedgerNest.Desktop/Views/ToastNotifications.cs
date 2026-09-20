using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LedgerNest.Desktop.Notifications;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private DispatcherTimer? toastTimer;

    // Connects the application-scoped toast service to the main-window notification host.
    private void InitializeToasts()
    {
        toastService.Requested += OnToastRequested;
        toastTimer = new DispatcherTimer();
        toastTimer.Tick += (_, _) => HideToast();
    }

    // Converts existing ViewModel status messages into typed toast notifications.
    private void ShowStatusToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var lower = message.ToLowerInvariant();
        var type = lower.Contains("error") || lower.Contains("failed") || lower.Contains("could not") || lower.Contains("corrupt") || lower.Contains("invalid") || lower.Contains("unavailable")
            ? ToastType.Error
            : lower.Contains("cancel") || lower.Contains("required") || lower.Contains("select ") || lower.Contains("check ") || lower.Contains("cannot")
                ? ToastType.Warning
                : lower.Contains("saved") || lower.Contains("created") || lower.Contains("complete") || lower.Contains("restored") || lower.Contains("imported") || lower.Contains("deleted") || lower.Contains("sent ")
                    ? ToastType.Success
                    : ToastType.Info;
        toastService.Show(type switch { ToastType.Error => "Error", ToastType.Warning => "Attention", ToastType.Success => "Success", _ => "Notification" }, message, type);
    }

    private void OnToastRequested(object? sender, ToastMessage toast)
    {
        var colors = toast.Type switch
        {
            ToastType.Success => ("#E8F5E9", "#2E7D32", "\ue86c"),
            ToastType.Warning => ("#FFF8E1", "#ED6C02", "\ue002"),
            ToastType.Error => ("#FFEBEE", "#D32F2F", "\ue000"),
            _ => ("#E3F2FD", "#1565C0", "\ue88e")
        };
        toastTitle.Text = toast.Title;
        toastMessage.Text = toast.Message;
        toastCard.Background = Brush.Parse(colors.Item1);
        toastCard.BorderBrush = Brush.Parse(colors.Item2);
        toastIconCircle.Background = Brush.Parse(colors.Item2);
        toastIcon.Foreground = Brushes.White;
        toastIcon.Text = colors.Item3;
        toastTitle.Foreground = Brush.Parse(colors.Item2);
        toastMessage.Foreground = Brush.Parse("#252525");
        toastCard.IsVisible = true;
        toastTimer?.Stop();
        if (!toast.IsPersistent && toastTimer != null)
        {
            toastTimer.Interval = toast.DisplayTime;
            toastTimer.Start();
        }
    }

    private void OnDismissToast(object? sender, RoutedEventArgs e) => HideToast();

    private void HideToast()
    {
        toastTimer?.Stop();
        toastCard.IsVisible = false;
    }
}
