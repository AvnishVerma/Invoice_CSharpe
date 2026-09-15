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
        // Performs the amount action for this screen or workflow.
        static decimal Amount(string value) => decimal.TryParse(value, out var amount) ? amount : 0m;
        // Performs the money action for this screen or workflow.
        static string Money(decimal amount) => $"Rs. {amount:0.00}";

        var total = Amount(invoice["Total"]);
        var paid = Amount(invoice["Paid"]);
        var outstanding = invoice["Outstanding"].Length > 0 ? Amount(invoice["Outstanding"]) : Math.Max(0m, total - paid);
        var payments = Model.PaymentsFor(invoice).ToArray();
        var fields = FormCatalog.Payment();
        fields[0].Value = outstanding.ToString("0.00");
        fields[1].Value = DateTime.Today.ToString("yyyy-MM-dd");
        fields[3].Value = invoice["Tax"].Length > 0 ? invoice["Tax"] : "0.00";

        Control SummaryCard(string label, decimal value, string color, string background) => new Border
        {
            Padding = new Thickness(16, 12),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.Parse(color), .35),
            BorderThickness = new Thickness(1),
            Background = Brush.Parse(background),
            Child = Ui.Stack(8, Ui.Text(label, 12, color: Ui.Muted), Ui.Text(Money(value), 16, true, Brush.Parse(color)))
        };

        Control History()
        {
            if (payments.Length == 0)
                return new Border { Background = Ui.Canvas, BorderBrush = Ui.Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Padding = new Thickness(18), Child = Ui.Text("No payments recorded yet", 13, color: Ui.Muted) };
            var rows = Ui.Stack(0);
            rows.Children.Add(new Border { Background = Ui.Canvas, Padding = new Thickness(14, 10), Child = Ui.Columns("1.2*,.8*,.9*,1*,1*", Ui.Text("Receipt #", 12), Ui.Text("Date", 12), Ui.Text("Amount", 12), Ui.Text("Tax Covered", 12), Ui.Text("Method", 12)) });
            foreach (var payment in payments)
            {
                var row = new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(14, 11), Child = Ui.Columns("1.2*,.8*,.9*,1*,1*", Ui.Text(payment.Name, 12, color: Ui.Primary), Ui.Text(payment["Date"], 12), Ui.Text(Money(Amount(payment["Amount"])), 12, true, Brush.Parse("#16A34A")), Ui.Text(Money(Amount(invoice["Tax"])), 12, color: Ui.Muted), Ui.Text(payment["Method"], 12, color: Ui.Muted)) };
                rows.Children.Add(row);
            }
            return new Border { Background = Ui.CardSurface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Child = rows };
        }

        var summary = Ui.Columns("*,12,*,12,*",
            SummaryCard("Invoice Total", total, "#0A84FF", "#EEF5FF"), new Border(),
            SummaryCard("Amount Paid", paid, "#16A34A", "#EEF8F0"), new Border(),
            SummaryCard("Outstanding", outstanding, outstanding <= 0.005m ? "#16A34A" : "#F59E0B", outstanding <= 0.005m ? "#EEF8F0" : "#FFF5E8"));

        Control content;
        Control footer;
        if (outstanding <= 0.005m)
        {
            var paidBanner = new Border { BorderBrush = new SolidColorBrush(Color.Parse("#16A34A"), .35), BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.Parse("#16A34A"), .08), CornerRadius = new CornerRadius(7), Padding = new Thickness(16, 14), Child = Ui.Columns("*,Auto,*", new Border(), Ui.Columns("22,8,Auto", Ui.Icon("check_circle", 18, Brush.Parse("#16A34A")), new Border(), Ui.Text("Invoice fully paid", 14, color: Brush.Parse("#16A34A"))), new Border()) };
            content = Ui.Stack(20, Ui.Text($"{invoice.Name} — {invoice["Customer"]}", 13, color: Ui.Muted), summary, Ui.Text("Payment History", 14, true, Ui.Muted), History(), paidBanner);
            footer = Ui.Button("Close", CloseOverlay, true);
        }
        else
        {
            var amountHint = Ui.Text($"Max: {Money(outstanding)}", 11, color: Ui.Muted);
            var form = Ui.Stack(12, Ui.Text("New Payment", 14, true), Ui.Fields(fields.Take(2), 2), amountHint, Ui.Fields(fields.Skip(2).Take(2), 2), Ui.Field(fields[4]));
            var savePayment = Ui.Button("Save Payment", () => { if (Model.ApplyPayment(invoice, fields)) ShowPayment(invoice); }, true);
            savePayment.Content = Ui.Columns("18,8,Auto", Ui.Icon("check_circle", 16, Brushes.White), new Border(), Ui.Text("Record Payment", 13, true, Brushes.White));
            content = Ui.Stack(20, Ui.Text($"{invoice.Name} — {invoice["Customer"]}", 13, color: Ui.Muted), summary, Ui.Text("Payment History", 14, true, Ui.Muted), History(), new Border { Height = 1, Background = Ui.Outline }, form);
            footer = Ui.Wrap(Ui.Button("Close", CloseOverlay), savePayment);
        }

        ShowOverlay("Record Payment", content, footer, width: 700);
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
