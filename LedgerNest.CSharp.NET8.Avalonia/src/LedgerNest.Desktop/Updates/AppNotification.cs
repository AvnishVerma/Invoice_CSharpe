using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Desktop.Updates;

public enum NotificationType { Information, Update, Warning, Error }

public sealed partial class AppNotification : ObservableObject
{
    public AppNotification(string title, string message, NotificationType type, DateTimeOffset createdAt)
    {
        Title = title;
        Message = message;
        Type = type;
        CreatedAt = createdAt;
    }

    public string Title { get; }
    public string Message { get; }
    public NotificationType Type { get; }
    public DateTimeOffset CreatedAt { get; }
    [ObservableProperty] private bool isRead;
}
