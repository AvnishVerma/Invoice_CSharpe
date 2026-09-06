namespace Invoiso.Application;

public enum InvoiceTaxMode { PerItem, Global, None }
public enum InvoiceDiscountKind { None, Amount, Percent }
public readonly record struct InvoiceLineInput(decimal Price, decimal Quantity, decimal Discount = 0, bool DiscountPerUnit = false, decimal ExtraCost = 0, decimal TaxRatePercent = 0, bool PriceIncludesTax = false);
public readonly record struct CalculatedInvoiceTotals(decimal Subtotal, decimal GrossSubtotal, decimal ItemDiscount, decimal Tax, decimal AdditionalCosts, decimal InvoiceDiscount)
{
    public decimal Total => Math.Max(0, Subtotal + Tax + AdditionalCosts - InvoiceDiscount);
}

/// <summary>Decimal port of the legacy invoice_totals_calculator.dart rules. Round only for display.</summary>
public static class InvoiceTotalsCalculator
{
    public static CalculatedInvoiceTotals Calculate(IEnumerable<InvoiceLineInput> lines, InvoiceTaxMode mode = InvoiceTaxMode.PerItem, decimal globalTaxPercent = 0, decimal additionalCosts = 0, InvoiceDiscountKind discountKind = InvoiceDiscountKind.None, decimal discountValue = 0)
    {
        decimal subtotal = 0, gross = 0, discounts = 0, itemTax = 0;
        foreach (var line in lines)
        {
            var discount = line.DiscountPerUnit ? line.Discount * line.Quantity : line.Discount;
            var displayTotal = line.Price * line.Quantity - discount + line.ExtraCost;
            var backOutRate = mode == InvoiceTaxMode.Global ? globalTaxPercent : line.TaxRatePercent;
            var divisor = line.PriceIncludesTax && backOutRate > 0 ? 1 + backOutRate / 100 : 1;
            var taxable = displayTotal / divisor;
            subtotal += taxable;
            gross += (line.Price * line.Quantity + line.ExtraCost) / divisor;
            discounts += discount;
            if (mode == InvoiceTaxMode.PerItem) itemTax += taxable * line.TaxRatePercent / 100;
        }
        var tax = mode switch { InvoiceTaxMode.Global => subtotal * globalTaxPercent / 100, InvoiceTaxMode.PerItem => itemTax, _ => 0 };
        var invoiceDiscount = discountValue <= 0 ? 0 : discountKind switch { InvoiceDiscountKind.Percent => (subtotal + tax + additionalCosts) * discountValue / 100, InvoiceDiscountKind.Amount => discountValue, _ => 0 };
        return new(subtotal, gross, discounts, tax, additionalCosts, invoiceDiscount);
    }
}
