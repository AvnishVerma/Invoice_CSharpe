using CommunityToolkit.Mvvm.ComponentModel;
using System.Data.Common;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    [ObservableProperty] private int? selectedInvoiceCustomerId;
    [ObservableProperty] private bool customerFieldsUnlocked;
    private InvoiceCustomerSnapshot? selectedCustomerIdentity;
    public bool HasSelectedInvoiceCustomer => SelectedInvoiceCustomerId != null;
    public bool CustomerIdentityLocked => HasSelectedInvoiceCustomer && !CustomerFieldsUnlocked;
    public bool CanSaveInvoiceCustomer
    {
        get
        {
            try { return HasPermission("Customer", HasSelectedInvoiceCustomer ? "Update" : "Add"); }
            catch (Exception ex) when (ex is DbException or InvalidOperationException or IOException) { return false; }
        }
    }
    public string CustomerLockIcon => CustomerIdentityLocked ? "edit" : "lock";
    public string CustomerLockTooltip => CustomerIdentityLocked ? "Unlock customer fields" : "Lock customer fields";
    public event Action? InvoiceCustomerStateChanged;
    partial void OnSelectedInvoiceCustomerIdChanged(int? value) => UpdateCustomerFieldLocks();
    partial void OnCustomerFieldsUnlockedChanged(bool value) => UpdateCustomerFieldLocks();
    private void UpdateCustomerFieldLocks()
    {
        for (var index = 0; index < InvoiceCustomer.Length; index++) InvoiceCustomer[index].IsReadOnly = index != 5 && CustomerIdentityLocked;
        OnPropertyChanged(nameof(HasSelectedInvoiceCustomer)); OnPropertyChanged(nameof(CustomerIdentityLocked));
        OnPropertyChanged(nameof(CanSaveInvoiceCustomer));
        OnPropertyChanged(nameof(CustomerLockIcon)); OnPropertyChanged(nameof(CustomerLockTooltip));
    }
    private InvoiceCustomerSnapshot CustomerFormSnapshot() => new(InvoiceCustomer[0].Value, InvoiceCustomer[1].Value,
        InvoiceCustomer[2].Value, InvoiceCustomer[3].Value, InvoiceCustomer[4].Value, InvoiceCustomer[5].Value);
    private void ApplyCustomerFields(Customer customer)
    {
        string[] values = [customer.Name, customer.BusinessName, customer.Phone ?? "", customer.Email ?? "", customer.GstNumber ?? "", customer.Address ?? "", customer.CustomerCode ?? ""];
        for (var index = 0; index < Math.Min(values.Length, InvoiceCustomer.Length); index++) { InvoiceCustomer[index].Value = values[index]; InvoiceCustomer[index].Error = ""; }
    }
    public bool SelectInvoiceCustomer(UiRecord record) => RunCustomerOperation("load", () => SelectInvoiceCustomerCore(record));
    private bool RunCustomerOperation(string operation, Func<bool> action)
    {
        try { return action(); }
        catch (Exception ex) when (ex is DbException or DbUpdateException or InvalidOperationException or IOException)
        { Status = $"Unable to {operation} customer details: {ex.Message}"; return false; }
    }
    private bool SelectInvoiceCustomerCore(UiRecord record)
    {
        var resource = InvoiceDetails[0].Value == "Quotation" ? "Quotation" : "Invoice";
        if (!HasPermission(resource, "View") || dbFactory == null || record.SourceId <= 0) { Status = "Customer selection is unavailable."; return false; }
        using var db = dbFactory.CreateDbContext();
        var customer = db.Customers.AsNoTracking().SingleOrDefault(item => item.Id == record.SourceId);
        if (customer == null) { Status = "Customer no longer exists. Refresh the customer list."; return false; }
        ApplyCustomerFields(customer); selectedCustomerIdentity = CustomerIdentityRules.Snapshot(customer);
        SelectedInvoiceCustomerId = customer.Id; CustomerFieldsUnlocked = false; UpdateCustomerFieldLocks();
        InvoiceCustomerStateChanged?.Invoke(); return true;
    }
    public void ClearInvoiceCustomer()
    {
        ResetCustomerSelection(); foreach (var field in InvoiceCustomer) { field.Value = ""; field.Error = ""; }
    }
    private void ResetCustomerSelection()
    {
        selectedCustomerIdentity = null; SelectedInvoiceCustomerId = null; CustomerFieldsUnlocked = false;
        if (InvoiceCustomer.Length > 6) InvoiceCustomer[6].Value = "";
        UpdateCustomerFieldLocks(); InvoiceCustomerStateChanged?.Invoke();
    }
    public bool RefreshInvoiceCustomer()
    {
        if (SelectedInvoiceCustomerId is not { } id) { ResetCustomerSelection(); return true; }
        if (SelectInvoiceCustomer(new UiRecord { SourceId = id })) return true;
        ResetCustomerSelection(); return false;
    }
    private void RestoreCustomerSelection(int? id)
    {
        ResetCustomerSelection();
        if (id == null || dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        var master = db.Customers.AsNoTracking().SingleOrDefault(customer => customer.Id == id);
        if (master == null || !CustomerIdentityRules.Matches(CustomerFormSnapshot(), CustomerIdentityRules.Snapshot(master))) return;
        selectedCustomerIdentity = CustomerIdentityRules.Snapshot(master); SelectedInvoiceCustomerId = id;
        if (InvoiceCustomer.Length > 6) InvoiceCustomer[6].Value = master.CustomerCode ?? "";
        UpdateCustomerFieldLocks();
    }
    private int? ResolveInvoiceCustomerId(LedgerNestDbContext db)
    {
        var form = CustomerFormSnapshot();
        if (SelectedInvoiceCustomerId is { } id)
        {
            var master = db.Customers.AsNoTracking().SingleOrDefault(customer => customer.Id == id);
            return master != null && CustomerIdentityRules.Matches(form, CustomerIdentityRules.Snapshot(master)) ? id : null;
        }
        // Never select an arbitrary first row when names/identities are ambiguous.
        return CustomerIdentityRules.FindUniqueId(form, CustomerIdentityCandidates(db, form).AsEnumerable());
    }
    private static IQueryable<Customer> CustomerIdentityCandidates(LedgerNestDbContext db, InvoiceCustomerSnapshot form)
    {
        var phone = form.Phone.Trim().ToLowerInvariant();
        // Compare names in Domain after narrowing candidates. SQLite LOWER does
        // not provide the same Unicode name semantics as OrdinalIgnoreCase.
        return phone.Length == 0
            ? db.Customers.AsNoTracking().Where(customer => customer.Phone == null || customer.Phone.Trim() == "")
            : db.Customers.AsNoTracking().Where(customer => customer.Phone != null && customer.Phone.Trim().ToLower() == phone);
    }
    public bool SaveInvoiceCustomer() => RunCustomerOperation("save", SaveInvoiceCustomerCore);
    private bool SaveInvoiceCustomerCore()
    {
        if (!HasPermission("Customer", HasSelectedInvoiceCustomer ? "Update" : "Add")) { Status = "Your role cannot save customer details."; return false; }
        if (dbFactory == null) { Status = "Customer storage is unavailable."; return false; }
        using var db = dbFactory.CreateDbContext();
        var form = CustomerFormSnapshot();
        int? id = SelectedInvoiceCustomerId;
        if (id != null)
        {
            var master = db.Customers.AsNoTracking().SingleOrDefault(customer => customer.Id == id);
            if (master == null || selectedCustomerIdentity == null || !CustomerIdentityRules.Matches(selectedCustomerIdentity, CustomerIdentityRules.Snapshot(master)))
            { Status = "Selected customer changed or no longer exists. Refresh before saving."; return false; }
        }
        else
        {
            var candidates = CustomerIdentityCandidates(db, form).AsEnumerable()
                .Where(customer => CustomerIdentityRules.Matches(form, CustomerIdentityRules.Snapshot(customer))).Take(2).ToArray();
            if (candidates.Length > 1) { Status = "Multiple customers match these details. Select the intended customer."; return false; }
            id = candidates.SingleOrDefault()?.Id;
        }
        var original = id == null ? null : Customers.SingleOrDefault(customer => customer.SourceId == id);
        if (id != null && original == null) { Status = "Refresh the customer list before saving."; return false; }
        if (original != null && InvoiceCustomer.Length > 6 && InvoiceCustomer[6].Value.Length == 0) InvoiceCustomer[6].Value = original["Customer ID"];
        if (!SaveRecord("Customer", InvoiceCustomer, original)) return false;
        var saved = id != null ? Customers.Single(customer => customer.SourceId == id) : Customers.Last();
        if (!SelectInvoiceCustomer(saved))
        {
            var reason = Status; ResetCustomerSelection();
            Status = "Customer saved, but the editor could not reload the saved details. Refresh the customer. " + reason;
        }
        return true;
    }
}
