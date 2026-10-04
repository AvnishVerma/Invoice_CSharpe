using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Notifications;
using LedgerNest.Desktop.Updates;

namespace LedgerNest.Desktop;

public sealed partial class PublisherConsoleViewModel : ObservableObject
{
    [ObservableProperty] private string version = AppVersion.Current;
    [ObservableProperty] private string downloadUrl = "";
    [ObservableProperty] private string releaseNotes = "";
    [ObservableProperty] private string announcementTitle = "";
    [ObservableProperty] private string announcementMessage = "";
    [ObservableProperty] private string notificationKind = NotificationType.Information.ToString();
    [ObservableProperty] private string preview = "";
    public IReadOnlyList<string> NotificationKinds { get; } = Enum.GetNames<NotificationType>();
    public IAsyncRelayCommand SaveCommand { get; }

    public PublisherConsoleViewModel(Func<string?, string?, string?, string?, string?, string?, Task> save)
    {
        SaveCommand = new AsyncRelayCommand(() => save(Version, DownloadUrl, ReleaseNotes, AnnouncementTitle, AnnouncementMessage, NotificationKind));
        PropertyChanged += RefreshPreview;
        RefreshPreview(this, new PropertyChangedEventArgs(null));
    }

    private void RefreshPreview(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Preview)) return;
        var title = AnnouncementTitle?.Trim() ?? "";
        var message = AnnouncementMessage?.Trim() ?? "";
        var type = Enum.TryParse<NotificationType>(NotificationKind, out var parsed) ? parsed : NotificationType.Information;
        IReadOnlyList<GlobalNotification>? notifications = title.Length == 0 || message.Length == 0
            ? null : [new GlobalNotification("generated-on-publish", title, message, type, DateTimeOffset.UtcNow)];
        Preview = HttpAppUpdateService.SerializeManifest(new AppUpdateManifest(Version?.Trim() ?? "0.0.0", DownloadUrl?.Trim(), ReleaseNotes?.Trim(), DateTimeOffset.UtcNow, false, notifications));
    }
}
