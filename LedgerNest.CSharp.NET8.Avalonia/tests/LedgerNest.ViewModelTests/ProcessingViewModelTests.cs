using LedgerNest.Application;

namespace LedgerNest.ViewModelTests;

public sealed class ProcessingViewModelTests
{
    [Fact]
    public async Task BusyStateIsVisibleUntilOperationCompletes()
    {
        var model = new ProcessingViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = model.RunAsync(_ => release.Task);
        Assert.True(model.IsBusy);
        Assert.Equal("Processing, please wait...", model.Message);
        Assert.False(await model.RunAsync(_ => Task.CompletedTask));
        release.SetResult();
        Assert.True(await running);
        Assert.False(model.IsBusy);
        Assert.Empty(model.Message);
    }

    [Fact]
    public async Task FailureRestoresUsableStateAndExposesError()
    {
        var model = new ProcessingViewModel();
        Assert.False(await model.RunAsync(_ => throw new InvalidOperationException("Report failed")));
        Assert.False(model.IsBusy);
        Assert.Equal("Report failed", model.Error);
    }

    [Fact]
    public async Task CancellationRestoresState()
    {
        var model = new ProcessingViewModel();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.False(await model.RunAsync(token => Task.Delay(100, token), cancellationToken: cancellation.Token));
        Assert.False(model.IsBusy);
        Assert.Equal("Processing was cancelled.", model.Error);
    }
}
