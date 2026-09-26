using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the edit record action for this screen or workflow.
    internal void EditRecord(string kind, Action refresh, UiRecord? record = null)
    {
        var fields = kind == "Customer" ? FormCatalog.Customer() : kind == "Product" ? Model.ProductEditorFields(record) : FormCatalog.User();
        if (record != null && kind == "User") fields = fields.Where(f => f.Kind != "password").ToArray();
        if (record != null) foreach (var f in fields) { f.Value = record[f.Label]; f.IsChecked = bool.TryParse(f.Value, out var v) && v; }
        var useDefault = new CheckBox { Content = "Use as default for new invoices", IsVisible = kind == "Customer" };
        var another = new CheckBox { Content = "Add another after saving", IsVisible = record == null };
        var cancel = Ui.Button("Cancel", CloseOverlay); cancel.Classes.Add("dialog-action"); cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var save = Ui.Button($"Save {kind}", () =>
        {
            if (!Model.SaveRecord(kind, fields, record)) return;
            if (kind == "Customer" && useDefault.IsChecked == true) Model.SetDefaultCustomer(Model.Customers.Last(c => c.Name == fields[0].Value.Trim()));
            refresh(); if (another.IsChecked == true) EditRecord(kind, refresh); else CloseOverlay();
        }, true); save.Classes.Add("material"); save.Classes.Add("dialog-action"); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        Control form = Ui.Fields(fields);
        if (kind == "Product")
        {
            form = new ProductEditorFormView(fields, Model.ProductFieldVisible);
        }
        Control footer;
        if (kind == "Product")
        {
            foreach (var button in new[] { cancel, save }) { button.Height = 32; button.MinHeight = 32; button.Padding = new Thickness(12, 5); }
            cancel.Background = Brushes.Transparent; cancel.Foreground = ProductEditorFormView.Purple;
            save.Background = ProductEditorFormView.Purple;
            save.Content = Ui.Columns("Auto,8,Auto", Ui.Icon("save", 17, Brushes.White), new Border(), Ui.LocalText("Save Product", 13, true, Brushes.White));
            footer = Ui.Stack(9.6, another, Ui.Columns("*,12,2*", cancel, new Border(), save));
        }
        else footer = new RecordDialogFooterView(useDefault, another, cancel, save);
        ShowOverlay(record == null ? (kind == "Product" ? "Add New Product" : $"New {kind}") : $"Edit {kind}", form, footer, true, kind == "Product" ? 550 : 520, kind == "Product" && Model.ProductFieldVisible("Type") ? ProductEditorFormView.TypeSelector(fields[0]) : null);
    }

    // Shows a compact read-only summary for a user account.
    internal void ShowUserDetails(UiRecord user, Action refresh)
    {
        var initial = user.Name.Length == 0 ? "?" : user.Name[..1].ToUpperInvariant();
        var initialText = Ui.Text(initial, 14, true, Brush.Parse("#9C27B0"));
        initialText.HorizontalAlignment = HorizontalAlignment.Center;
        initialText.VerticalAlignment = VerticalAlignment.Center;
        initialText.TextAlignment = TextAlignment.Center;
        var avatar = new Border { Width = 30.4, Height = 30.4, CornerRadius = new CornerRadius(19), Background = Ui.Palette("#F0DDF8", "#202B36"), Child = initialText };
        var role = new Border { Background = Ui.Palette("#F3E5F5", "#202B36"), CornerRadius = new CornerRadius(6), Padding = new Thickness(7.2, 3.2), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Text(user["Role"], 11, false, Brush.Parse("#9C27B0")) };
        var note = user.Name == Model.CurrentUsername ? "This is your account" : $"{user["Role"]} access";
        var content = Ui.Stack(11.2, Ui.Columns("38,10,*", avatar, new Border(), Ui.Text(user.Name, 16, true)), role, Ui.Text(note, 12, color: Ui.Muted));
        ShowOverlay("User Details", content, Ui.Button("Close", CloseOverlay, true), width: 360);
    }

    // Opens the user editor and prevents changing the signed-in user's own role.
    internal void ShowUserEditor(UiRecord user, Action refresh)
    {
        var username = new FormField("Username", user.Name, required: true);
        var role = new FormField("Role", user["Role"], "choice", ["Admin", "User"], required: true);
        var roleControl = Ui.Field(role);
        var editingSelf = user.Name == Model.CurrentUsername;
        roleControl.IsEnabled = !editingSelf;
        var form = Ui.Stack(14.4, Ui.Field(username), roleControl);
        form.Margin = new Thickness(0, 8, 0, 0);
        if (editingSelf) form.Children.Add(Ui.LocalText("You can't change your own role.", 12, color: Ui.Muted));
        var cancel = Ui.Button("Cancel", CloseOverlay);
        var save = Ui.Button("✓  Save Changes", () =>
        {
            if (!Model.SaveRecord("User", [username, role], user)) return;
            refresh();
            CloseOverlay();
        }, true);
        ShowOverlay("Edit User", form, Ui.Columns("*,12,2*", cancel, new Border(), save), side: true, width: 520);
    }

    // Opens the current-password flow for the signed-in user or an administrator reset flow for another user.
    internal void ShowUserPassword(UiRecord user)
    {
        var ownAccount = user.Name == Model.CurrentUsername;
        FormField[] fields = ownAccount
            ? [new("Current Password", kind: "password", required: true), new("New Password", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)]
            : [new("New Password", kind: "password", required: true), new("Confirm New Password", kind: "password", required: true)];
        var identity = new Border { Background = Ui.Palette("#E3F2FD", "#202B36"), BorderBrush = Brush.Parse("#64B5F6"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(11.2, 9.6), Child = Ui.Text($"User: {user.Name}", 14, false, Brush.Parse("#1976D2")) };
        var body = Ui.Stack(12.8, identity, Ui.Fields(fields));
        var save = Ui.Button("✓  Change Password", () =>
        {
            var changed = ownAccount ? Model.ChangePassword(user.Name, fields) : Model.ResetUserPassword(user, fields);
            if (changed) CloseOverlay();
        }, true);
        ShowOverlay("Change Password", body, Ui.Wrap(Ui.Button("Cancel", CloseOverlay), save), width: 450, headerAccessory: Ui.Icon("lock", 22, Brushes.White));
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
        Control? paymentForm = null;
        Control footer;
        if (fullyPaid)
        {
            footer = Ui.Button("Close", CloseOverlay, true);
        }
        else
        {
            var amountHint = Ui.Text($"Max: {Money(outstanding)}", 11, color: Ui.Muted);
            paymentForm = Ui.Stack(9.6, Ui.Fields(fields.Take(2), 2), amountHint, Ui.Fields(fields.Skip(2).Take(2), 2), Ui.Field(fields[4]));
            var savePayment = Ui.Button("Save Payment", () => { if (Model.ApplyPayment(invoice, fields)) ShowPayment(invoice); }, true);
            savePayment.Content = Ui.Columns("18,8,Auto", Ui.Icon("check_circle", 16, Brushes.White), new Border(), Ui.LocalText("Record Payment", 13, true, Brushes.White));
            footer = Ui.Wrap(Ui.Button("Close", CloseOverlay), savePayment);
        }

        var outstandingColor = fullyPaid ? "#16A34A" : "#F59E0B";
        var model = new PaymentDialogModel
        {
            InvoiceLine = $"{invoice.Name} — {invoice["Customer"]}",
            TotalText = Money(total),
            PaidText = Money(paid),
            OutstandingText = Money(outstanding),
            OutstandingBrush = Brush.Parse(outstandingColor),
            OutstandingBorder = new SolidColorBrush(Color.Parse(outstandingColor), .35),
            OutstandingBackground = Brush.Parse(fullyPaid ? "#EEF8F0" : "#FFF5E8"),
            IsFullyPaid = fullyPaid,
            PaymentForm = paymentForm
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
        ShowOverlay("Add Custom Item", Ui.Fields(fields, 2), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Add Item", () =>
        {
            if (!fields.Select(f => f.Validate()).ToArray().All(v => v) || fields[2].Number <= 0) { fields[2].Error = "Quantity must be greater than zero."; return; }
            if (Model.TryAddInvoiceLine(new InvoiceLineViewModel { Name = fields[0].Value, Quantity = fields[2].Number, Price = fields[3].Number, TaxRate = fields[4].Number, Discount = fields[5].Number })) CloseOverlay();
        }, true)), width: 640);
    }
}
