using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductRecordFormView : UserControl
{
    // Performs the product record form view initialization action for this screen or workflow.
    public ProductRecordFormView()
    {
        InitializeComponent();
    }

    // Performs the product record form section assignment action for this screen or workflow.
    public ProductRecordFormView(Control general, Control pricing, Control stock, Control advanced)
    {
        InitializeComponent();
        GeneralHost.Content = general;
        PricingHost.Content = pricing;
        StockHost.Content = stock;
        AdvancedHost.Content = advanced;
    }
}
