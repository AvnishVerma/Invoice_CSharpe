using LedgerNest.Domain;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public UiRecord[] SearchProducts(string? query) => Products
        .Select(product => (Product: product, Score: ProductSearchRules.Score(ToSearchEntry(product), query)))
        .Where(result => result.Score > 0)
        .OrderByDescending(result => result.Score)
        .ThenBy(result => result.Product.Name, StringComparer.OrdinalIgnoreCase)
        .Select(result => result.Product).ToArray();

    public UiRecord[] ExactProductCodeMatches(string? query)
    {
        var value = query?.Trim() ?? "";
        if (value.Length == 0) return [];
        return Products.Where(product => new[] { product["Barcode"], product["Product ID"], product["SKU Code"] }
            .Any(code => code.Equals(value, StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    private static ProductSearchEntry ToSearchEntry(UiRecord product) => new(product.SourceId, product.Name,
        product["Alias Name (for invoice PDF)"], product["Product ID"], product["SKU Code"], product["Barcode"], product["HSN/SAC"], product["Description"]);
}
