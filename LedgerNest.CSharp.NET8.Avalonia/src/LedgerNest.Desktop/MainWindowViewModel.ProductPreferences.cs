namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public FormField ProductSetting(string label) => Settings["Product Details"].SelectMany(s => s.Fields).Single(f => f.Label == label);

    public bool ProductFieldVisible(string label)
    {
        if (label is "Name" or "Price" or "Sale Price" or "Name / Alias") return true;
        var key = label switch
        {
            "Alias Name (for invoice PDF)" => "Alias Name",
            "Tax (%)" or "Price includes tax" => "Tax Rate",
            "Unlimited stock" => "Stock",
            "Custom unit" => "Unit",
            "Type" => "Product / Service Type",
            _ => label
        };
        var metadata = Settings["Product Details"].Single(s => s.Title == "Advanced Information").Fields;
        if (metadata.Any(f => f.Label == key) && !ProductSetting("Advanced Information").IsChecked) return false;
        return Settings["Product Details"].SelectMany(s => s.Fields).FirstOrDefault(f => f.Label == key)?.IsChecked ?? true;
    }

    public FormField[] ProductEditorFields(UiRecord? record = null)
    {
        var fields = FormCatalog.Product();
        if (record != null)
            foreach (var field in fields)
            {
                field.Value = record[field.Label];
                field.IsChecked = bool.TryParse(field.Value, out var enabled) && enabled;
            }
        else if (!ProductFieldVisible("Stock"))
            fields.Single(f => f.Label == "Unlimited stock").IsChecked = true;
        return fields;
    }
}
