using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private static readonly IBrush InvoiceSettingsBlue = Ui.HeaderBand;

    private Control InvoiceSettingsView()
    {
        string[] keys = ["General", "Branding", "Tax", "Items", "Customer", "Columns", "Custom Fields"];
        string[] labels = ["General", "Branding", "Tax & GST", "Invoice Items", "Customer Details", "Invoice Columns", "Custom Fields"];
        string[] icons = ["settings", "image", "percent", "view_list", "person", "view_column", "dashboard"];
        var content = new ContentControl();
        var nav = Ui.Stack(4);
        var buttons = new List<Button>();
        void Select(int index)
        {
            foreach (var button in buttons) button.Classes.Set("selected", button.Tag?.ToString() == labels[index]);
            Control fields = index switch
            {
                0 => InvoiceGeneralSettings(),
                1 => InvoiceBrandingSettings(),
                2 => InvoiceTaxSettings(),
                3 => InvoiceItemSettings(),
                4 => InvoiceCustomerSettings(),
                5 => InvoiceColumnSettings(),
                6 => InvoiceCustomSettings(),
                _ => Ui.Stack(16, Model.Settings["Invoice Settings"].Single(section => section.Title == keys[index]).Fields
                    .Select(field => field.Kind == "toggle" ? InvoiceToggle(field, field.Label, field.Help, icons[index]) : Ui.Field(field)).ToArray())
            };
            content.Content = new InvoiceSettingsPanelView(labels[index], fields);
        }
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            var button = Ui.Button(labels[i], () => Select(index));
            button.Classes.Clear(); button.Classes.Add("nav");
            button.MinHeight = 35.2; button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(12.8, 6.4);
            button.Content = Ui.Columns("20,12,*", Ui.Icon(icons[i], 18, InvoiceSettingsBlue), new Border(), Ui.LocalText(labels[i], 14));
            buttons.Add(button); nav.Children.Add(button);
        }
        var options = Ui.Button("See Options", () => Select(6));
        options.Content = Ui.LocalText("→  See Options", 12, color: InvoiceSettingsBlue);
        options.HorizontalAlignment = HorizontalAlignment.Stretch; options.MinHeight = 25.6;
        var promo = InvoiceSettingsCard(Ui.Stack(9.6,
            Ui.Columns("34,14,*", Ui.Icon("tune", 20, InvoiceSettingsBlue), new Border(), Ui.LocalText("Need more fields on your invoices?", 14, color: InvoiceSettingsBlue)),
            Ui.LocalText("Add PO number, project code, department, or any custom field.", 12, color: Ui.Muted), options), 14);
        promo.Background = Ui.Palette("#EAEEF3", "#252D38"); promo.BorderBrush = Brush.Parse("#BDCAE0");
        var save = Ui.Button("Save", () => Model.SaveSettings("Invoice Settings"), true);
        save.Background = InvoiceSettingsBlue; save.HorizontalAlignment = HorizontalAlignment.Stretch; save.MinHeight = 25.6;
        save.Content = Ui.Columns("Auto,8,Auto", Ui.Icon("save", 17, Brushes.White), new Border(), Ui.LocalText("Save", 13, true, Brushes.White));
        var layout = new SettingsNavigationShellView(nav, promo, save, content);
        Select(0);
        return new ScreenScaffoldView(Ui.AppBar("Invoice Settings"), layout);
    }

    private Control InvoiceGeneralSettings()
    {
        FormField F(string label) => Model.InvoiceSetting(label);
        Control starting = InvoiceInput(F("Starting Number"), "Starting Number", "pin");
        if (!Model.CanChangeInvoiceStartingNumber)
        {
            starting = new Border { Padding = new Thickness(9.6, 8), CornerRadius = new CornerRadius(10), Background = Ui.Palette("#FFF4E3", "#202B36"), BorderBrush = Brush.Parse("#FFCC80"), BorderThickness = new Thickness(1),
                Child = Ui.Columns("16,8,*", Ui.Icon("lock", 16, Brush.Parse("#EF7800")), new Border(), Ui.LocalText("Invoice starting number cannot be changed while invoices exist. Please permanently delete all invoices/quotations (including trash) and try again.", 12, color: Brush.Parse("#EF7800"))) };
        }
        return Ui.Stack(16,
            InvoicePair(InvoiceInput(F("Invoice Prefix"), "Invoice Prefix", "confirmation_number"), starting),
            InvoicePair(InvoiceToggle(F("Leading Zeros"), "Leading Zeros", "Pad invoice numbers to 8 digits (e.g. 00000007)", "pin"), InvoiceInput(F("Currency"), "Currency", "payments")),
            InvoicePair(InvoiceInput(F("Date Format"), "Date Format", "calendar_today"), InvoiceInput(F("Time Format"), "Time Format", "schedule")),
            InvoicePair(InvoiceToggle(F("Show time in PDF"), "Show Time on PDF", "Append the invoice creation time next to the date on PDFs and thermal receipts", "schedule"),
                Ui.Stack(4.8, InvoiceInput(F("Quantity Column"), "Quantity Column Label", "tag"), Ui.LocalText("Leave blank to use default \"Qty\"", 12, color: Ui.Muted))),
            InvoiceLongText(F("Additional Information"), "info_outline"), InvoiceLongText(F("Thank You Note"), "favorite_border"),
            InvoicePair(
                InvoiceToggle(F("Hide Invoice Number"), "Hide Invoice Number by Default", "Enable \"Hide invoice number in PDF\" by default when creating new invoices.", "confirmation_number"),
                InvoiceToggle(F("Show Invoice Number QR Code"), "Invoice Number QR Code", "Print a scannable QR code containing the displayed invoice number on generated PDFs.", "qr_code")));
    }

    private Control InvoiceTaxSettings()
    {
        FormField F(string label) => Model.InvoiceSetting(label);
        var modes = Ui.Stack(0); modes.Orientation = Orientation.Horizontal;
        var modeButtons = new List<Button>();
        void RefreshMode()
        {
            foreach (var button in modeButtons)
            {
                var selected = button.Tag?.ToString() == F("Tax Mode").Value;
                button.Background = selected ? Ui.Palette("#E8DEF8", "#3A4C69") : Brushes.Transparent;
                button.Content = (selected ? "✓  " : "") + button.Tag;
            }
        }
        foreach (var mode in F("Tax Mode").Options.Where(mode => mode != "No Tax" || F("Tax Mode").Value == "No Tax"))
        {
            var button = Ui.Button(mode, () => { F("Tax Mode").Value = mode; RefreshMode(); });
            button.MinHeight = 25.6; button.Padding = new Thickness(9.6, 4); button.CornerRadius = new CornerRadius(0);
            modeButtons.Add(button); modes.Children.Add(button);
        }
        RefreshMode();
        var titleLabel = Ui.LocalText("Default TAX Invoice Title", 14);
        titleLabel.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.IsChecked)) { Source = F("Show GST fields"), Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(value => value ? "Default GST Invoice Title" : "Default TAX Invoice Title") });
        return Ui.Stack(16,
            InvoicePair(Ui.Stack(4.8, InvoiceInput(F("Default Tax Rate (%)"), "Default Tax Rate (%)", "percent"), Ui.LocalText("Applied to new invoices", 12, color: Ui.Muted)), new Border()),
            InvoiceToggle(F("Tax Enabled"), "Tax Enabled by Default", "Enable the Tax toggle by default when creating new invoices.", "percent"),
            InvoiceSettingsCard(Ui.Stack(3.2, Ui.LocalText("Default Tax Rate Mode", 14), Ui.LocalText("Applies to new invoices only", 12), new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = modes }), 12),
            InvoiceToggle(F("Show GST fields"), "Show GST Fields", "Display GSTIN fields (HSN/SAC) on invoices, PDFs, and CSV exports", "receipt_long"),
            InvoiceSettingsCard(Ui.Stack(6.4, titleLabel, Ui.LocalText("Preselected on new invoices", 12, color: Ui.Muted), InvoiceInput(F("Default GST Title"), "", "")), 16),
            InvoiceToggle(F("Show Round Off"), "Show Round Off", "Show a Round off row + Net Amount (rounded to nearest) and amount in words on invoice PDFs.", "payments"));
    }

    private Control InvoiceBrandingSettings()
    {
        FormField F(string label) => Model.InvoiceSetting(label);
        var opacity = Ui.Field(F("Watermark Opacity"));
        opacity.Bind(IsVisibleProperty, new Binding(nameof(FormField.Value)) { Source = F("Watermark Image"), Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(value => !string.IsNullOrEmpty(value)) });
        return Ui.Stack(16,
            InvoicePair(InvoiceInput(F("Logo Position"), "Company Logo Position", ""), InvoiceSizeChoice(F("Logo Size"), "Company Logo Size", false)),
            InvoiceSettingsCard(Ui.Stack(9.6, Ui.LocalText("Signature Image", 14), Ui.LocalText("Printed on invoices as Authorised Signature\nPNG, JPG or JPEG — max 2 MB", 12, color: Ui.Muted),
                InvoiceImageUpload(F("Signature Image"), "Signature"),
                InvoicePair(InvoiceSizeChoice(F("Signature Size"), "Signature Size", true), InvoiceInput(F("Signature Position"), "Signature Position", "view_list"))), 16),
            InvoiceSettingsCard(Ui.Stack(9.6, Ui.LocalText("Watermark Image", 14), Ui.LocalText("Shown on invoice PDFs (not printed on thermal receipts)\nPNG, JPG or JPEG — max 2 MB", 12, color: Ui.Muted),
                InvoiceImageUpload(F("Watermark Image"), "Watermark"), opacity), 16));
    }

    private static Control InvoicePair(Control left, Control right)
    {
        var grid = Ui.Columns("*,24,*", left, new Border(), right);
        grid.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 480;
            grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,24,*");
            grid.RowDefinitions = new RowDefinitions(narrow ? "Auto,20,Auto" : "Auto");
            Grid.SetColumn(right, narrow ? 0 : 2); Grid.SetRow(right, narrow ? 2 : 0);
        };
        left.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Top;
        return grid;
    }

    private static Control InvoiceToggle(FormField field, string title, string help, string icon, bool compact = false, bool leading = false)
        => new InvoiceToggleSettingView(field, title, help, icon) { Compact = compact || leading, Leading = leading };

    private static Border InvoiceSettingsCard(Control content, double padding = 16)
    {
        var card = Ui.Card(content, padding);
        card.Background = Ui.Palette("#FFFFFF", "#252525");
        return card;
    }

    private static Control InvoiceInput(FormField field, string label, string icon)
        => new InvoiceInputView { DataContext = new InvoiceInputViewModel(field, label, icon) };

    private static Control InvoiceSizeChoice(FormField field, string label, bool signature)
        => new InvoiceSizeChoiceView(field, label, signature);

    private Control InvoiceLongText(FormField field, string icon)
    {
        var expand = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            var editor = new ExpandedTextViewModel(field.Value);
            ShowOverlay(field.Label, editor, new DialogActions([
                new DialogAction("Cancel", new CommunityToolkit.Mvvm.Input.RelayCommand(CloseOverlay)),
                new DialogAction("Apply", new CommunityToolkit.Mvvm.Input.RelayCommand(() => { field.Value = editor.Text; CloseOverlay(); }), true)
            ]), width: 700);
        });
        return new InvoiceLongTextView { DataContext = new InvoiceLongTextViewModel(field, icon, expand) };
    }

    private Control InvoiceImageUpload(FormField field, string name)
        => new InvoiceImageUploadView { DataContext = new InvoiceImageUploadViewModel(field, name,
            bytes => Model.SetInvoiceBrandingImage(field.Label, bytes)) };
}
