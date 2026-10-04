using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed partial class PrinterSelectionViewModel : ObservableObject
{
    public IReadOnlyList<string> Printers { get; }
    [ObservableProperty] private string? selectedPrinter;
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand PrintCommand { get; }

    public PrinterSelectionViewModel(IReadOnlyList<string> printers, string? selected, Action<bool, string?> complete)
    {
        Printers = printers;
        SelectedPrinter = selected;
        CancelCommand = new RelayCommand(() => complete(false, null));
        PrintCommand = new RelayCommand(() => complete(true, SelectedPrinter));
    }
}
