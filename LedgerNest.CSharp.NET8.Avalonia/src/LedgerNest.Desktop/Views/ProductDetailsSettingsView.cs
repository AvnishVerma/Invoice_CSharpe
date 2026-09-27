using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void OpenProductDetailsSettings()
    {
        settingsTab = "Product Details";
        if (Model.Title == "Settings") ShowPage(); else Model.NavigateCommand.Execute("Settings");
    }
    private Control ProductDetailsSettingsView()
    {
        Control Row(string key, string title, string help, string icon, bool required = false)
        {
            var field = Model.ProductSetting(key);
            if (required) field.IsChecked = true;
            var row = InvoiceToggle(field, title, help, icon);
            row.IsEnabled = !required;
            if (required) row.Opacity = .45;
            return row;
        }
        var metadata = Ui.Stack(8);
        metadata.Margin = new Thickness(44, 4, 0, 8);
        foreach (var field in Model.Settings["Product Details"].Single(s => s.Title == "Advanced Information").Fields)
            metadata.Children.Add(InvoiceToggle(field, field.Label, "", "", leading: true));
        var master = Model.ProductSetting("Advanced Information");
        metadata.IsVisible = master.IsChecked;
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) =>
        { if (e.PropertyName == nameof(FormField.IsChecked)) metadata.IsVisible = master.IsChecked; };
        metadata.AttachedToVisualTree += (_, _) => master.PropertyChanged += changed;
        metadata.DetachedFromVisualTree += (_, _) => master.PropertyChanged -= changed;
        var requiredRows = Ui.Stack(6.4,
            Row("Name", "Name", "Always shown — required.", "label", true),
            Row("Price", "Price", "Always shown — required.", "payments", true),
            Row("Stock", "Stock", "Turn off if you never track stock — new products default to unlimited stock instead.", "inventory_2"));
        var productRows = Ui.Stack(6.4,
            Row("Alias Name", "Alias Name", "Local-language display name for PDFs/printing.", "translate"),
            Row("Tax Rate", "Tax Rate", "Per-product tax percentage.", "percent"),
            Row("HSN/SAC", "HSN/SAC", "HSN or SAC code field.", "qr_code_2"),
            Row("Description", "Description", "Free-text product description.", "view_list"),
            Row("Purchase Price", "Purchase Price", "Cost price, for margin tracking.", "shopping_cart"),
            Row("Default Discount", "Default Discount", "Pre-filled discount when adding this product to an invoice.", "discount"),
            Row("Unit", "Unit", "Unit of measure (pcs, kg, hrs…).", "straighten"),
            Row("Product / Service Type", "Product/Service Type", "Segmented Product vs Service selector.", "category"),
            Row("Advanced Information", "Product Metadata", "Storage location, container/batch number, expiry, manufacture date, manufacturer, supplier, SKU, notes.", "more_horiz"));
        var invoiceRows = Ui.Stack(6.4, Row("Extra Cost", "Extra Cost", "Optional flat extra charge on an invoice line item.", "payments"));
        var save = Ui.Button("Save", () => Model.SaveSettings("Product Details"), true);
        save.Background = Ui.HeaderBand; save.MinHeight = 25.6; save.Height = 25.6; save.Padding = new Thickness(6.4, 4.8);
        save.HorizontalAlignment = HorizontalAlignment.Stretch;
        save.Content = Ui.Columns("Auto,8,Auto", Ui.Icon("save", 17, Brushes.White), new Border(), Ui.LocalText("Save", 13, true, Brushes.White));
        var rail = new SettingsSaveRailView(save);
        var layout = new SettingsWorkspaceView(rail, new ProductDetailsFormView(requiredRows, productRows, metadata, invoiceRows), railAfterContentOnNarrow: true);
        return new ScreenScaffoldView(Ui.AppBar("Customize Product Details"), layout);
    }
}
