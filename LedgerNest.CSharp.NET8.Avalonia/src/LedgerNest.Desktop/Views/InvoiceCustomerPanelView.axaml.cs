using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts the customer fields and actions in the invoice editor's XAML customer panel.</summary>
public sealed partial class InvoiceCustomerPanelView : UserControl
{
    public InvoiceCustomerPanelView()
    {
        InitializeComponent();
        SizeChanged += (_, args) => { if (DataContext is InvoiceCustomerPanelViewModel model) model.CustomerColumns = args.NewSize.Width < 450 ? 1 : args.NewSize.Width < 600 ? 2 : 3; };
        AttachedToVisualTree += (_, _) => (DataContext as InvoiceCustomerPanelViewModel)?.Attach();
        DetachedFromVisualTree += (_, _) => (DataContext as InvoiceCustomerPanelViewModel)?.Detach();
    }

    public InvoiceCustomerPanelView(MainWindowViewModel workspace, Action selectCustomer)
        : this()
    {
        DataContext = new InvoiceCustomerPanelViewModel(workspace, selectCustomer);
    }
}
