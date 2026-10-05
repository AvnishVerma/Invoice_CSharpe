using System.Linq.Expressions;

namespace LedgerNest.Domain;

public static class InvoiceStatusRules
{
    public const decimal MoneyTolerance = 0.005m;

    public static bool IsVoided(string? status) =>
        string.Equals(status, "Voided", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Declined", StringComparison.OrdinalIgnoreCase);

    // Reusable SQL predicate: voided documents remain visible, but are not receivables.
    public static readonly Expression<Func<Invoice, bool>> FinancialInvoices = invoice =>
        invoice.Type == "Invoice" && invoice.DeletedAt == null
        && invoice.Status.ToLower() != "voided" && invoice.Status.ToLower() != "declined";

    public static decimal Outstanding(Invoice invoice) => IsVoided(invoice.Status) || invoice.DeletedAt != null
        ? 0 : Math.Max(0, invoice.GrandTotal - invoice.PaidAmount) <= MoneyTolerance
            ? 0 : invoice.GrandTotal - invoice.PaidAmount;

    public static bool CanRequestPayment(Invoice invoice) =>
        invoice.Type.Equals("Invoice", StringComparison.OrdinalIgnoreCase) && Outstanding(invoice) > 0;

    public static void ValidateVoid(Invoice invoice, bool hasPayments)
    {
        if (invoice.DeletedAt != null) throw new InvalidOperationException("Invoice is unavailable or in trash.");
        if (invoice.Type != "Invoice") throw new InvalidOperationException("Only invoices can be voided.");
        if (IsVoided(invoice.Status)) throw new InvalidOperationException("This invoice is already voided.");
        if (Math.Abs(invoice.PaidAmount) > MoneyTolerance || hasPayments)
            throw new InvalidOperationException("Invoices with payments cannot be voided. Refund or remove the payment first.");
    }
}
