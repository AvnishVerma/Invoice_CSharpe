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
            var heading = Ui.Columns("4,12,*", new Border { Height = 19.2, Background = InvoiceSettingsBlue, CornerRadius = new CornerRadius(2) }, new Border(), Ui.LocalText(labels[index], 20, true));
            var card = InvoiceSettingsCard(Ui.Stack(25.6, heading, fields), 32);
            card.Background = Ui.Surface;
            card.BorderThickness = new Thickness(0);
            card.CornerRadius = new CornerRadius(18);
            card.MaxWidth = 900;
            card.VerticalAlignment = VerticalAlignment.Top;
            content.Content = Ui.Scroll(card, 28);
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
        var rail = new Border { Background = Ui.Surface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0),
            Child = Ui.Rows("*,Auto,Auto", Ui.Scroll(nav, 12), new Border { Padding = new Thickness(12.8), Child = promo }, new Border { Padding = new Thickness(12.8, 0, 12.8, 12.8), Child = save }) };
        var layout = Ui.Columns("240,*", rail, content);
        layout.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 760;
            layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "240,*");
            layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");
            Grid.SetColumn(content, narrow ? 0 : 1); Grid.SetRow(content, narrow ? 1 : 0);
            rail.Height = narrow ? 190 : double.NaN; promo.IsVisible = !narrow;
        };
        Select(0);
        return Ui.Rows("Auto,*", Ui.AppBar("Invoice Settings"), layout);
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
            InvoiceToggle(F("Hide Invoice Number"), "Hide Invoice Number by Default", "Enable \"Hide invoice number in PDF\" by default when creating new invoices.", "confirmation_number"));
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
    {
        var track = new Border { Width = 41.6, Height = 25.6, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(2), Padding = new Thickness(3.2) };
        var thumb = new Avalonia.Controls.Shapes.Ellipse { Width = 16, Height = 16 };
        track.Child = thumb;
        var toggle = new ToggleButton { Content = track, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        toggle.Classes.Add("form-toggle");
        toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = field, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(toggle, field.Label);
        void Paint()
        {
            track.Background = toggle.IsChecked == true ? Brush.Parse("#8097BD") : Brushes.White;
            track.BorderBrush = toggle.IsChecked == true ? Brushes.Transparent : Brush.Parse("#BDBDBD");
            thumb.Fill = toggle.IsChecked == true ? InvoiceSettingsBlue : Brush.Parse("#BDBDBD");
            thumb.HorizontalAlignment = toggle.IsChecked == true ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        toggle.IsCheckedChanged += (_, _) => Paint(); Paint();
        if (leading) return Ui.Columns("52,16,*", toggle, new Border(), Ui.LocalText(title, 14));
        if (compact) return Ui.Columns("*,16,Auto", Ui.LocalText(title, 14), new Border(), toggle);
        return InvoiceSettingsCard(Ui.Columns("24,16,*,16,Auto", Ui.Icon(icon, 22, InvoiceSettingsBlue), new Border(), Ui.Stack(3.2, Ui.LocalText(title, 16), Ui.LocalText(help, 13, color: Ui.Muted)), new Border(), toggle), 12);
    }

    private static Border InvoiceSettingsCard(Control content, double padding = 16)
    {
        var card = Ui.Card(content, padding);
        card.Background = Ui.Palette("#FFFFFF", "#252525");
        return card;
    }

    private static Control InvoiceInput(FormField field, string label, string icon)
    {
        Control input;
        var padding = new Thickness(icon.Length > 0 ? 36 : 10, 8, 10, 8);
        if (field.Kind == "choice")
        {
            var combo = new ComboBox { ItemsSource = field.Options, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 38.4, Padding = padding, Background = Ui.Palette("#FFFFFF", "#252525") };
            combo.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay });
            if (field.Label == "Time Format") combo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((value, _) => Ui.Text(value == "12 hour" ? "12-hour (2:30 PM)" : "24-hour (14:30)", 15));
            input = combo;
        }
        else
        {
            var box = new TextBox { MinHeight = 38.4, Padding = padding, MaxLength = field.Label == "Invoice Prefix" ? 25 : field.MaxLength, Background = Ui.Palette("#FFFFFF", "#252525") };
            box.Bind(TextBox.TextProperty, new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay });
            input = box;
        }
        AutomationProperties.SetName(input, field.Label);
        var grid = new Grid(); grid.Children.Add(input);
        if (icon.Length > 0) grid.Children.Add(new Border { Padding = new Thickness(8, 0), HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false, Child = Ui.Icon(icon, 22) });
        if (label.Length > 0) grid.Children.Add(new Border { Background = Ui.CardSurface, Padding = new Thickness(3.2, 0), Margin = new Thickness(12, -8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = Ui.LocalText(label, 12, color: Ui.Muted) });
        var error = Ui.LocalText("", 12, color: Brushes.Firebrick);
        error.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Error)) { Source = field });
        error.Bind(IsVisibleProperty, new Binding(nameof(FormField.Error)) { Source = field, Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(text => !string.IsNullOrEmpty(text)) });
        return Ui.Stack(3.2, grid, error);
    }

    private static Control InvoiceSizeChoice(FormField field, string label, bool signature)
    {
        // Keep existing numeric settings compatible while presenting the legacy size names.
        var sizes = signature ? new[] { ("Small", "35"), ("Medium", "50"), ("Large", "70") } : new[] { ("X-Small", "40"), ("Small", "60"), ("Medium", "90"), ("Large", "120") };
        var value = sizes.FirstOrDefault(size => size.Item2 == field.Value).Item1;
        var names = sizes.Select(size => size.Item1).ToList();
        if (value == null) { value = "Custom (" + field.Value + ")"; names.Add(value); }
        var choice = new FormField(field.Label, value, "choice", names.ToArray());
        choice.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FormField.Value) && sizes.FirstOrDefault(size => size.Item1 == choice.Value) is var selected && selected.Item2 != null) field.Value = selected.Item2; };
        return InvoiceInput(choice, label, "");
    }

    private Control InvoiceLongText(FormField field, string icon)
    {
        var box = new TextBox { MinHeight = 76.8, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, PlaceholderText = field.Label, Padding = new Thickness(35.2, 11.2, 32, 9.6), MaxLength = 10000, Background = Ui.Palette("#FFFFFF", "#252525") };
        box.Bind(TextBox.TextProperty, new Binding(nameof(FormField.Value)) { Source = field, Mode = BindingMode.TwoWay });
        var expand = Ui.Button("Expand " + field.Label, () =>
        {
            var editor = new TextBox { Text = field.Value, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 260, MaxLength = 10000 };
            ShowOverlay(field.Label, editor, Ui.Wrap(Ui.Button("Cancel", CloseOverlay), Ui.Button("Apply", () => { field.Value = editor.Text ?? ""; CloseOverlay(); }, true)), width: 700);
        });
        expand.Content = Ui.Icon("open_in_full", 20); expand.Classes.Add("text"); expand.HorizontalAlignment = HorizontalAlignment.Right;
        var grid = new Grid(); grid.Children.Add(box); grid.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8), Child = Ui.Icon(icon, 23), IsHitTestVisible = false }); grid.Children.Add(expand);
        return grid;
    }

    private Control InvoiceImageUpload(FormField field, string name)
    {
        var preview = new Image { Height = 48, MaxWidth = 200, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        Avalonia.Media.Imaging.Bitmap? bitmap = null;
        var remove = Ui.Button("Remove " + name, () => { });
        remove.Click += (_, _) => { field.Value = ""; Refresh(); };
        var upload = Ui.Button("Upload " + name, () => { });
        upload.Content = Ui.Columns("Auto,8,*", Ui.Icon("upload", 16, Brush.Parse("#6750A4")), new Border(), Ui.LocalText("Upload " + name, 14, color: Brush.Parse("#6750A4")));
        upload.MinHeight = 25.6; upload.Padding = new Thickness(12.8, 4); upload.CornerRadius = new CornerRadius(18);
        var error = Ui.LocalText("", 12, color: Brushes.Firebrick);
        error.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Error)) { Source = field });
        error.Bind(IsVisibleProperty, new Binding(nameof(FormField.Error)) { Source = field, Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(value => !string.IsNullOrEmpty(value)) });
        var panel = Ui.Stack(6.4, preview, Ui.Wrap(upload, remove), error);
        void Refresh()
        {
            preview.Source = null; bitmap?.Dispose(); bitmap = Ui.LoadLogo(field.Value); preview.Source = bitmap;
            preview.IsVisible = remove.IsVisible = bitmap != null;
        }
        upload.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Upload " + name, AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("PNG or JPEG image") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }] });
            if (files.Count == 0) return;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
                if (!Model.SetInvoiceBrandingImage(field.Label, bytes.ToArray())) return;
                Refresh();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException) { field.Error = "The selected image could not be read."; }
        };
        panel.AttachedToVisualTree += (_, _) => Refresh();
        panel.DetachedFromVisualTree += (_, _) => { preview.Source = null; bitmap?.Dispose(); bitmap = null; };
        return panel;
    }
}
