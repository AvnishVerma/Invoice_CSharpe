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
        var fields = kind == "Customer" ? FormCatalog.Customer() : kind == "Product" ? FormCatalog.Product() : FormCatalog.User();
        if (record != null && kind == "User") fields = fields.Where(f => f.Kind != "password").ToArray();
        if (record != null) foreach (var f in fields) { f.Value = record[f.Label]; f.IsChecked = bool.TryParse(f.Value, out var v) && v; }
        var useDefault = new CheckBox { Content = "Use as default for new invoices", IsVisible = kind == "Customer" };
        var another = new CheckBox { Content = "Add another after saving", IsVisible = record == null };
        var cancel = Ui.Button("Cancel", CloseOverlay); cancel.CornerRadius = new CornerRadius(24); cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var save = Ui.Button($"Save {kind}", () =>
        {
            if (!Model.SaveRecord(kind, fields, record)) return;
            if (kind == "Customer" && useDefault.IsChecked == true) Model.SetDefaultCustomer(Model.Customers.Last(c => c.Name == fields[0].Value.Trim()));
            refresh(); if (another.IsChecked == true) EditRecord(kind, refresh); else CloseOverlay();
        }, true); save.Classes.Add("material"); save.CornerRadius = new CornerRadius(24); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        Control form = Ui.Fields(fields);
        if (kind == "Product")
        {
            form = Ui.Stack(20, Ui.Text("GENERAL", 11, true, Ui.Muted), Ui.Fields(fields.Skip(1).Take(4)), Ui.Text("PRICING", 11, true, Ui.Muted), Ui.Fields(fields.Skip(5).Take(2), 2), Ui.Field(fields[7]), Ui.Fields(fields.Skip(8).Take(2), 2), Ui.Text("STOCK & UNIT", 11, true, Ui.Muted), Ui.Fields(fields.Skip(10).Take(4), 2), new Expander { Header = "Advanced Information", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Fields(fields.Skip(14), 2) });
        }
        ShowOverlay(record == null ? (kind == "Product" ? "Add New Product" : $"New {kind}") : $"Edit {kind}", form, Ui.Stack(16, useDefault, another, Ui.Columns("*,12,2*", cancel, new Border(), save)), true, kind == "Product" ? 550 : 520, kind == "Product" ? Ui.Segments(fields[0]) : null);
    }

    // Performs the show payment action for this screen or workflow.
    internal void ShowPayment(UiRecord invoice)
    {
        // Performs the amount parsing action for this screen or workflow.
        static decimal Amount(string value) => decimal.TryParse(value, out var amount) ? amount : 0m;
        // Performs the money formatting action for this screen or workflow.
        static string Money(decimal amount) => $"Rs. {amount:0.00}";

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
            paymentForm = Ui.Stack(12, Ui.Fields(fields.Take(2), 2), amountHint, Ui.Fields(fields.Skip(2).Take(2), 2), Ui.Field(fields[4]));
            var savePayment = Ui.Button("Save Payment", () => { if (Model.ApplyPayment(invoice, fields)) ShowPayment(invoice); }, true);
            savePayment.Content = Ui.Columns("18,8,Auto", Ui.Icon("check_circle", 16, Brushes.White), new Border(), Ui.Text("Record Payment", 13, true, Brushes.White));
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
            Model.Lines.Add(new InvoiceLineViewModel { Name = fields[0].Value, Quantity = fields[2].Number, Price = fields[3].Number, TaxRate = fields[4].Number, Discount = fields[5].Number }); CloseOverlay();
        }, true)), width: 640);
    }
}
