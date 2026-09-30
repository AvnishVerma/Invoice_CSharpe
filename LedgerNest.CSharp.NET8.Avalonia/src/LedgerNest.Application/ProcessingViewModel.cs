using CommunityToolkit.Mvvm.ComponentModel;

namespace LedgerNest.Application;

public sealed partial class ProcessingViewModel : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string message = "";
    [ObservableProperty] private string error = "";

    public async Task<bool> RunAsync(Func<CancellationToken, Task> operation, string message = "Processing, please wait...", CancellationToken cancellationToken = default)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Message = message;
        Error = "";
        try
        {
            await operation(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Error = "Processing was cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
            Message = "";
        }
    }
}
