using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private Control InvoiceItemSettings() => Ui.Stack(20,
        InvoiceToggle(Model.InvoiceSetting("Show Alias Name"), "Show Alias Name in PDF", "Print a product's local-language alias (if set) instead of its actual name on PDFs", "translate"),
        InvoiceToggle(Model.InvoiceSetting("Allow Fractional Quantity"), "Allow Fractional Quantities", "Enable decimal quantities (e.g. 1.5 hrs, 0.5 kg)", "pin"),
        InvoiceToggle(Model.InvoiceSetting("Show Product / Service Tag"), "Show Product/Service Tag", "Show or hide the Product/Service label on each invoice item", "label"),
        InvoiceToggle(Model.InvoiceSetting("Allow Duplicate Items"), "Allow Duplicate Invoice Items", "Allow adding the same product more than once to an invoice", "content_copy"),
        InvoiceToggle(Model.InvoiceSetting("Show Previous Balance"), "Show Previous Balance Due", "Show calculated prior outstanding balance on invoice PDFs", "account_balance_wallet"));

    private Control InvoiceCustomerSettings() => Ui.Stack(20,
        Ui.Text("Choose which customer details print on invoice PDFs and thermal receipts. A field only shows when it's enabled and the customer has a value for it. Customer name is always shown.", 13, color: Ui.Muted),
        new Border { Height = 4 },
        InvoiceToggle(Model.InvoiceSetting("Show Customer Business Name"), "Show Business Name", "Print the customer's business name under their name", "business"),
        InvoiceToggle(Model.InvoiceSetting("Show Customer Address"), "Show Address", "Print the customer's address in the Bill To block", "location_on"),
        InvoiceToggle(Model.InvoiceSetting("Show Customer Phone"), "Show Phone", "Print the customer's phone number", "phone"),
        InvoiceToggle(Model.InvoiceSetting("Show Customer Email"), "Show Email", "Print the customer's email address (not shown on thermal receipts)", "email"),
        InvoiceToggle(Model.InvoiceSetting("Show Customer GSTIN"), "Show GSTIN / Tax ID", "Print the customer's GSTIN / tax id (requires GST fields on)", "badge"));

    private Control InvoiceColumnSettings()
    {
        FormField F(string label) => Model.InvoiceSetting(label);
        Control Required(string label)
        {
            var card = InvoiceToggle(new FormField(label, "true", "toggle"), label, "Always shown", "check_circle");
            card.IsEnabled = false; card.Opacity = .5;
            return card;
        }
        var newline = InvoiceToggle(F("Description on new line"), "Description on New Line", "Print the description on a separate row below the item name", "view_list");
        newline.Bind(IsVisibleProperty, new Binding(nameof(FormField.IsChecked)) { Source = F("Show Description") });
        var description = InvoiceSettingsCard(Ui.Stack(8,
            InvoiceToggle(F("Show Description"), "Show Product Description", "Print each item's description as a row under it on A4 PDFs (not on thermal receipts)", "view_list"), newline), 8);
        description.Background = Ui.Palette("#F0F1F3", "#303030");
        var metadata = Ui.Stack(10, Ui.Text("Product metadata columns", 14), Ui.Text("Print product metadata as extra columns in the items table.", 12, color: Ui.Muted),
            Ui.Text("Note: Metadata columns are only applied to the Grid Classic PDF template.", 12, color: Brushes.Red));
        foreach (var label in MainWindowViewModel.InvoiceMetadataLabels) metadata.Children.Add(InvoiceToggle(F("Metadata: " + label), label, "", "", true));
        return Ui.Stack(20,
            Ui.Text("Choose which columns appear in the invoice PDF items table. Item Name, Price and Total are always shown.", 13, color: Ui.Muted), new Border { Height = 4 },
            InvoiceToggle(F("Show Sl. No."), "Sl No column", "Print the serial-number column on A4/Letter invoices", "format_list_numbered"),
            Required("Item Name column"), description,
            InvoiceToggle(F("Show GST fields"), "HSN/SAC column", "Print the HSN/SAC code column (tied to Show GST Fields)", "receipt_long"),
            InvoiceToggle(F("Show Quantity"), "Show Quantity Field", "Hide quantity for service-based billing; price column becomes \"Rate\"", "pin"),
            Required("Price / Rate column"),
            InvoiceToggle(F("Tax"), "Tax column", "Print the per-item CGST/SGST, IGST % column", "percent"),
            InvoiceToggle(F("Show Discount"), "Show Discount Column", "Hide discount column for clients who don't use item-level discounts", "discount"),
            Required("Total column"), InvoiceSettingsCard(metadata, 12));
    }

    private Control CustomFieldsPreview(bool full = false)
    {
        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), HorizontalAlignment = HorizontalAlignment.Stretch };
        var fields = Model.InvoiceCustomFieldDefinitions.ToArray();
        for (var i = 0; i < fields.Length; i++)
        {
            if (i % 3 == 0) table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = Ui.Text(fields[i].Label, full ? 14 : 11, true);
            label.Bind(TextBlock.TextProperty, new Binding(nameof(InvoiceCustomFieldDefinition.Label)) { Source = fields[i] });
            var sample = Ui.Text("Sample value", full ? 14 : 11, color: Ui.Muted); sample.FontStyle = FontStyle.Italic;
            var cell = new Border { Padding = new Thickness(6), BorderBrush = Ui.Palette("#EAE4EE", "#526171"), BorderThickness = new Thickness(1), Child = Ui.Stack(3, label, sample) };
            Grid.SetRow(cell, i / 3); Grid.SetColumn(cell, i % 3); table.Children.Add(cell);
        }
        return table;
    }

    private Control InvoiceCustomSettings()
    {
        var rows = Ui.Stack(16);
        var livePreview = new ContentControl();
        var samplePreview = new ContentControl();
        void Render()
        {
            rows.Children.Clear();
            for (var i = 0; i < Model.InvoiceCustomFieldDefinitions.Count; i++)
            {
                var field = Model.InvoiceCustomFieldDefinitions[i];
                Button Action(string title, string icon, Action action)
                {
                    var button = Ui.Button(title + " " + field.Id, action); button.Content = Ui.Icon(icon, 20, title == "Delete field" ? Brushes.Red : Ui.Muted);
                    button.Classes.Add("text"); button.Padding = new Thickness(4); button.MinWidth = 30;
                    ToolTip.SetTip(button, title); return button;
                }
                var up = Action("Move up", "arrow_upward", () => Model.MoveInvoiceCustomField(field, -1)); up.IsEnabled = i > 0;
                var down = Action("Move down", "arrow_downward", () => Model.MoveInvoiceCustomField(field, 1)); down.IsEnabled = i < Model.InvoiceCustomFieldDefinitions.Count - 1;
                var box = new TextBox { MinHeight = 48, MaxLength = 100, Background = Ui.Palette("#FFFFFF", "#252525") };
                box.Bind(TextBox.TextProperty, new Binding(nameof(InvoiceCustomFieldDefinition.Label)) { Source = field, Mode = BindingMode.TwoWay });
                AutomationProperties.SetName(box, "Field label " + field.Id);
                var input = new Grid(); input.Children.Add(box);
                input.Children.Add(new Border { Background = Ui.Surface, Padding = new Thickness(4, 0), Margin = new Thickness(9, -8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Ui.Text("Field label", 12, color: Ui.Muted) });
                rows.Children.Add(Ui.Columns("30,30,8,*,8,32", up, down, new Border(), input, new Border(), Action("Delete field", "delete", () => Model.RemoveInvoiceCustomField(field))));
            }
            livePreview.Content = CustomFieldsPreview();
            samplePreview.Content = CustomFieldsPreview();
        }
        var newLabel = new TextBox { PlaceholderText = "New field label", MinHeight = 48, MaxLength = 100 };
        AutomationProperties.SetName(newLabel, "New field label");
        void Add() { if (Model.AddInvoiceCustomField(newLabel.Text ?? "")) newLabel.Text = ""; }
        var add = Ui.Button("Add custom field", Add, true); add.Content = Ui.Text("＋ Add", 14, true, Brushes.White); add.Background = Brush.Parse("#6750A4"); add.MinHeight = 32; add.CornerRadius = new CornerRadius(18);
        newLabel.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Add(); e.Handled = true; } };
        var previewCard = InvoiceSettingsCard(Ui.Stack(16, Ui.Columns("16,8,*", Ui.Icon("visibility", 16), new Border(), Ui.Text("Preview", 13)), livePreview,
            Ui.Text("ⓘ  Preview may slightly differ in the final PDF.", 12, color: Ui.Muted)), 16);
        previewCard.Background = Ui.Palette("#FDF7FF", "#25212B");
        var definitionEditor = Ui.Stack(16, rows, Ui.Columns("*,8,Auto", newLabel, new Border(), add), previewCard);
        definitionEditor.Bind(IsVisibleProperty, new Binding(nameof(FormField.IsChecked)) { Source = Model.InvoiceSetting("Enable Custom Fields") });
        var example = Ui.Button("View custom field example", () => ShowOverlay("Custom Fields Preview", Ui.Scroll(CustomFieldsPreview(true)), Ui.Button("Close", CloseOverlay), width: 900));
        example.Content = samplePreview; example.Height = 140; example.ClipToBounds = true; example.HorizontalAlignment = HorizontalAlignment.Stretch; example.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var panel = Ui.Stack(24,
            Ui.Text("Define fields once here (e.g. Vehicle No, Delivery Note), then fill their values on each invoice. Not tied to the customer.", 13, color: Ui.Muted),
            Ui.Text("Note: Custom fields are only printed on the Grid Classic PDF template.", 14, color: Brushes.Red), example,
            Ui.Text("Tap to view full size", 11, color: Ui.Muted),
            InvoiceToggle(Model.InvoiceSetting("Enable Custom Fields"), "Enable Custom Fields", "Show a Custom Fields section on the create-invoice screen", "dashboard"), definitionEditor);
        System.Collections.Specialized.NotifyCollectionChangedEventHandler changed = (_, _) => Render();
        panel.AttachedToVisualTree += (_, _) => Model.InvoiceCustomFieldDefinitions.CollectionChanged += changed;
        panel.DetachedFromVisualTree += (_, _) => Model.InvoiceCustomFieldDefinitions.CollectionChanged -= changed;
        Render();
        return panel;
    }
}
