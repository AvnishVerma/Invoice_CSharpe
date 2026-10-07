using System.Globalization;

namespace LedgerNest.Domain;

public static class InvoiceEditRules
{
    public static void Validate(Invoice invoice, decimal total, decimal recordedPayments)
    {
        if (invoice.DeletedAt != null || InvoiceStatusRules.IsVoided(invoice.Status)
            || string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(invoice.Status, "Converted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This document is no longer eligible for editing.");
        if (total < 0 || invoice.PaidAmount < 0 || recordedPayments < 0)
            throw new InvalidOperationException("Invoice and payment amounts must not be negative.");
        if (recordedPayments - invoice.PaidAmount > InvoiceStatusRules.MoneyTolerance)
            throw new InvalidOperationException("Payment history does not match the stored paid amount. Reconcile payments before editing.");
        if (invoice.PaidAmount - total > InvoiceStatusRules.MoneyTolerance)
            throw new InvalidOperationException("Invoice total cannot be less than the amount already paid.");
    }

    public static DateTime ResolveDate(string date, string time, DateTimeKind kind = DateTimeKind.Unspecified)
    {
        if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            throw new InvalidOperationException("Enter a valid invoice date.");
        string[] formats = ["h\\:mm", "hh\\:mm", "h\\:mm\\:ss", "hh\\:mm\\:ss", "hh\\:mm\\:ss\\.FFFFFFF"];
        if (!TimeSpan.TryParseExact(time, formats, CultureInfo.InvariantCulture, out var orderTime) || orderTime < TimeSpan.Zero || orderTime >= TimeSpan.FromDays(1))
            throw new InvalidOperationException("Enter a valid order time, for example 14:30 or 14:30:45.");
        return DateTime.SpecifyKind(day.Add(orderTime), kind);
    }

    public static string PaymentStatus(decimal total, decimal paid) => total - paid <= InvoiceStatusRules.MoneyTolerance
        ? "Paid" : paid > InvoiceStatusRules.MoneyTolerance ? "Partial" : "Unpaid";
}
