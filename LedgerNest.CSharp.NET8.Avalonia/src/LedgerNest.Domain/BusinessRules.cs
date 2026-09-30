using System.Globalization;

namespace LedgerNest.Domain;

public static class MasterNumberRules
{
    public static string Format(string prefix, long value, int length)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
        if (length is < 1 or > 18) throw new ArgumentOutOfRangeException(nameof(length));
        return prefix.Trim() + value.ToString("D" + length, CultureInfo.InvariantCulture);
    }
}

public static class SellingUnitRules
{
    public static decimal ToBaseQuantity(decimal sellingQuantity, decimal conversionFactor)
    {
        if (sellingQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(sellingQuantity));
        if (conversionFactor <= 0) throw new ArgumentOutOfRangeException(nameof(conversionFactor));
        return sellingQuantity * conversionFactor;
    }

    public static decimal Amount(decimal sellingQuantity, decimal sellingPrice)
    {
        if (sellingQuantity <= 0 || sellingPrice < 0) throw new ArgumentOutOfRangeException(nameof(sellingQuantity));
        return decimal.Round(sellingQuantity * sellingPrice, 2, MidpointRounding.AwayFromZero);
    }
}

public static class InventoryRules
{
    public static decimal CurrentStock(decimal opening, IEnumerable<decimal> movements) => opening + movements.Sum();
    public static decimal Apply(decimal current, decimal change, bool allowNegative = false)
    {
        var result = current + change;
        if (!allowNegative && result < 0) throw new InvalidOperationException("Inventory movement would make stock negative.");
        return result;
    }
}

public static class RefundRules
{
    public static void ValidateQuantity(decimal sold, decimal previouslyRefunded, decimal requested)
    {
        if (requested <= 0) throw new InvalidOperationException("Refund quantity must be positive.");
        if (previouslyRefunded < 0 || sold < 0 || previouslyRefunded + requested > sold)
            throw new InvalidOperationException("Refund quantity exceeds the remaining sold quantity.");
    }

    public static (decimal Subtotal, decimal Tax, decimal Total) Calculate(decimal quantity, decimal unitPrice, decimal taxRate, bool priceIncludesTax)
    {
        ValidateQuantity(decimal.MaxValue, 0, quantity);
        if (unitPrice < 0 || taxRate < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        var gross = quantity * unitPrice;
        var tax = priceIncludesTax ? gross - gross / (1 + taxRate / 100m) : gross * taxRate / 100m;
        var subtotal = priceIncludesTax ? gross - tax : gross;
        return (decimal.Round(subtotal, 2), decimal.Round(tax, 2), decimal.Round(subtotal + tax, 2));
    }
}

public static class QuotationRules
{
    public static bool CanCancel(string status) => status is "Open" or "Draft" or "Expired";
}

public static class PermissionRules
{
    public static bool IsAllowed(string role, string resource, string action, IEnumerable<RolePermission> permissions) =>
        role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || permissions.Any(item =>
            item.IsAllowed && item.Role.Equals(role, StringComparison.OrdinalIgnoreCase) &&
            item.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) && item.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
}

public sealed record ProductSearchEntry(int Id, string Name, string Alias, string ProductCode, string Sku, string Barcode, string Hsn, string Description);

public static class ProductSearchRules
{
    public static int Score(ProductSearchEntry product, string? query)
    {
        var normalized = query?.Trim() ?? "";
        if (normalized.Length == 0) return 1;
        var fields = new[] { product.Name, product.Alias, product.ProductCode, product.Sku, product.Barcode, product.Hsn, product.Description };
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Any(term => !fields.Any(field => field.Contains(term, StringComparison.OrdinalIgnoreCase)))) return 0;
        if (new[] { product.Barcode, product.ProductCode, product.Sku }.Any(field => field.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return 1000;
        if (product.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return 900;
        if (product.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)) return 700;
        if (product.Alias.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)) return 600;
        return 100 + terms.Length * 10;
    }
}

public sealed record BarcodeLabelData(string ProductName, string Code, decimal Price);

public static class BarcodeRules
{
    private const string Allowed = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%";
    public static string NormalizeCode39(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        return new string(normalized.Select(character => Allowed.Contains(character) ? character : '-').ToArray());
    }

    public static BarcodeLabelData[] Expand(IEnumerable<(BarcodeLabelData Label, int Quantity)> selections, int maximumLabels = 1000)
    {
        if (maximumLabels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumLabels));
        var labels = selections.Where(item => item.Quantity > 0)
            .SelectMany(item => Enumerable.Repeat(item.Label with { Code = NormalizeCode39(item.Label.Code) }, item.Quantity)).ToArray();
        if (labels.Length > maximumLabels) throw new InvalidOperationException($"A maximum of {maximumLabels} barcode labels can be generated at once.");
        if (labels.Any(label => label.Code.Length == 0)) throw new InvalidOperationException("Every barcode label requires a barcode or product ID.");
        return labels;
    }
}
