using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts the customer fields and actions in the invoice editor's XAML customer panel.</summary>
public sealed partial class InvoiceCustomerPanelView : UserControl
{
    public InvoiceCustomerPanelView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => (DataContext as InvoiceCustomerPanelViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as InvoiceCustomerPanelViewModel)?.Detach();
    }

    public InvoiceCustomerPanelView(MainWindowViewModel workspace, Action selectCustomer)
        : this()
    {
        DataContext = new InvoiceCustomerPanelViewModel(workspace, selectCustomer);
    }
}
