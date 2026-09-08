using LedgerNest.Domain;

namespace LedgerNest.Application;

public sealed class InvoiceService
{
    public InvoiceTotals CalculateTotals(IEnumerable<InvoiceItem> items)
    {
        var totals = InvoiceTotalsCalculator.Calculate(items.Select(item => new InvoiceLineInput(
            item.UnitPrice, item.Quantity, item.Discount, item.DiscountPerUnit,
            item.ExtraCost, item.TaxRate, item.PriceIncludesTax)));

        return new InvoiceTotals(totals.Subtotal, totals.Tax, totals.ItemDiscount, totals.Total);
    }
}

public readonly record struct InvoiceTotals(
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal GrandTotal);
