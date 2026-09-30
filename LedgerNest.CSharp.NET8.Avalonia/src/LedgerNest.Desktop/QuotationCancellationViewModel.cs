using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class QuotationCancellationViewModel : ObservableObject
{
    private readonly MainWindowViewModel model;
    private readonly UiRecord quotation;
    private readonly Action completed;
    private readonly Action close;
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    public string QuotationNumber => quotation.Name;
    public string Customer => quotation["Customer"];

    public QuotationCancellationViewModel(MainWindowViewModel model, UiRecord quotation, Action completed, Action close)
    { this.model = model; this.quotation = quotation; this.completed = completed; this.close = close; }

    [RelayCommand]
    private async Task CancelQuotationAsync()
    {
        if (string.IsNullOrWhiteSpace(Reason)) { Error = "Cancellation reason is required."; return; }
        IsBusy = true; Error = "";
        try
        {
            if (!await model.CancelQuotationAsync(quotation, Reason)) { Error = model.Status; return; }
            completed(); close();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Close() => close();
}
