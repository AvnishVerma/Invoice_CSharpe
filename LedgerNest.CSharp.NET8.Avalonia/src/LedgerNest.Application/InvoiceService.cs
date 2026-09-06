using LedgerNest.Domain;

namespace LedgerNest.Application;

public sealed class InvoiceService
{
    public InvoiceTotals CalculateTotals(IEnumerable<InvoiceItem> items)
    {
        decimal subTotal = 0m;
        decimal taxTotal = 0m;
        decimal discountTotal = 0m;

        foreach (var item in items)
        {
            var gross = item.Quantity * item.UnitPrice;
            var net = Math.Max(0m, gross - item.Discount);
            var tax = Math.Round(net * item.TaxRate / 100m, 2);

            subTotal += net;
            taxTotal += tax;
            discountTotal += item.Discount;
        }

        return new InvoiceTotals(
            subTotal,
            taxTotal,
            discountTotal,
            subTotal + taxTotal);
    }
}

public readonly record struct InvoiceTotals(
    decimal SubTotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal GrandTotal);
