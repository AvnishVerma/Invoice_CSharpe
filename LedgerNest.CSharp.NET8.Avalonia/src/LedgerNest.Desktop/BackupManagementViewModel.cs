using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed class BackupManagementViewModel : ObservableObject
{
    public ObservableCollection<BackupHistoryItem> History { get; }
    public bool HasHistory => History.Count > 0;
    public IRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand CreateDatabaseCommand { get; }
    public IAsyncRelayCommand ExportJsonCommand { get; }
    public IAsyncRelayCommand ImportCommand { get; }
    public IAsyncRelayCommand<BackupHistoryItem> RestoreCommand { get; }
    public IAsyncRelayCommand<BackupHistoryItem> DownloadCommand { get; }
    public IAsyncRelayCommand<BackupHistoryItem> ShareCommand { get; }
    public IRelayCommand<BackupHistoryItem> DeleteCommand { get; }

    public BackupManagementViewModel(ObservableCollection<BackupHistoryItem> history, Action refresh,
        Func<Task> createDatabase, Func<Task> exportJson, Func<Task> import,
        Func<BackupHistoryItem, Task> restore, Func<BackupHistoryItem, Task> download,
        Func<BackupHistoryItem, Task> share, Action<BackupHistoryItem> delete)
    {
        History = history;
        RefreshCommand = new RelayCommand(refresh);
        CreateDatabaseCommand = new AsyncRelayCommand(createDatabase);
        ExportJsonCommand = new AsyncRelayCommand(exportJson);
        ImportCommand = new AsyncRelayCommand(import);
        RestoreCommand = new AsyncRelayCommand<BackupHistoryItem>(item => item == null ? Task.CompletedTask : restore(item));
        DownloadCommand = new AsyncRelayCommand<BackupHistoryItem>(item => item == null ? Task.CompletedTask : download(item));
        ShareCommand = new AsyncRelayCommand<BackupHistoryItem>(item => item == null ? Task.CompletedTask : share(item));
        DeleteCommand = new RelayCommand<BackupHistoryItem>(item => { if (item != null) delete(item); });
    }

    public void Attach()
    {
        History.CollectionChanged -= HistoryChanged;
        History.CollectionChanged += HistoryChanged;
        OnPropertyChanged(nameof(HasHistory));
    }
    public void Detach() => History.CollectionChanged -= HistoryChanged;
    private void HistoryChanged(object? sender, NotifyCollectionChangedEventArgs args) => OnPropertyChanged(nameof(HasHistory));
}

public sealed record BackupHistoryItem(string Name, string FilePath, long Size, DateTime CreatedAt, bool IsDatabase)
{
    public string Icon => IsDatabase ? "storage" : "code";
    public string FormattedSize => Size >= 1024 * 1024 ? $"{Size / 1024d / 1024d:0.0} MB" : $"{Math.Max(0.1, Size / 1024d):0.0} KB";
    public string FormattedCreatedAt => CreatedAt.ToString("dd MMM yyyy HH:mm");
}
