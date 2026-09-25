using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Templates;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the invoice editor action for this screen or workflow.
    private Control InvoiceEditor()
    {
        Control Field(FormField field) => Ui.Field(field, compact: true);
        Control Fields(IEnumerable<FormField> fields, int columns = 1) => Ui.Fields(fields, columns, compact: true);
        Button EditorButton(string label, Action? action = null, bool primary = false)
        {
            var button = Ui.Button(label, action, primary);
            button.MinHeight = 32;
            button.Padding = new Thickness(10, 5);
            button.VerticalAlignment = VerticalAlignment.Center;
            return button;
        }
        invoiceCompletionVisible = false;
        var editorModel = Model;
        var saveCustomer = EditorButton("Save customer", () => editorModel.SaveRecord("Customer", editorModel.InvoiceCustomer)); saveCustomer.Classes.Add("text");
        var customerFields = Fields(new[] { editorModel.InvoiceCustomer[0], editorModel.InvoiceCustomer[2] }, 2);
        var extraCustomerFields = editorModel.InvoiceCustomer.Where((_, index) => index is not (0 or 2) && (index != 4 || editorModel.InvoiceSetting("Show GST fields").IsChecked));
        var customerMore = new Expander { Header = "Business, address & contact details", Content = Ui.Stack(10, Fields(extraCustomerFields, 2), saveCustomer), HorizontalAlignment = HorizontalAlignment.Stretch };
        var customerHeader = Ui.Columns("*,Auto", Ui.LocalText("Bill to", 16, true), EditorButton("Select customer", SelectCustomer));
        var customer = Ui.Card(Ui.Stack(8, customerHeader, customerFields, customerMore), 13);
        var productSearch = new TextBox { PlaceholderText = "Search & add a product or service (Ctrl+F)", MinWidth = 120, MinHeight = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 5), Background = Ui.Canvas };
        var suggestions = new ListBox
        {
            IsVisible = false,
            MaxHeight = 260,
            Background = Ui.CardSurface,
            BorderBrush = Ui.Outline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ItemTemplate = new FuncDataTemplate<UiRecord>((product, _) => ProductSearchSuggestion(product, editorModel.InvoiceSetting("Show GST fields").IsChecked), true)
        };
        bool Matches(UiRecord p, string query) => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || p["SKU Code"].Contains(query, StringComparison.OrdinalIgnoreCase)
            || p["Alias Name (for invoice PDF)"].Contains(query, StringComparison.OrdinalIgnoreCase)
            || p["HSN/SAC"].Contains(query, StringComparison.OrdinalIgnoreCase);
        productSearch.TextChanged += (_, _) =>
        {
            var query = productSearch.Text?.Trim() ?? "";
            suggestions.ItemsSource = editorModel.Products.Where(p => Matches(p, query)).ToArray();
            suggestions.IsVisible = query.Length > 0;
        };
        var selectingProduct = false;
        void SelectProduct(UiRecord product)
        {
            if (selectingProduct || overlay.IsVisible) return;
            selectingProduct = true;
            try
            {
                suggestions.SelectedItem = null;
                productSearch.Text = "";
                suggestions.IsVisible = false;
                ShowProductItem(product, productSearch);
            }
            finally { selectingProduct = false; }
        }
        suggestions.SelectionChanged += (_, _) =>
        {
            if (suggestions.SelectedItem is UiRecord product) SelectProduct(product);
        };
        productSearch.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape) { suggestions.IsVisible = false; e.Handled = true; return; }
            if (e.Key != Avalonia.Input.Key.Enter) return;
            e.Handled = true;
            var query = productSearch.Text?.Trim() ?? "";
            if (query.Length == 0) return;
            var exact = editorModel.Products.Where(p => p["SKU Code"].Equals(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            var matches = exact.Length > 0 ? exact : editorModel.Products.Where(p => Matches(p, query)).ToArray();
            if (matches.Length == 1) SelectProduct(matches[0]);
        };
        var lineHost = new ContentControl(); var totals = new ContentControl(); var count = Ui.LocalText("0 items", 11, true, Ui.Muted);
        totals.Name = "InvoiceFooterTotals";
        var create = EditorButton($"{(editorModel.IsEditingDocument ? "Save" : "Create")} {editorModel.InvoiceDetails[0].Value} (Ctrl+S)", () => { if (!invoiceCompletionVisible && editorModel.SaveInvoice()) ShowInvoiceSuccess(); }, true);
        create.Background = Ui.HeaderBand; create.MinHeight = 32;
        System.ComponentModel.PropertyChangedEventHandler typeChanged = (_, _) => create.Content = $"{(editorModel.IsEditingDocument ? "Save" : "Create")} {editorModel.InvoiceDetails[0].Value} (Ctrl+S)";
        editorModel.InvoiceDetails[0].PropertyChanged += typeChanged;
        create.DetachedFromVisualTree += (_, _) => editorModel.InvoiceDetails[0].PropertyChanged -= typeChanged;
        void UpdateTotals()
        {
            var t = editorModel.Totals;
            var rows = new WrapPanel { Orientation = Orientation.Horizontal };
            void AddTotal(string label, decimal amount, bool primary = false)
            {
                rows.Children.Add(new Border { Margin = new Thickness(0, 0, 16, 3), Child = Ui.Stack(2,
                    Ui.LocalText(label, 11, color: Ui.Muted), Ui.Text($"Rs. {amount:0.00}", primary ? 19 : 14, true)) });
            }
            AddTotal("Subtotal", t.Subtotal); AddTotal("Tax", t.Tax);
            if (t.ItemDiscount != 0) AddTotal("Item discount", t.ItemDiscount);
            if (t.AdditionalCosts != 0) AddTotal("Charges & adjustments", t.AdditionalCosts);
            if (t.InvoiceDiscount != 0) AddTotal("Invoice discount", t.InvoiceDiscount);
            AddTotal("Total", t.Total, true);
            totals.Content = rows;
        }
        void RefreshLines()
        {
            count.Text = $"{editorModel.Lines.Count} items";
            if (editorModel.Lines.Count == 0) lineHost.Content = Ui.Empty("No items added yet", "Search above to add a product, or add a custom item.", "cart");
            else
            {
                var rows = Ui.Stack(0, new Border { Padding = new Thickness(6), Child = Ui.Columns("*,70,85,60,80,90,40", Ui.LocalText("ITEM", 11, true), Ui.LocalText("QTY", 11, true), Ui.LocalText("PRICE", 11, true), Ui.LocalText("TAX %", 11, true), Ui.LocalText("DISCOUNT", 11, true), Ui.LocalText("TOTAL", 11, true), Ui.LocalText("")) });
                foreach (var line in editorModel.Lines)
                {
                    NumericUpDown Number(string property, decimal min = 0)
                    { var n = new NumericUpDown { Minimum = min, Maximum = 1000000000, Increment = 1, FormatString = "0.##", ShowButtonSpinner = false, Margin = new Thickness(2), MinWidth = 0, MinHeight = 29, Padding = new Thickness(6, 3) }; n.Bind(NumericUpDown.ValueProperty, new Binding(property) { Source = line, Mode = BindingMode.TwoWay }); return n; }
                    var total = Ui.Text(line.Total.ToString("0.00"), 12, true); total.Bind(TextBlock.TextProperty, new Binding(nameof(line.Total)) { Source = line, StringFormat = "{0:0.00}" });
                    var name = Ui.Stack(2, Ui.Text(line.Name, 13, true), Ui.Text(line.Unit == "None" ? "" : line.Unit, 11, color: Ui.Muted));
                    if (editorModel.InvoiceSetting("Show Product / Service Tag").IsChecked) name.Children.Add(Ui.Text(line.ProductType, 11, color: Ui.Muted));
                    rows.Children.Add(new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(6), Child = Ui.Columns("*,70,85,60,80,90,40", name, Number(nameof(line.Quantity), .001m), Number(nameof(line.Price)), Number(nameof(line.TaxRate)), Number(nameof(line.Discount)), total, EditorButton("×", () => editorModel.Lines.Remove(line))) });
                }
                lineHost.Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = new Border { MinWidth = 650, Child = rows } };
            }
            UpdateTotals(); create.IsEnabled = editorModel.Lines.Count > 0;
        }
        var quickAdd = Ui.Card(Ui.Stack(8, Ui.Columns("*,8,Auto", productSearch, new Border(), EditorButton("＋ Custom Item", ShowCustomItem)), suggestions), 8); quickAdd.Background = Ui.Palette("#F1F5FB", "#1E2C40"); quickAdd.BorderBrush = Ui.Outline;
        var items = Ui.Card(Ui.Rows("Auto,Auto,*", Ui.Columns("*,Auto", Ui.LocalText("Items", 16, true), count), new Border { Padding = new Thickness(0, 10, 0, 6), Child = quickAdd }, lineHost), 10);
        var detailFields = Ui.Stack(8, Fields(editorModel.InvoiceDetails.Take(3), 2), new Expander { Header = "Document title & numbering", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Stack(10, Field(editorModel.InvoiceDetails[3]), Field(editorModel.HideInvoiceNumber)) });
        var detailHeading = Ui.LocalText("Document details", 16, true);
        var details = Ui.Card(Ui.Stack(13, detailHeading, detailFields), 10);
        var additional = Ui.Stack(8);
        void AddCost(FormField[] fields) => additional.Children.Add(Ui.Columns("*,Auto", Fields(fields, 2), EditorButton("×", () => { editorModel.AdditionalCosts.Remove(fields); additional.Children.Clear(); foreach (var cost in editorModel.AdditionalCosts) AddCost(cost); })));
        foreach (var cost in editorModel.AdditionalCosts) AddCost(cost);
        var costs = new Expander { Header = "⊞  Charges and Adjustments", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Stack(8, additional, EditorButton("＋ Add Cost", () => { FormField[] fields = [new("Description"), new("Amount", "0", "number")]; editorModel.AdditionalCosts.Add(fields); AddCost(fields); })) };
        var discount = Ui.Card(Fields(editorModel.InvoiceOptions.Take(2), 2), 8); discount.Background = Ui.Palette("#FFF4F4", "#392A30"); discount.BorderBrush = Brush.Parse("#FFD6A5");
        var tax = Fields(editorModel.InvoiceOptions.Skip(3));
        var options = Ui.Stack(10, new Expander { Header = "Discount & additional charges", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Stack(10, discount, costs) }, new Expander { Header = "Notes", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Field(editorModel.InvoiceOptions[2]) }, new Expander { Header = "Tax settings", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Ui.Stack(10, tax, Field(editorModel.InterState)) });
        if (editorModel.InvoiceCustomFields.Count > 0)
        {
            options.Children.Insert(0, new Expander { Header = "Custom fields", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Fields(editorModel.InvoiceCustomFields.Select(field => field.Field)) });
        }
        var optionsCard = Ui.Card(Ui.Scroll(options, 10), 0);
        var left = Ui.Rows("Auto,8,*", customer, new Border(), items); var right = Ui.Rows("Auto,8,*", details, new Border(), optionsCard);
        var viewport = new InvoiceWorkspace(left, right, items, optionsCard);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var (label, icon) in new[] { ("View", "visibility"), ("Preview", "picture_as_pdf"), ("Download", "download"), ("Print", "print") })
        {
            var button = label switch
            {
                "View" => EditorButton(label, () => { if (Model.LastSavedDocument != null) ShowDocumentPreview(Model.LastSavedDocument); }),
                "Preview" => EditorButton(label, async () => { if (Model.LastSavedDocument != null) await ShowPdfPreviewAsync(Model.LastSavedDocument); }),
                "Download" => EditorButton(label, async () => { if (Model.LastSavedDocument != null) await DownloadDocumentPdf(Model.LastSavedDocument); }),
                "Print" => EditorButton(label, async () => { if (Model.LastSavedDocument != null) await PrintDocumentAsync(Model.LastSavedDocument); }),
                _ => EditorButton(label)
            };
            button.Content = Ui.Columns("13,5,Auto", Ui.Icon(icon, 13), new Border(), Ui.LocalText(label, 12)); button.MinHeight = 28; button.Padding = new Thickness(8, 4);
            button.IsEnabled = editorModel.LastSavedDocument != null; ToolTip.SetTip(button, "Open the last saved document. Save this invoice first to include your changes."); actions.Children.Add(button);
        }
        var shellModel = new InvoiceEditorShellModel();
        void UpdateHeader()
        {
            shellModel.Title = $"{(editorModel.IsEditingDocument ? "Edit" : "Create New")} {editorModel.InvoiceDetails[0].Value}";
            shellModel.DateText = DateTime.Today.ToString("dd/MM/yyyy");
            shellModel.NumberText = $"#{editorModel.EditorDocumentNumber}";
        }
        System.ComponentModel.PropertyChangedEventHandler headerChanged = (_, _) => UpdateHeader();
        editorModel.InvoiceDetails[0].PropertyChanged += headerChanged;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler documentsChanged = (_, _) => UpdateHeader();
        editorModel.Invoices.CollectionChanged += documentsChanged;
        UpdateHeader();

        var body = new InvoiceEditorShellView(shellModel, viewport, actions, create, totals);
        body.DetachedFromVisualTree += (_, _) => { editorModel.InvoiceDetails[0].PropertyChanged -= headerChanged; editorModel.Invoices.CollectionChanged -= documentsChanged; };
        editorModel.InvoiceChanged += UpdateTotals;
        System.Collections.Specialized.NotifyCollectionChangedEventHandler collectionChanged = (_, _) => RefreshLines(); editorModel.Lines.CollectionChanged += collectionChanged;
        body.DetachedFromVisualTree += (_, _) => { editorModel.InvoiceChanged -= UpdateTotals; editorModel.Lines.CollectionChanged -= collectionChanged; };
        RefreshLines(); return body;
    }

    // Builds a detailed product-search row with the commercial and stock information needed before selection.
    private Control ProductSearchSuggestion(UiRecord product, bool showGst = true)
    {
        var hasStock = !Model.ProductFieldVisible("Stock") || !decimal.TryParse(product["Stock"], out var stock) || stock > 0 ||
            bool.TryParse(product["Unlimited stock"], out var unlimited) && unlimited;
        var name = Ui.Text(product.Name, 13, false, hasStock ? Ui.TextColor : Brush.Parse("#D32F2F"));
        name.TextWrapping = TextWrapping.NoWrap;
        var price = decimal.TryParse(product["Sale Price"], out var amount) ? $"Rs.{amount:0.00}" : "Rs.0.00";
        var stockText = bool.TryParse(product["Unlimited stock"], out unlimited) && unlimited ? "∞" : product["Stock"].Length == 0 ? "0" : product["Stock"];
        var hsn = product["HSN/SAC"].Length == 0 ? "—" : product["HSN/SAC"];
        var metadata = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        metadata.Children.Add(Ui.Text(price, 10.5, color: Ui.Muted));
        if (Model.ProductFieldVisible("Stock"))
        {
            metadata.Children.Add(Ui.LocalText("•", 10.5, color: Ui.Muted));
            metadata.Children.Add(Ui.Text($"Stock: {stockText}", 10.5, color: Ui.Muted));
        }
        if (showGst && Model.ProductFieldVisible("HSN/SAC"))
        {
            metadata.Children.Add(Ui.LocalText("•", 10.5, color: Ui.Muted));
            metadata.Children.Add(Ui.Text($"HSN {hsn}", 10.5, color: Ui.Muted));
        }
        if (Model.ProductFieldVisible("Storage Location") && !string.IsNullOrWhiteSpace(product["Storage Location"]))
        {
            metadata.Children.Add(Ui.LocalText("•", 10.5, color: Ui.Muted));
            metadata.Children.Add(Ui.Icon("location_on", 12, Brush.Parse("#E91E63")));
            metadata.Children.Add(Ui.Text(product["Storage Location"], 10.5, true, Brush.Parse("#526780")));
        }
        return new Border
        {
            Padding = new Thickness(10, 8),
            MinHeight = 52,
            Child = Ui.Stack(3, name, metadata)
        };
    }
    // Performs the show product item action for this screen or workflow.
    private void ShowProductItem(UiRecord product, TextBox search)
    {
        var dialog = new ProductItemDialogViewModel(product, _ => { }, () =>
        {
            CloseOverlay();
            search.Focus();
        }, Model.TryAddInvoiceLine, Model.InvoiceSetting("Allow Fractional Quantity").IsChecked, Model.ProductFieldVisible);
        overlay.Children.Clear();
        overlay.Margin = new Thickness(0);
        overlay.IsVisible = true;
        overlay.Children.Add(new ProductItemDialogView { DataContext = dialog });
    }
    // Performs the total row action for this screen or workflow.
    private static Control TotalRow(string label, decimal value, bool bold = false) => Ui.Columns("*,Auto", Ui.LocalText(label, bold ? 18 : 13, bold), Ui.Text($"Rs.{value:0.00}", bold ? 22 : 14, bold, bold ? Brush.Parse("#4CAF50") : null));
    // Performs the select customer action for this screen or workflow.
    private void SelectCustomer()
    {
        var list = new ListBox { ItemsSource = Model.Customers.Select(c => c.Name).ToArray(), MinHeight = 180 };
        var search = new TextBox { PlaceholderText = "Search customer" };
        search.TextChanged += (_, _) => list.ItemsSource = Model.Customers.Where(c => c.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name).ToArray();
        var useDefault = new CheckBox { Content = "Use as default for new invoices" };
        ShowOverlay("Select Customer", Ui.Stack(12, search, list, useDefault, Ui.Button("Clear default customer", () => Model.SetDefaultCustomer(null))), Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Select", () => { var c = Model.Customers.FirstOrDefault(c => c.Name == list.SelectedItem?.ToString()); if (c == null) return; foreach (var f in Model.InvoiceCustomer) f.Value = c[f.Label]; if (useDefault.IsChecked == true) Model.SetDefaultCustomer(c); CloseOverlay(); }, true)));
    }
    // Performs the show invoice success action for this screen or workflow.
    private void ShowInvoiceSuccess()
    {
        invoiceCompletionVisible = true;
        var document = Model.LastSavedDocument!;
        var outstanding = decimal.TryParse(document["Outstanding"], out var balance) ? balance : decimal.TryParse(document["Total"], out var total) ? total : 0m;
        Action? applyPaymentAction = outstanding <= 0.005m ? null : () => ShowPayment(document);
        var applyPayment = Ui.Button("Apply Payment", applyPaymentAction);
        applyPayment.IsEnabled = outstanding > 0.005m;
        page.Content = InvoiceCreatedScreen(document, applyPayment);
    }

    // Performs the invoice created screen action for this screen or workflow.
    private Control InvoiceCreatedScreen(UiRecord document, Button applyPayment)
    {
        var invoiceId = document.Name.TrimStart('#').Replace("[", "").Replace("]", "");
        return new InvoiceCreatedView(
            invoiceId,
            Model.EditorDocumentNumber,
            applyPayment.IsEnabled,
            () => ShowDocumentPreview(document),
            () => ShowPdfPreviewAsync(document),
            () => PrintDocumentAsync(document),
            () => { Model.StartDocument("Invoice"); page.Content = InvoiceEditor(); },
            () => ShowPayment(document));
    }
}
