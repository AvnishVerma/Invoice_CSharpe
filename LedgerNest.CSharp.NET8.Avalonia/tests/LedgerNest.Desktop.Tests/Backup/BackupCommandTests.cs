using System.Collections.ObjectModel;
using LedgerNest.Desktop;

namespace LedgerNest.Desktop.Tests.Backup;

[Trait("Category", "Unit")]
public sealed class BackupCommandTests
{
    [Fact]
    public async Task BackupCommand_DuringLoading_DisablesDuplicateUiRequest()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var model = new BackupManagementViewModel([], () => { }, () => { calls++; return pending.Task; },
            () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask,
            _ => Task.CompletedTask, _ => Task.CompletedTask, _ => { });
        var running = model.CreateDatabaseCommand.ExecuteAsync(null);
        Assert.True(model.CreateDatabaseCommand.IsRunning);
        Assert.False(model.CreateDatabaseCommand.CanExecute(null));
        Assert.Equal(1, calls);
        pending.SetResult();
        await running;
        Assert.True(model.CreateDatabaseCommand.CanExecute(null));
    }

    [Fact]
    public void BackupHistory_Refresh_UpdatesEmptyStateWithoutDuplicates()
    {
        var history = new ObservableCollection<BackupHistoryItem>();
        var item = new BackupHistoryItem("fixture", "fixture.json", 1024, new DateTime(2026, 1, 1), false);
        var model = new BackupManagementViewModel(history, () => { history.Clear(); history.Add(item); },
            () => Task.CompletedTask, () => Task.CompletedTask, () => Task.CompletedTask,
            _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => { });
        model.Attach();
        Assert.False(model.HasHistory);
        model.RefreshCommand.Execute(null);
        model.RefreshCommand.Execute(null);
        Assert.True(model.HasHistory);
        Assert.Single(history);
        model.Detach();
    }
}
