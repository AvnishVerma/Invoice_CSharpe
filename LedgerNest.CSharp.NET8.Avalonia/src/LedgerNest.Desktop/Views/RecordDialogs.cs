using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the edit record action for this screen or workflow.
    internal void EditRecord(string kind, Action refresh, UiRecord? record = null)
    {
        if (!Model.HasPermission(kind, record == null ? "Add" : "Update"))
        { Model.Status = $"You do not have permission to {(record == null ? "create" : "edit")} {kind.ToLowerInvariant()} records."; return; }
        var fields = kind == "Customer" ? FormCatalog.Customer() : kind == "Product" ? Model.ProductEditorFields(record) : FormCatalog.User(Model.PermissionManagement.Roles);
        if (record != null && kind == "User") fields = fields.Where(f => f.Kind != "password").ToArray();
        if (record != null) foreach (var f in fields) { f.Value = record[f.Label]; f.IsChecked = bool.TryParse(f.Value, out var v) && v; }
        var footer = new RecordEditorFooterViewModel(kind, record == null, CloseOverlay, state =>
        {
            if (!Model.SaveRecord(kind, fields, record)) return;
            if (kind == "Customer" && state.UseDefault) Model.SetDefaultCustomer(Model.Customers.Last(customer => customer.Name == fields[0].Value.Trim()));
            refresh();
            if (state.AddAnother) EditRecord(kind, refresh); else CloseOverlay();
        });
        object form = kind == "Product"
            ? new ProductEditorFormView(fields, Model.ProductFieldVisible)
            : new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 1);
        var typeSelector = kind == "Product" && Model.ProductFieldVisible("Type")
            ? new ProductTypeSelectorView { DataContext = new SegmentedChoiceViewModel(fields[0]) } : null;
        ShowOverlay(record == null ? (kind == "Product" ? "Add New Product" : $"New {kind}") : $"Edit {kind}",
            form, footer, true, kind == "Product" ? 550 : 520, typeSelector);
    }
    // Shows a compact read-only summary for a user account.
    internal void ShowUserDetails(UiRecord user, Action refresh)
    {
        var note = user.Name == Model.CurrentUsername ? "This is your account" : $"{user["Role"]} access";
        ShowOverlay("User Details", new UserDetailsViewModel(user.Name, user["Role"], note),
            new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]), width: 360);
    }

    // Opens the user editor and prevents changing the signed-in user's own role.
    internal void ShowUserEditor(UiRecord user, Action refresh)
    {
        var username = new FormField("Username", user.Name, required: true);
        var role = new FormField("Role", user["Role"], "choice", ["Admin", "User"], required: true);
        var form = new UserEditorViewModel(new FormFieldViewModel(username), new FormFieldViewModel(role), user.Name == Model.CurrentUsername);
        var save = new RelayCommand(() =>
        {
            if (!Model.SaveRecord("User", [username, role], user)) return;
            refresh();
            CloseOverlay();
        });
        ShowOverlay("Edit User", form, new DialogActions([
            new DialogAction("Cancel", new RelayCommand(CloseOverlay)),
            new DialogAction("✓  Save Changes", save, true)]), side: true, width: 520);
    }

    // Opens the current-password flow for the signed-in user or an administrator reset flow for another user.
    internal void ShowUserPassword(UiRecord user)
    {
        var ownAccount = user.Name == Model.CurrentUsername;
        FormField[] fields = ownAccount
            ? [new("Current Password", kind: "password", required: true), new("New Password", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)]
            : [new("New Password", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)];
        var body = new UserPasswordViewModel(user.Name, new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 1));
        var save = new RelayCommand(() =>
        {
            var changed = ownAccount ? Model.ChangePassword(user.Name, fields) : Model.ResetUserPassword(user, fields);
            if (changed) CloseOverlay();
        });
        ShowOverlay("Change Password", body, new DialogActions([
            new DialogAction("Cancel", new RelayCommand(CloseOverlay)),
            new DialogAction("✓  Change Password", save, true)]), width: 450, headerAccessory: new DialogIcon("lock"));
    }

    // Performs the show payment action for this screen or workflow.
    internal void ShowPayment(UiRecord invoice)
    {
        // Performs the amount parsing action for this screen or workflow.
        static decimal Amount(string value) => decimal.TryParse(value, out var amount) ? amount : 0m;
        // Performs the money formatting action for this screen or workflow.
        static string Money(decimal amount) => CurrencyDisplay.Format(amount, "0.00");

        var total = Amount(invoice["Total"]);
        var paid = Amount(invoice["Paid"]);
        var outstanding = invoice["Outstanding"].Length > 0 ? Amount(invoice["Outstanding"]) : Math.Max(0m, total - paid);
        var payments = Model.PaymentsFor(invoice).ToArray();
        var fields = FormCatalog.Payment();
        fields[0].Value = outstanding.ToString("0.00");
        fields[1].Value = DateTime.Today.ToString("yyyy-MM-dd");
        fields[3].Value = invoice["Tax"].Length > 0 ? invoice["Tax"] : "0.00";

        var fullyPaid = outstanding <= 0.005m;
        var footer = new DialogActions(fullyPaid
            ? [new DialogAction("Close", new RelayCommand(CloseOverlay), true)]
            : [new DialogAction("Close", new RelayCommand(CloseOverlay)),
                new DialogAction("Save Payment", new RelayCommand(() => { if (Model.ApplyPayment(invoice, fields)) ShowPayment(invoice); }), true)]);
        var model = new PaymentDialogModel
        {
            InvoiceLine = $"{invoice.Name} — {invoice["Customer"]}",
            TotalText = Money(total),
            PaidText = Money(paid),
            OutstandingText = Money(outstanding),
            IsFullyPaid = fullyPaid,
            AmountAndDate = new FormFieldsViewModel(fields.Take(2).Select(field => new FormFieldViewModel(field)).ToArray(), 2),
            MethodAndTax = new FormFieldsViewModel(fields.Skip(2).Take(2).Select(field => new FormFieldViewModel(field)).ToArray(), 2),
            Note = new FormFieldViewModel(fields[4]),
            AmountHint = $"Max: {Money(outstanding)}"
        };
        foreach (var payment in payments)
        {
            model.Payments.Add(new PaymentHistoryRowModel(payment.Name, payment["Date"], Money(Amount(payment["Amount"])), Money(Amount(invoice["Tax"])), payment["Method"]));
        }

        ShowOverlay("Record Payment", new PaymentDialogView(model), footer, width: 700);
    }
    // Performs the show custom item action for this screen or workflow.
    private void ShowCustomItem()
    {
        var fields = FormCatalog.CustomItem();
        ShowOverlay("Add Custom Item", new FormFieldsViewModel(fields.Select(field => new FormFieldViewModel(field)).ToArray(), 2), new DialogActions([new DialogAction("Cancel", new RelayCommand(CloseOverlay)), new DialogAction("Add Item", new RelayCommand(() =>
        {
            if (!fields.Select(f => f.Validate()).ToArray().All(v => v) || fields[2].Number <= 0) { fields[2].Error = "Quantity must be greater than zero."; return; }
            if (Model.TryAddInvoiceLine(new InvoiceLineViewModel { Name = fields[0].Value, Quantity = fields[2].Number, Price = fields[3].Number, TaxRate = fields[4].Number, Discount = fields[5].Number })) CloseOverlay();
        }), true)]), width: 640);
    }
}
