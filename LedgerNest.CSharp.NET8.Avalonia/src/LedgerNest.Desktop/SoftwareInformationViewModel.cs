using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Updates;

namespace LedgerNest.Desktop;

public sealed partial class SoftwareInformationViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly Func<Task> checkForUpdates;
    private readonly Action openDownload;
    public string AppName => Branding.Name;
    public string Version => AppVersion.Display;
    public string Build => string.IsNullOrWhiteSpace(AppVersion.BuildId) ? "" : AppVersion.BuildId[..Math.Min(7, AppVersion.BuildId.Length)];
    public bool HasBuild => Build.Length > 0;
    public string UpdateStatus => model.UpdateStatus;
    public string LatestVersion => model.LatestUpdateVersion;
    public string LatestNotes => model.LatestUpdateNotes;
    public bool HasLatestVersion => !string.IsNullOrWhiteSpace(LatestVersion);
    public bool HasLatestNotes => !string.IsNullOrWhiteSpace(LatestNotes);
    public bool HasDownload => !string.IsNullOrWhiteSpace(model.LatestUpdateDownloadUrl);
    public IEnumerable<AppNotification> Notifications => model.Notifications.Take(5);
    public bool HasNotifications => model.Notifications.Count > 0;
    public int UnreadCount => model.UnreadNotificationCount;
    [ObservableProperty] private string manifestUrl;
    public IRelayCommand CheckForUpdatesCommand { get; }
    public IRelayCommand OpenDownloadCommand { get; }
    public IRelayCommand ChangePasswordCommand { get; }
    public IRelayCommand OnboardingCommand { get; }

    public SoftwareInformationViewModel(MainWindowViewModel model, Func<Task> checkForUpdates, Action openDownload, Action changePassword, Action onboarding)
    {
        this.model = model; this.checkForUpdates = checkForUpdates; this.openDownload = openDownload;
        manifestUrl = model.UpdateManifestUrl;
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckAsync);
        OpenDownloadCommand = new RelayCommand(openDownload);
        ChangePasswordCommand = new RelayCommand(changePassword);
        OnboardingCommand = new RelayCommand(onboarding);
    }

    [RelayCommand] private void SaveChannel() { model.SaveUpdateManifestUrl(ManifestUrl); Refresh(); }
    [RelayCommand] private void MarkRead() { model.MarkNotificationsRead(); Refresh(); }
    private async Task CheckAsync() { await checkForUpdates(); Refresh(); }
    private void Refresh()
    {
        OnPropertyChanged(nameof(UpdateStatus)); OnPropertyChanged(nameof(LatestVersion)); OnPropertyChanged(nameof(LatestNotes));
        OnPropertyChanged(nameof(HasLatestVersion)); OnPropertyChanged(nameof(HasLatestNotes)); OnPropertyChanged(nameof(HasDownload));
        OnPropertyChanged(nameof(Notifications)); OnPropertyChanged(nameof(HasNotifications)); OnPropertyChanged(nameof(UnreadCount));
    }
}
