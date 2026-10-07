namespace LedgerNest.Domain;

public static class CustomerIdentityRules
{
    public static InvoiceCustomerSnapshot Snapshot(Customer customer) => new(customer.Name, customer.BusinessName,
        customer.Phone ?? "", customer.Email ?? "", customer.GstNumber ?? "", customer.Address ?? "");
    // Address is a document-specific snapshot, not an identity-bearing field.
    public static bool Matches(InvoiceCustomerSnapshot left, InvoiceCustomerSnapshot right) =>
        Equal(left.Name, right.Name) && Equal(left.BusinessName, right.BusinessName)
        && Equal(left.Phone, right.Phone) && Equal(left.Email, right.Email) && Equal(left.GstNumber, right.GstNumber);
    public static bool Equal(string? left, string? right) => string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
    public static int? FindUniqueId(InvoiceCustomerSnapshot form, IEnumerable<Customer> customers)
    {
        if (string.IsNullOrWhiteSpace(form.Name)) return null;
        var matches = customers.Where(customer => Matches(form, Snapshot(customer))).Take(2).ToArray();
        return matches.Length == 1 ? matches[0].Id : null;
    }
}
