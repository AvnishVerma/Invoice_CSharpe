using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    private const string ProductDetailsNoticeDismissedKey = "product_details_notice.dismissed";

    /// <summary>Returns whether the optional Product Details notice has not yet been opened.</summary>
    public bool ShouldShowProductDetailsNotice()
    {
        if (dbFactory == null) return true;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return db.Settings.AsNoTracking().FirstOrDefault(setting => setting.Key == ProductDetailsNoticeDismissedKey)?.Value != "true";
    }

    /// <summary>Persists that the user has opened Product Details from its management notice.</summary>
    public void DismissProductDetailsNotice()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        SetSetting(db, ProductDetailsNoticeDismissedKey, "true");
        db.SaveChanges();
    }

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
        var currentUnit = record?["Unit"] ?? "None";
        fields.Single(field => field.Label == "Unit").Options = new[] { "None" }
            .Concat(UnitMaster.Units.Where(unit => unit.IsActive).Select(unit => unit.Code))
            .Append(currentUnit)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (record != null)
            foreach (var field in fields)
            {
                field.Value = record[field.Label];
                field.IsChecked = bool.TryParse(field.Value, out var enabled) && enabled;
            }
        else
        {
            fields.Single(f => f.Label == "Sale Price").Value = "";
            fields.Single(f => f.Label == "Purchase Price").Value = "";
            fields.Single(f => f.Label == "Tax (%)").Value = InvoiceSetting("Default Tax Rate (%)").Value;
            if (!ProductFieldVisible("Stock")) fields.Single(f => f.Label == "Unlimited stock").IsChecked = true;
        }
        return fields;
    }

    public string[] ActiveUnitCodes(string current = "None") => new[] { "None" }
        .Concat(UnitMaster.Units.Where(unit => unit.IsActive).Select(unit => unit.Code))
        .Append(current)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public SellingUnitChoice[] SellingUnitChoices(UiRecord product)
    {
        var baseUnit = product["Unit"] == "Custom…" ? product["Custom unit"] : product["Unit"];
        var fallback = new SellingUnitChoice(null, string.IsNullOrWhiteSpace(baseUnit) ? "None" : baseUnit, 1, ParseDecimal(product["Sale Price"]));
        if (dbFactory == null || product.SourceId <= 0) return [fallback];

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var units = (from sellingUnit in db.ProductSellingUnits.AsNoTracking()
                     join unit in db.Units.AsNoTracking() on sellingUnit.UnitId equals unit.Id
                     where sellingUnit.ProductId == product.SourceId && sellingUnit.IsActive && unit.IsActive
                     select new { SellingUnit = sellingUnit, unit.Code }).ToArray();
        if (units.Length == 0) return [fallback];
        var unitIds = units.Select(item => item.SellingUnit.Id).ToArray();
        var effectivePrices = db.ProductPrices.AsNoTracking()
            .Where(price => unitIds.Contains(price.SellingUnitId) && price.IsActive && price.EffectiveDate <= DateTime.UtcNow)
            .OrderByDescending(price => price.EffectiveDate).ThenByDescending(price => price.Id).ToArray()
            .GroupBy(price => price.SellingUnitId).ToDictionary(group => group.Key, group => group.First().SellingPrice);
        var productSalePrice = ParseDecimal(product["Sale Price"]);
        return units.Select(item => new SellingUnitChoice(item.SellingUnit.Id, item.Code,
                item.SellingUnit.ConversionFactor <= 0 ? 1 : item.SellingUnit.ConversionFactor,
                effectivePrices.TryGetValue(item.SellingUnit.Id, out var masterPrice)
                    ? masterPrice
                    : item.SellingUnit.SellingPrice > 0 ? item.SellingUnit.SellingPrice : productSalePrice))
            .Append(fallback)
            .GroupBy(choice => choice.Code, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
    }
}
