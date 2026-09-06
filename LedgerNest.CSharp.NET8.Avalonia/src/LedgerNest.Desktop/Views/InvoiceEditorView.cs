using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private Control InvoiceEditor()
    {
        var saveCustomer = Ui.Button("Save customer", () => Model.SaveRecord("Customer", Model.InvoiceCustomer)); saveCustomer.Classes.Add("text");
        var customerFields = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"), RowDefinitions = new RowDefinitions("Auto,12,Auto") };
        int[] order = [0, 1, 2, 4, 3, 5]; string[] labels = ["Customer Name *", "Business name", "Phone", "GSTIN / VAT", "Email", "Address"];
        for (var i = 0; i < order.Length; i++)
        {
            var f = Ui.Field(Model.InvoiceCustomer[order[i]], labels[i], true); Grid.SetColumn(f, i % 3 * 2); Grid.SetRow(f, i / 3 * 2); customerFields.Children.Add(f);
        }
        var customerHeader = Ui.Columns("Auto,8,*,Auto", Ui.Icon("person", 16), new Border(), Ui.Text("CUSTOMER DETAILS", 12, true), Ui.Wrap(saveCustomer, Ui.Button("Select from existing", SelectCustomer), Ui.Button("⌃", () => customerFields.IsVisible = !customerFields.IsVisible)));
        var customer = Ui.Card(Ui.Stack(6, customerHeader, customerFields), 12);
        var productSearch = new TextBox { Watermark = "Search & add a product or service (Ctrl+F)", MinWidth = 120, Background = Brushes.White };
        var suggestions = new ListBox { IsVisible = false, MaxHeight = 180 };
        productSearch.TextChanged += (_, _) => { suggestions.ItemsSource = Model.Products.Where(p => p.Name.Contains(productSearch.Text ?? "", StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToArray(); suggestions.IsVisible = !string.IsNullOrWhiteSpace(productSearch.Text); };
        suggestions.SelectionChanged += (_, _) =>
        {
            var product = Model.Products.FirstOrDefault(p => p.Name == suggestions.SelectedItem?.ToString()); if (product == null) return;
            Model.Lines.Add(new InvoiceLineViewModel { Name = product.Name, Price = decimal.TryParse(product["Sale Price"], out var price) ? price : 0, TaxRate = decimal.TryParse(product["Tax (%)"], out var tax) ? tax : 0, PriceIncludesTax = bool.TryParse(product["Price includes tax"], out var inclusive) && inclusive });
            productSearch.Text = ""; suggestions.IsVisible = false;
        };
        var lineHost = new ContentControl(); var totals = new ContentControl(); var count = Ui.Text("0 items", 11, true, Ui.Muted);
        var create = Ui.Button("Create Invoice (Ctrl+S)", () => { if (Model.SaveInvoice()) ShowInvoiceSuccess(); }, true);
        void UpdateTotals()
        {
            var t = Model.Totals; var rows = Ui.Stack(8, TotalRow("Subtotal:", t.Subtotal), TotalRow("Tax:", t.Tax));
            if (t.ItemDiscount != 0) rows.Children.Add(TotalRow("Item Discount:", t.ItemDiscount));
            if (t.AdditionalCosts != 0) rows.Children.Add(TotalRow("Additional Costs:", t.AdditionalCosts));
            if (t.InvoiceDiscount != 0) rows.Children.Add(TotalRow("Invoice Discount:", t.InvoiceDiscount));
            rows.Children.Add(new Border { Height = 6 }); rows.Children.Add(TotalRow("Total:", t.Total, true)); totals.Content = rows;
        }
        void RefreshLines()
        {
            count.Text = $"{Model.Lines.Count} items";
            if (Model.Lines.Count == 0) lineHost.Content = Ui.Empty("No items added yet", "Search below or press Ctrl+F", "cart");
            else
            {
                var rows = Ui.Stack(0, new Border { Padding = new Thickness(8), Child = Ui.Columns("*,70,85,60,80,90,40", Ui.Text("ITEM", 11, true), Ui.Text("QTY", 11, true), Ui.Text("PRICE", 11, true), Ui.Text("TAX %", 11, true), Ui.Text("DISCOUNT", 11, true), Ui.Text("TOTAL", 11, true), Ui.Text("")) });
                foreach (var line in Model.Lines)
                {
                    NumericUpDown Number(string property, decimal min = 0)
                    { var n = new NumericUpDown { Minimum = min, Maximum = 1000000000, Increment = 1, FormatString = "0.##", ShowButtonSpinner = false, Margin = new Thickness(2), MinWidth = 0 }; n.Bind(NumericUpDown.ValueProperty, new Binding(property) { Source = line, Mode = BindingMode.TwoWay }); return n; }
                    var total = Ui.Text(line.Total.ToString("0.00"), 12, true); total.Bind(TextBlock.TextProperty, new Binding(nameof(line.Total)) { Source = line, StringFormat = "{0:0.00}" });
                    rows.Children.Add(new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(8), Child = Ui.Columns("*,70,85,60,80,90,40", Ui.Text(line.Name, 13, true), Number(nameof(line.Quantity), .001m), Number(nameof(line.Price)), Number(nameof(line.TaxRate)), Number(nameof(line.Discount)), total, Ui.Button("×", () => Model.Lines.Remove(line))) });
                }
                lineHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new Border { MinWidth = 650, Child = rows } };
            }
            UpdateTotals(); create.IsEnabled = Model.Lines.Count > 0;
        }
        var quickAdd = Ui.Card(Ui.Stack(4, suggestions, Ui.Columns("*,12,Auto", productSearch, new Border(), Ui.Button("＋ Custom Item", ShowCustomItem))), 8); quickAdd.Background = Brush.Parse("#E0F2F1"); quickAdd.BorderBrush = Brush.Parse("#80CBC4");
        var items = Ui.Card(Ui.Rows("Auto,*,Auto", Ui.Wrap(Ui.Text("ITEMS", 12, true), count), lineHost, quickAdd), 12);
        var detailFields = Ui.Stack(8, Ui.Fields(Model.InvoiceDetails.Take(4)), Ui.Field(Model.HideInvoiceNumber));
        var detailHeading = Ui.Columns("*,Auto", Ui.Text("INVOICE DETAILS", 12, true), Ui.Button("⌃", () => detailFields.IsVisible = !detailFields.IsVisible));
        var details = Ui.Card(Ui.Stack(0, detailHeading, detailFields), 12);
        var additional = Ui.Stack(8);
        void AddCost(FormField[] fields) => additional.Children.Add(Ui.Columns("*,Auto", Ui.Fields(fields, 2), Ui.Button("×", () => { Model.AdditionalCosts.Remove(fields); additional.Children.Clear(); foreach (var cost in Model.AdditionalCosts) AddCost(cost); })));
        foreach (var cost in Model.AdditionalCosts) AddCost(cost);
        var costs = new Expander { Header = "⊞  Additional Costs", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Stack(8, additional, Ui.Button("＋ Add Cost", () => { FormField[] fields = [new("Description"), new("Amount", "0", "number")]; Model.AdditionalCosts.Add(fields); AddCost(fields); })) };
        var discount = Ui.Card(Ui.Fields(Model.InvoiceOptions.Take(2), 2), 8); discount.Background = Brush.Parse("#FFF4F4"); discount.BorderBrush = Brush.Parse("#FFD6A5");
        var tax = Ui.Fields(Model.InvoiceOptions.Skip(3));
        var options = Ui.Stack(12, costs, discount, Ui.Text("NOTES", 11, true, Ui.Muted), Ui.Field(Model.InvoiceOptions[2]), Ui.Text("TAX SETTINGS", 11, true, Ui.Muted), tax, Ui.Field(Model.InterState));
        var optionsCard = Ui.Card(Ui.Rows("*,Auto", Ui.Scroll(options, 12), new Border { Padding = new Thickness(16), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Child = totals }), 0);
        var left = Ui.Rows("Auto,8,*", customer, new Border(), items); var right = Ui.Rows("Auto,8,*", details, new Border(), optionsCard);
        var viewport = new InvoiceWorkspace(left, right, items, optionsCard);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var (label, icon) in new[] { ("View", "visibility"), ("Preview", "picture_as_pdf"), ("Download", "download"), ("Print", "print") })
        {
            var button = Ui.Button(label); button.Content = Ui.Icon(icon, 24); button.Width = 48; button.Height = 48;
            var action = Ui.Stack(4, button, Ui.Text(label, 12, true, Ui.Muted)); foreach (var c in action.Children) c.HorizontalAlignment = HorizontalAlignment.Center; actions.Children.Add(action);
        }
        var footer = new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(64, 10), Child = Ui.Columns("*,Auto,*", actions, create, new Border()) };
        var body = Ui.Rows("Auto,*,Auto", Ui.AppBar("Create New Invoice", Ui.Text(DateTime.Today.ToString("dd/MM/yyyy"), 20, color: Brushes.White), Ui.Text($"Invoice Number : #[{Model.Invoices.Count + 1:00000000}]", 16, color: Brushes.White)), viewport, footer);
        Model.InvoiceChanged += UpdateTotals;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChanged = (_, _) => RefreshLines(); Model.Lines.CollectionChanged += collectionChanged;
        body.DetachedFromVisualTree += (_, _) => { Model.InvoiceChanged -= UpdateTotals; Model.Lines.CollectionChanged -= collectionChanged; };
        body.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.F && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)) { productSearch.Focus(); e.Handled = true; } };
        body.SizeChanged += (_, e) => footer.Padding = new Thickness(e.NewSize.Width < 700 ? 8 : 64, 10);
        RefreshLines(); return body;
    }
    private static Control TotalRow(string label, decimal value, bool bold = false) => Ui.Columns("*,Auto", Ui.Text(label, bold ? 18 : 13, bold), Ui.Text($"Rs.{value:0.00}", bold ? 22 : 14, bold, bold ? Brush.Parse("#4CAF50") : null));
    private void SelectCustomer()
    {
        var list = new ListBox { ItemsSource = Model.Customers.Select(c => c.Name).ToArray(), MinHeight = 180 };
        var search = new TextBox { Watermark = "Search customer" };
        search.TextChanged += (_, _) => list.ItemsSource = Model.Customers.Where(c => c.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name).ToArray();
        ShowOverlay("Select Customer", Ui.Stack(12, search, list), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Select", () => { var c = Model.Customers.FirstOrDefault(c => c.Name == list.SelectedItem?.ToString()); if (c == null) return; foreach (var f in Model.InvoiceCustomer) f.Value = c[f.Label]; CloseOverlay(); }, true)));
    }
    private void ShowInvoiceSuccess()
    {
        page.Content = Ui.Scroll(Ui.Stack(24, Ui.Empty($"{Model.InvoiceDetails[0].Value} created successfully", Model.Invoices.Last().Name, "✓"), Ui.Card(Ui.Stack(16, Ui.Text("Payment Summary", 18, true), TotalRow("Total", Model.Totals.Total), Ui.Button("Apply Payment", () => ShowPayment(Model.Invoices.Last())))), Ui.Wrap(Ui.Button("View", () => ShowPayment(Model.Invoices.Last())), Ui.Button("Preview"), Ui.Button("Download"), Ui.Button("Print"), Ui.Button("Create New Invoice", () => { Model.Lines.Clear(); foreach (var f in Model.InvoiceCustomer) f.Value = ""; page.Content = InvoiceEditor(); }, true))));
    }
}
