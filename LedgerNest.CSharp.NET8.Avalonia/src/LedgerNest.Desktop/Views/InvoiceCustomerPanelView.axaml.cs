using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Hosts the customer fields and actions in the invoice editor's XAML customer panel.</summary>
public sealed partial class InvoiceCustomerPanelView : UserControl
{
    public InvoiceCustomerPanelView()
    {
        InitializeComponent();
    }

    public InvoiceCustomerPanelView(Control selectCustomer, Control customerName, Control customerPhone, Control detailsToggle, Control details)
        : this()
    {
        SelectCustomerHost.Content = selectCustomer;
        CustomerNameHost.Content = customerName;
        CustomerPhoneHost.Content = customerPhone;
        DetailsToggleHost.Content = detailsToggle;
        CustomerDetailsHost.Content = details;
    }
}
