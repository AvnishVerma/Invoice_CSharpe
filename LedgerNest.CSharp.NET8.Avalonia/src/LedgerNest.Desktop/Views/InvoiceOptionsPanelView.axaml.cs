using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Provides the XAML container for invoice options and additional settings.</summary>
public sealed partial class InvoiceOptionsPanelView : UserControl
{
    public InvoiceOptionsPanelView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as InvoiceOptionsViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as InvoiceOptionsViewModel)?.Detach();
    }

    public InvoiceOptionsPanelView(MainWindowViewModel workspace)
        : this()
    {
        DataContext = new InvoiceOptionsViewModel(workspace);
    }
}
