using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class ProductViewDialog : UserControl
{
    // Performs the product view dialog initialization action for this screen or workflow.
    public ProductViewDialog()
    {
        InitializeComponent();
    }

    // Performs the product view dialog data context assignment action for this screen or workflow.
    public ProductViewDialog(ProductViewDialogModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}

// Provides read-only product details for the AXAML product view dialog.
public sealed class ProductViewDialogModel
{
    public string ProductName { get; init; } = "—";
    public string AliasName { get; init; } = "—";
    public string Description { get; init; } = "—";
    public string HsnSac { get; init; } = "—";
    public string Price { get; init; } = CurrencyDisplay.Format(0, "0.0");
    public string PurchasePrice { get; init; } = CurrencyDisplay.Format(0, "0.0");
    public string DefaultDiscount { get; init; } = CurrencyDisplay.Format(0, "0.0");
    public string TaxRate { get; init; } = "0";
    public bool PriceIncludesTax { get; init; }
    public string Type { get; init; } = "Product";
}
