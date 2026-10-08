using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace LedgerNest.Desktop;

public sealed record ProductFieldOption(FormField Field, string Title, string Help, string Icon, bool Required = false);

public sealed class ProductDetailsSettingsViewModel
{
    private readonly MainWindowViewModel model;
    public ObservableCollection<ProductFieldOption> RequiredFields { get; } = [];
    public ObservableCollection<ProductFieldOption> ProductFields { get; } = [];
    public ObservableCollection<ProductFieldOption> MetadataFields { get; } = [];
    public ObservableCollection<ProductFieldOption> InvoiceFields { get; } = [];
    public FormField MetadataMaster { get; }
    public IRelayCommand SaveCommand { get; }
    public MasterCodeSettingsViewModel CodeSettings { get; }

    public ProductDetailsSettingsViewModel(MainWindowViewModel model)
    {
        this.model = model;
        CodeSettings = model.CreateMasterCodeSettings("Product");
        ProductFieldOption Option(string key, string title, string help, string icon, bool required = false)
        {
            var field = model.ProductSetting(key);
            if (required) field.IsChecked = true;
            return new ProductFieldOption(field, title, help, icon, required);
        }
        RequiredFields.Add(Option("Name", "Name", "Always shown — required.", "label", true));
        RequiredFields.Add(Option("Price", "Price", "Always shown — required.", "payments", true));
        RequiredFields.Add(Option("Stock", "Stock", "Turn off if you never track stock — new products default to unlimited stock instead.", "inventory_2"));
        ProductFields.Add(Option("Alias Name", "Alias Name", "Local-language display name for PDFs/printing.", "translate"));
        ProductFields.Add(Option("Tax Rate", "Tax Rate", "Per-product tax percentage.", "percent"));
        ProductFields.Add(Option("HSN/SAC", "HSN/SAC", "HSN or SAC code field.", "qr_code_2"));
        ProductFields.Add(Option("Description", "Description", "Free-text product description.", "view_list"));
        ProductFields.Add(Option("Purchase Price", "Purchase Price", "Cost price, for margin tracking.", "shopping_cart"));
        ProductFields.Add(Option("Default Discount", "Default Discount", "Pre-filled discount when adding this product to an invoice.", "discount"));
        ProductFields.Add(Option("Unit", "Unit", "Unit of measure (pcs, kg, hrs…).", "straighten"));
        ProductFields.Add(Option("Product / Service Type", "Product/Service Type", "Segmented Product vs Service selector.", "category"));
        ProductFields.Add(Option("Advanced Information", "Product Metadata", "Storage location, batch, expiry, manufacturer, supplier, SKU, and notes.", "more_horiz"));
        MetadataMaster = model.ProductSetting("Advanced Information");
        foreach (var field in model.Settings["Product Details"].Single(section => section.Title == "Advanced Information").Fields)
            MetadataFields.Add(new ProductFieldOption(field, field.Label, "", ""));
        InvoiceFields.Add(Option("Extra Cost", "Extra Cost", "Optional flat extra charge on an invoice line item.", "payments"));
        SaveCommand = new RelayCommand(() => model.SaveSettings("Product Details"));
    }
}
