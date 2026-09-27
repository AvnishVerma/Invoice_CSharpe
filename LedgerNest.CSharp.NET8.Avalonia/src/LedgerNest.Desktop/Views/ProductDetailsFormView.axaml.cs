using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductDetailsFormView : UserControl
{
    public ProductDetailsFormView() => InitializeComponent();

    public ProductDetailsFormView(Control requiredRows, Control productRows, Control metadata, Control invoiceRows) : this()
    {
        RequiredRowsHost.Content = requiredRows;
        ProductRowsHost.Content = productRows;
        MetadataHost.Content = metadata;
        InvoiceRowsHost.Content = invoiceRows;
    }
}
