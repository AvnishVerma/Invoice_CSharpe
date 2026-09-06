using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void EditRecord(string kind, Action refresh, UiRecord? record = null)
    {
        var fields = kind == "Customer" ? FormCatalog.Customer() : kind == "Product" ? FormCatalog.Product() : FormCatalog.User();
        if (record != null && kind == "User") fields = fields.Where(f => f.Kind != "password").ToArray();
        if (record != null) foreach (var f in fields) { f.Value = record[f.Label]; f.IsChecked = bool.TryParse(f.Value, out var v) && v; }
        var another = new CheckBox { Content = "Add another after saving", IsVisible = record == null };
        var cancel = Ui.Button("Cancel", CloseOverlay); cancel.CornerRadius = new CornerRadius(24); cancel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var save = Ui.Button($"Save {kind}", () =>
        {
            if (!Model.SaveRecord(kind, fields, record)) return;
            refresh(); if (another.IsChecked == true) EditRecord(kind, refresh); else CloseOverlay();
        }, true); save.Classes.Add("material"); save.CornerRadius = new CornerRadius(24); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        Control form = Ui.Fields(fields);
        if (kind == "Product")
        {
            form = Ui.Stack(20, Ui.Text("GENERAL", 11, true, Ui.Muted), Ui.Fields(fields.Skip(1).Take(4)), Ui.Text("PRICING", 11, true, Ui.Muted), Ui.Fields(fields.Skip(5).Take(2), 2), Ui.Field(fields[7]), Ui.Fields(fields.Skip(8).Take(2), 2), Ui.Text("STOCK & UNIT", 11, true, Ui.Muted), Ui.Fields(fields.Skip(10).Take(4), 2), new Expander { Header = "Advanced Information", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Fields(fields.Skip(14), 2) });
        }
        ShowOverlay(record == null ? (kind == "Product" ? "Add New Product" : $"New {kind}") : $"Edit {kind}", form, Ui.Stack(16, another, Ui.Columns("*,12,2*", cancel, new Border(), save)), true, kind == "Product" ? 550 : 520, kind == "Product" ? Ui.Segments(fields[0]) : null);
    }

    internal void ShowPayment(UiRecord invoice)
    {
        var fields = FormCatalog.Payment();
        ShowOverlay("Apply Payment", Ui.Stack(18, Ui.Text($"Invoice: {invoice.Name} · {invoice["Customer"]}"), Ui.Stats(("Invoice Total", invoice["Total"], "", "#002E78"), ("Amount Paid", "0.00", "", "#2E7D32"), ("Outstanding", invoice["Total"], "", "#C62828")), Ui.Text("Payment History", 16, true), Ui.Empty("No payments yet", "", ""), Ui.Text("New Payment", 16, true), Ui.Fields(fields, 2)), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Save Payment")), width: 760);
    }
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
