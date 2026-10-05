namespace LedgerNest.Application;

public sealed record InventoryValuationInput(string Name, decimal Stock, decimal PurchasePrice, decimal SalePrice, string Type, bool UnlimitedStock);
public sealed record InventoryValuationItem(string Name, decimal Stock, decimal PurchasePrice, decimal SalePrice, decimal StockValue, decimal SaleValue);
public sealed record InventoryValuationResult(decimal StockValue, decimal SaleValue, decimal PotentialProfit, decimal Units, int ExcludedCount, InventoryValuationItem[] Items);

public static class InventoryValuationCalculator
{
    public static InventoryValuationResult Calculate(IEnumerable<InventoryValuationInput> products)
    {
        var source = products.ToArray();
        var items = source.Where(product => !product.Type.Equals("Service", StringComparison.OrdinalIgnoreCase) && !product.UnlimitedStock)
            .Select(product =>
            {
                // Preserve the application's existing nonnegative available-stock valuation.
                var stock = Math.Max(0, product.Stock);
                var purchase = Math.Max(0, product.PurchasePrice);
                var sale = Math.Max(0, product.SalePrice);
                return new InventoryValuationItem(product.Name, stock, purchase, sale, stock * purchase, stock * sale);
            }).OrderByDescending(product => product.StockValue).ThenBy(product => product.Name).ToArray();
        return new(items.Sum(item => item.StockValue), items.Sum(item => item.SaleValue),
            items.Sum(item => item.SaleValue - item.StockValue), items.Sum(item => item.Stock), source.Length - items.Length, items);
    }
}
