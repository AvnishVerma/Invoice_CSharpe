using Avalonia.Threading;

namespace LedgerNest.Desktop.Notifications;

public enum ToastType { Info, Success, Warning, Error }

public sealed record ToastMessage(string Title, string Message, ToastType Type, TimeSpan DisplayTime, bool IsPersistent = false);

// Provides the dependency-injection-friendly notification contract inspired by WPF_ToastNotifications.
public interface IToastService
{
    event EventHandler<ToastMessage>? Requested;
    void Show(string title, string message, ToastType type = ToastType.Info, TimeSpan? displayTime = null, bool isPersistent = false);
    void Show(Exception exception, string title = "Something went wrong");
}

// Publishes toast requests on Avalonia's UI thread without depending on WPF or an operating system.
public sealed class AvaloniaToastService : IToastService
{
    public event EventHandler<ToastMessage>? Requested;

    public void Show(string title, string message, ToastType type = ToastType.Info, TimeSpan? displayTime = null, bool isPersistent = false)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var toast = new ToastMessage(title, message.Trim(), type, displayTime ?? TimeSpan.FromSeconds(type == ToastType.Error ? 8 : 5), isPersistent);
        if (Dispatcher.UIThread.CheckAccess()) Requested?.Invoke(this, toast);
        else Dispatcher.UIThread.Post(() => Requested?.Invoke(this, toast));
    }

    public void Show(Exception exception, string title = "Something went wrong") =>
        Show(title, exception.Message, ToastType.Error, isPersistent: true);
}
