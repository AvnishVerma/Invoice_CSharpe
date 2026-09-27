using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts the customer fields and actions in the invoice editor's XAML customer panel.</summary>
public sealed partial class InvoiceCustomerPanelView : UserControl
{
    public InvoiceCustomerPanelView()
    {
        InitializeComponent();
    }

    public InvoiceCustomerPanelView(Control selectCustomer, Control fields, Control details)
        : this()
    {
        SelectCustomerHost.Content = selectCustomer;
        CustomerFieldsHost.Content = fields;
        CustomerDetailsHost.Content = details;
    }
}
