using System.Text.Json;

namespace LedgerNest.Desktop;

public enum InvoiceLeaveChoice { KeepEditing, Discard, Save }

public partial class MainWindowViewModel
{
    private string? cleanInvoiceSnapshot;
    private Action? pendingInvoiceNavigation;
    private bool resumingInvoiceNavigation;
    public event Action? InvoiceLeaveRequested;
    public bool HasPendingInvoiceNavigation => pendingInvoiceNavigation != null;
    public bool HasUnsavedInvoiceChanges => cleanInvoiceSnapshot != null && cleanInvoiceSnapshot != CurrentInvoiceFormSnapshot();

    private string CurrentInvoiceFormSnapshot() => JsonSerializer.Serialize(new
    {
        Customer = InvoiceCustomer.Select(field => field.Value).ToArray(), SelectedInvoiceCustomerId,
        Details = InvoiceDetails.Select(field => field.Value).ToArray(), Time = OrderTime.Value,
        Options = InvoiceOptions.Select(field => field.Value).ToArray(),
        Costs = AdditionalCosts.Select(row => row.Select(field => field.Value).ToArray()).ToArray(),
        CustomFields = InvoiceCustomFields.Select(entry => new { entry.Id, entry.Field.Value }).ToArray(),
        HideNumber = HideInvoiceNumber.IsChecked, Interstate = InterState.IsChecked,
        Bank = SelectedInvoiceBank?.Account, Currency = EditorCurrency,
        Upi = UpiAccounts.Select(account => account.Select(field => field.Value).ToArray()).ToArray(),
        Lines = Lines.Select(line => new { line.ProductKey, line.ProductType, line.Unit, line.Name, line.Quantity, line.Price, line.Discount, line.DiscountPerUnit, line.TaxRate, line.PriceIncludesTax, line.ExtraCost, line.SellingUnitId, line.UnitConversionFactor }).ToArray()
    });

    public void MarkInvoiceFormClean() => cleanInvoiceSnapshot = CurrentInvoiceFormSnapshot();

    public bool RequestInvoiceLeave(Action navigation)
    {
        if (resumingInvoiceNavigation || Title != "New Invoice" || !HasUnsavedInvoiceChanges) return true;
        if (pendingInvoiceNavigation != null) return false;
        pendingInvoiceNavigation = navigation;
        InvoiceLeaveRequested?.Invoke();
        return false;
    }

    public bool ResolveInvoiceLeave(InvoiceLeaveChoice choice)
    {
        if (pendingInvoiceNavigation == null) return false;
        if (choice == InvoiceLeaveChoice.KeepEditing) { pendingInvoiceNavigation = null; return false; }
        if (choice == InvoiceLeaveChoice.Save && !SaveInvoice()) return false;
        var navigation = pendingInvoiceNavigation;
        pendingInvoiceNavigation = null;
        resumingInvoiceNavigation = true;
        try
        {
            if (choice == InvoiceLeaveChoice.Discard) ResetInvoiceForm(InvoiceDetails[0].Value);
            navigation();
            return true;
        }
        finally { resumingInvoiceNavigation = false; }
    }
}
