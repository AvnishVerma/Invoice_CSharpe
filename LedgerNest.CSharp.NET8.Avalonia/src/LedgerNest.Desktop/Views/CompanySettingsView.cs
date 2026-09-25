using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;
using Avalonia.Styling;
using LedgerNest.Desktop.Printing;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the company settings view action for this screen or workflow.
    private Control CompanySettingsView()
    {
        var sections = Model.Settings["Company Info"];
        var fields = sections[1].Fields;
        FormField F(string label) => fields.First(f => f.Label == label);
        var previewName = Ui.Text(F("Company Name").Value, 18, true);
        previewName.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Value)) { Source = F("Company Name") });
        var logo = Ui.Button("", () => { }); logo.Width = 180; logo.Height = 180;
        logo.Content = Ui.Stack(9.6, Ui.Icon("upload", 40, Ui.Primary), Ui.LocalText("Upload Logo", 14, color: Ui.Muted), Ui.LocalText("Click to browse", 12, color: Ui.Muted));
        Avalonia.Media.Imaging.Bitmap? logoBitmap = Ui.LoadLogo(sections[0].Fields[0].Value);
        if (logoBitmap != null) logo.Content = new Image { Source = logoBitmap, Stretch = Stretch.Uniform };
        logo.DetachedFromVisualTree += (_, _) => logoBitmap?.Dispose();
        logo.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Company Logo", FileTypeFilter = [Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll] });
            if (files.Count == 0) return;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var imageBytes = new MemoryStream();
                await stream.CopyToAsync(imageBytes);
                if (imageBytes.Length > 2 * 1024 * 1024) { Model.Status = "Logo must be 2 MB or smaller."; return; }
                imageBytes.Position = 0;
                var bitmap = new Avalonia.Media.Imaging.Bitmap(imageBytes);
                if (bitmap.PixelSize.Width > 1080 || bitmap.PixelSize.Height > 1080) { bitmap.Dispose(); Model.Status = "Logo must be at most 1080 × 1080 pixels."; return; }
                logoBitmap?.Dispose(); logoBitmap = bitmap;
                logo.Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }; sections[0].Fields[0].Value = "base64:" + Convert.ToBase64String(imageBytes.ToArray());
            }
            catch (Exception ex) when (ex is IOException or ArgumentException) { Model.Status = "The selected image could not be opened."; }
        };
        var save = Ui.Button("Save", () => Model.SaveSettings("Company Info"), true); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        var businessType = sections[2].Fields[0];
        var businessIcon = Ui.Icon("account_tree", 25, Brush.Parse("#003B87"));
        businessIcon.VerticalAlignment = VerticalAlignment.Top;
        businessIcon.Margin = new Thickness(0, 4, 0, 0);
        var businessCard = Ui.Card(Ui.Columns("Auto,16,*",
            businessIcon,
            new Border(),
            Ui.Stack(6.4,
                Ui.LocalText("Business Type", 16),
                Ui.LocalText("Controls item type options in the product list and invoices", 12, color: Ui.Muted),
                CompanyBusinessTypeSegments(businessType))), 16);
        var qrCard = Ui.Card(Ui.Columns("Auto,16,*",
            Ui.Icon("credit_card", 25, Ui.Muted),
            new Border(),
            Ui.Field(sections[3].Fields[0], "Show QR Code on Invoices")), 16);
        var bankCard = Ui.Card(Ui.Columns("Auto,16,*",
            Ui.Icon("account_balance", 25, Ui.Muted),
            new Border(),
            Ui.Field(sections[3].Fields[1], "Show Bank Details on Invoices")), 16);
        var details = Ui.Stack(12.8,
            Ui.LocalText("COMPANY DETAILS", 12, true, Ui.Muted),
            Ui.Fields([F("Company Name"), F("GSTIN")], 2),
            Ui.Fields([F("PAN"), F("FSSAI Code")], 2),
            Ui.Fields([F("Country"), F("Phone"), F("Email")], 3),
            Ui.Field(F("Website")),
            Ui.Field(F("Address")),
            new Border { Height = 8 },
            Ui.LocalText("BUSINESS TYPE", 12, true, Ui.Muted),
            businessCard,
            new Border { Height = 8 },
            Ui.LocalText("PAYMENT SETTINGS", 12, true, Ui.Muted),
            qrCard,
            Ui.LocalText("UPI ACCOUNTS", 12, true, Ui.Muted));

        var upiRows = Ui.Stack(10);
        var bankRows = Ui.Stack(10);
        void RenderUpiRows()
        {
            upiRows.Children.Clear();
            foreach (var account in Model.UpiAccounts)
            {
                var remove = Ui.Button("−", () => Model.RemoveUpiAccount(account));
                ToolTip.SetTip(remove, "Remove UPI account");
                upiRows.Children.Add(Ui.Columns("*,2*,Auto", Ui.Field(account[0]), Ui.Field(account[1]), remove));
            }
        }
        void RenderBankRows()
        {
            bankRows.Children.Clear();
            foreach (var account in Model.BankAccounts)
            {
                var remove = Ui.Button("−", () => Model.RemoveBankAccount(account));
                ToolTip.SetTip(remove, "Remove bank account");
                bankRows.Children.Add(Ui.Columns("*,*,1.25*,*,Auto", Ui.Field(account[0]), Ui.Field(account[1]), Ui.Field(account[2]), Ui.Field(account[3]), remove));
            }
        }
        RenderUpiRows();
        RenderBankRows();
        System.Collections.Specialized.NotifyCollectionChangedEventHandler upiChanged = (_, _) => RenderUpiRows();
        System.Collections.Specialized.NotifyCollectionChangedEventHandler bankChanged = (_, _) => RenderBankRows();
        details.AttachedToVisualTree += (_, _) => { Model.UpiAccounts.CollectionChanged += upiChanged; Model.BankAccounts.CollectionChanged += bankChanged; };
        details.DetachedFromVisualTree += (_, _) => { Model.UpiAccounts.CollectionChanged -= upiChanged; Model.BankAccounts.CollectionChanged -= bankChanged; };
        details.Children.Add(upiRows);
        var addUpi = Ui.Button("＋ Add UPI Account", Model.AddUpiAccount);
        addUpi.Classes.Add("text");
        addUpi.HorizontalAlignment = HorizontalAlignment.Left;
        details.Children.Add(addUpi);
        details.Children.Add(new Border { Height = 12 });
        details.Children.Add(bankCard);
        details.Children.Add(Ui.LocalText("BANK ACCOUNTS", 12, true, Ui.Muted));
        details.Children.Add(bankRows);
        var addBank = Ui.Button("＋ Add Bank Account", Model.AddBankAccount);
        addBank.Classes.Add("text");
        addBank.HorizontalAlignment = HorizontalAlignment.Left;
        details.Children.Add(addBank);
        var language = new ComboBox { ItemsSource = UiLocalization.Languages, SelectedItem = Model.Language, MinWidth = 145, MinHeight = 32, Padding = new Thickness(10, 5), Background = Ui.Canvas, Foreground = Ui.TextColor };
        language.SelectionChanged += (_, _) => Model.SetLanguage(language.SelectedItem?.ToString() ?? "English");
        Avalonia.Automation.AutomationProperties.SetName(language, "Language");
        var theme = new ComboBox { ItemsSource = new[] { "Light", "Dark", "System" }, SelectedItem = Model.ThemeMode, MinWidth = 100, MinHeight = 32, Padding = new Thickness(10, 5), Background = Ui.Canvas, Foreground = Ui.TextColor };
        theme.SelectionChanged += (_, _) => ApplyTheme(theme.SelectedItem?.ToString() ?? "Light");
        return new CompanySettingsInfoView(Ui.AppBar("Company Information", language, theme), logo, Ui.Field(sections[0].Fields[1]), previewName, save, details);
    }

    // Builds the icon-backed Product, Service, and Both selector used by Company Information.
    private static Control CompanyBusinessTypeSegments(FormField field)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<Button>();
        void UpdateSelection()
        {
            foreach (var button in buttons)
            {
                var selected = button.Tag?.ToString() == field.Value;
                button.Background = selected ? Ui.Palette("#E8DEF8", "#3A4C69") : Brushes.Transparent;
                button.Foreground = selected ? Ui.Palette("#4F4268", "#E5DBFA") : Ui.TextColor;
            }
        }
        foreach (var (label, iconName) in new[] { ("Product", "inventory_2"), ("Service", "build"), ("Both", "check") })
        {
            var button = Ui.Button(label, () => { field.Value = label; UpdateSelection(); });
            button.Tag = label;
            button.Padding = new Thickness(9.6, 4.8);
            button.MinHeight = 25.6;
            button.CornerRadius = new CornerRadius(0);
            button.Content = Ui.Columns("Auto,7,*", Ui.Icon(iconName, 17, Ui.TextColor), new Border(), Ui.LocalText(label, 14));
            buttons.Add(button);
            panel.Children.Add(button);
        }
        UpdateSelection();
        return new Border
        {
            BorderBrush = Ui.Outline,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = panel
        };
    }

    // Performs the apply theme action for this screen or workflow.
    private void ApplyTheme(string mode)
    {
        Model.SetThemeMode(mode);
    }

    // Performs the pdf settings view action for this screen or workflow.
    private Control PdfSettingsView()
    {
        var sections = Model.Settings["PDF Settings"];
        var printer = sections.Single(section => section.Title == "PRINTING").Fields.Single(field => field.Label == "Printer");
        printer.Options = new[] { PlatformPrinterService.DefaultPrinter, printer.Value }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var pageSize = sections[0].Fields[0]; var selectedTemplate = sections[1].Fields[0]; var color = sections[3].Fields[0];
        var templateList = Ui.Stack(8); var preview = new ContentControl(); var options = new ContentControl();
        var buttons = new List<Button>();
        void Display()
        {
            foreach (var b in buttons)
            {
                var template = b.Tag?.ToString() ?? "Classic";
                b.IsVisible = pageSize.Value switch { "A5" => template == "Grid Classic", "A6" => template is "Compact" or "Grid Classic", "Thermal 80mm" or "Thermal 58mm" => template == "Thermal", _ => template is not ("Compact" or "Thermal") };
                b.BorderBrush = template == selectedTemplate.Value ? Ui.Primary : Ui.Outline; b.BorderThickness = new Thickness(template == selectedTemplate.Value ? 2 : 1); b.Background = template == selectedTemplate.Value ? Ui.Palette("#ECE8F3", "#3A4C69") : Ui.Palette("#FAFAFA", "#202B36");
                if (b.Content is Grid tile && tile.Children.LastOrDefault() is TextBlock check) check.IsVisible = template == selectedTemplate.Value;
            }
            var controls = Ui.Stack(9.6, Ui.Columns("*,Auto", Ui.Text(selectedTemplate.Value, 18, true), PdfActiveBadge()), Ui.Text(TemplateDescription(selectedTemplate.Value), 12.5, color: Ui.Muted));
            controls.Children.Add(new Border { Height = 4 });
            controls.Children.Add(Ui.LocalText("DISPLAY OPTIONS", 12, true, Ui.Muted));
            if (selectedTemplate.Value is "Compact" or "Grid Classic" or "Thermal")
            {
                var quantity = new ToggleSwitch { OnContent = null, OffContent = null, MinWidth = 0 };
                quantity.Bind(ToggleSwitch.IsCheckedProperty, new Binding(nameof(FormField.IsChecked)) { Source = sections[2].Fields[0], Mode = BindingMode.TwoWay });
                controls.Children.Add(Ui.Card(Ui.Columns("*,Auto", Ui.LocalText("Show total quantity row", 14), quantity), 12));
            }
            var orientation = sections[0].Fields[1];
            var segments = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var value in orientation.Options)
            {
                var segment = Ui.Button(value, () => { orientation.Value = value; Display(); });
                segment.Content = (orientation.Value == value ? "✓  " : "") + value;
                segment.Background = orientation.Value == value ? Ui.Palette("#E8DEF8", "#3A4C69") : Brushes.Transparent;
                segment.Padding = new Thickness(9.6, 4); segment.MinHeight = 24; segment.CornerRadius = new CornerRadius(0);
                segments.Children.Add(segment);
            }
            controls.Children.Add(Ui.Card(Ui.Stack(6.4, Ui.LocalText("Orientation", 14), new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = segments }), 12));
            controls.Children.Add(Ui.LocalText("THEME COLOR", 12, true, Ui.Muted));
            var swatches = Ui.Wrap();
            foreach (var hex in new[] { "#002E78", "#2563EB", "#047857", "#7C2D12", "#6D28D9" })
            { var b = Ui.Button("", () => { color.Value = hex; Display(); }); b.Background = Brush.Parse(hex); b.Width = 17.6; b.Height = 17.6; b.MinHeight = 17.6; b.Padding = new Thickness(0); b.CornerRadius = new CornerRadius(11); ToolTip.SetTip(b, hex); swatches.Children.Add(b); }
            var defaultColor = Ui.Button("Default", () => { color.Value = "#002E78"; Display(); }); defaultColor.Classes.Add("text"); defaultColor.MinHeight = 19.2; defaultColor.Padding = new Thickness(4.8, 0);
            swatches.Children.Add(defaultColor);
            var colorInput = new TextBox { MinHeight = 32 };
            colorInput.Bind(TextBox.TextProperty, new Binding(nameof(FormField.Value)) { Source = color, Mode = BindingMode.TwoWay });
            colorInput.LostFocus += (_, _) => preview.Content = InvoicePreview(selectedTemplate.Value, color.Value, orientation.Value == "Landscape");
            controls.Children.Add(Ui.Card(Ui.Stack(9.6, Ui.LocalText("Theme Color", 14), swatches, colorInput), 12));
            var custom = Ui.Card(Ui.Stack(6.4, Ui.LocalText("⚒  Want a custom template?", 14, color: Ui.Primary), Ui.LocalText("Get a design that matches your brand — colors, fonts, and layout.", 12, color: Ui.Muted), Ui.Button("→  Customization Options", () => { settingsTab = "Customize"; page.Content = SettingsView(); })), 12);
            custom.Background = Ui.Palette("#EEEBF8", "#202B36"); custom.BorderBrush = Brush.Parse("#C8C1E7");
            controls.Children.Add(custom);
            controls.Children.Add(new Expander { Header = "Printing & advanced options", Content = Ui.Stack(12.8, Ui.Fields(sections[2].Fields.Skip(1)), Ui.Fields(sections.Single(section => section.Title == "PRINTING").Fields)), HorizontalAlignment = HorizontalAlignment.Stretch });
            options.Content = Ui.Scroll(controls, 16);
            preview.Content = InvoicePreview(selectedTemplate.Value, color.Value, orientation.Value == "Landscape");
        }
        foreach (var template in selectedTemplate.Options.OrderBy(t => t == "Grid Classic" ? 0 : 1))
        {
            var button = Ui.Button(template, () => { selectedTemplate.Value = template; Display(); }); button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(7.2); button.CornerRadius = new CornerRadius(12);
            var description = Ui.Text(TemplateDescription(template), 12, color: Ui.Muted); description.MaxHeight = 36; description.TextTrimming = TextTrimming.CharacterEllipsis;
            var labels = Ui.Stack(3.2, Ui.Text(template, 16), description);
            if (template == "Classic") labels.Children.Add(new Border { Background = Ui.Palette("#E5E5E5", "#202B36"), Padding = new Thickness(4, 0.8), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.LocalText("Default", 11, color: Ui.Muted) });
            button.Content = Ui.Columns("72,*,18", new TemplateSketch(template, Brushes.Black, false) { Width = 51.2, Height = 59.2 }, labels, Ui.Icon("check_circle", 15, Ui.Primary));
            buttons.Add(button); templateList.Children.Add(button);
        }
        var pageChoice = new ComboBox { ItemsSource = pageSize.Options, HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 32 };
        pageChoice.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((value, _) => Ui.Text(value == "A4" ? "Standard A4" : value ?? "", 15));
        pageChoice.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(FormField.Value)) { Source = pageSize, Mode = BindingMode.TwoWay });
        var templates = Ui.Card(Ui.Rows("Auto,Auto,*", Ui.Stack(6.4, Ui.LocalText("PAGE SIZE", 12, true, Ui.Muted), pageChoice), new Border { Padding = new Thickness(0, 12.8, 0, 8), Child = Ui.LocalText("TEMPLATES", 12, true, Ui.Muted) }, Ui.Scroll(templateList, 0)), 12);
        var settings = Ui.Card(options, 0); var previewCard = Ui.Card(Ui.Rows("Auto,*,Auto", new Border { Padding = new Thickness(12.8, 8), Child = Ui.LocalText("Preview", 13, true) }, preview, new Border { Padding = new Thickness(12.8, 8), Child = Ui.LocalText("Preview may slightly differ in the final PDF.", 12, color: Ui.Muted) }), 0);
        templates.Background = settings.Background = previewCard.Background = Ui.Palette("#FDF7FF", "#25212B");
        System.ComponentModel.PropertyChangedEventHandler pageSizeChanged = (_, e) =>
        {
            if (e.PropertyName != nameof(FormField.Value)) return;
            selectedTemplate.Value = pageSize.Value switch { "A5" => "Grid Classic", "A6" => "Compact", "Thermal 80mm" or "Thermal 58mm" => "Thermal", _ => "Classic" }; Display();
        };
        Display();
        var trackedFields = sections.SelectMany(section => section.Fields).ToArray();
        var savedValues = trackedFields.Select(field => field.Value).ToArray();
        var save = Ui.Button("Save Settings", null, true);
        save.Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            if (Model.SaveSettings("PDF Settings")) { savedValues = trackedFields.Select(field => field.Value).ToArray(); save.IsEnabled = false; }
        });
        save.IsEnabled = false;
        System.ComponentModel.PropertyChangedEventHandler settingChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(FormField.Value)) save.IsEnabled = !trackedFields.Select(field => field.Value).SequenceEqual(savedValues);
        };
        var reset = Ui.Button("Reset to Default", () =>
        {
            var defaults = FormCatalog.Settings()["PDF Settings"].SelectMany(section => section.Fields).ToArray();
            for (var i = 0; i < trackedFields.Length; i++) trackedFields[i].Value = defaults[i].Value;
            Display();
        });
        reset.CornerRadius = new CornerRadius(18); reset.MinHeight = save.MinHeight = 25.6;
        reset.Padding = save.Padding = new Thickness(14.4, 4.8);
        var header = Ui.AppBar("PDF Settings", reset, save);
        var view = new PdfSettingsShellView(header, templates, settings, previewCard);
        view.AttachedToVisualTree += async (_, _) =>
        {
            pageSize.PropertyChanged += pageSizeChanged;
            foreach (var field in trackedFields) field.PropertyChanged += settingChanged;
            try
            {
                var discovered = await printService.GetPrintersAsync();
                printer.Options = new[] { PlatformPrinterService.DefaultPrinter }
                    .Concat(discovered.Where(item => !PrinterClassifier.IsFilePrinter(item.Name)).Select(item => item.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!printer.Options.Contains(printer.Value, StringComparer.OrdinalIgnoreCase))
                    printer.Value = discovered.FirstOrDefault(item => item.IsDefault && !PrinterClassifier.IsFilePrinter(item.Name))?.Name ?? printer.Options[0];
                Display();
            }
            catch (Exception ex)
            {
                AppErrorLog.Write(ex, "Refreshing printer choices");
                Model.Status = "Printers could not be refreshed. Check the printing service configuration.";
            }
        };
        view.DetachedFromVisualTree += (_, _) =>
        {
            pageSize.PropertyChanged -= pageSizeChanged;
            foreach (var field in trackedFields) field.PropertyChanged -= settingChanged;
        };
        return view;
    }
    // Performs the template description action for this screen or workflow.
    private static string TemplateDescription(string template) => template switch
    {
        "Modern" => "Bold header with contemporary styling", "Minimal" => "Simple and distraction-free",
        "Executive" => "Premium business layout with structured billing blocks", "Compact" => "Space-efficient receipt layout, ideal for A6 printing",
        "Thermal" => "Narrow receipt layout for 80mm and 58mm thermal printers", "Grid Classic" => "Old-style bordered tabular bill, for A4, A5 and A6",
        _ => "Traditional layout with clean structure"
    };
    // Performs the invoice preview action for this screen or workflow.
    private static Control PdfActiveBadge() => new Border { Background = Ui.Palette("#E7E2F2", "#202B36"), CornerRadius = new CornerRadius(8), Padding = new Thickness(6.4, 2.4), HorizontalAlignment = HorizontalAlignment.Left, Child = Ui.Columns("Auto,6,Auto", Ui.Icon("check_circle", 13, Ui.Primary), new Border(), Ui.LocalText("Active", 12, color: Ui.Primary)) };

    private static Control InvoicePreview(string template, string hex, bool landscape = false)
    {
        var accent = Color.TryParse(hex, out var c) ? new SolidColorBrush(c) : Ui.Primary;
        var header = new Border { Background = Ui.Surface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 1), Padding = new Thickness(19.2, 12.8), Child = Ui.Stack(6.4, Ui.Text(template, 22, true), Ui.Text(TemplateDescription(template), 16, color: Ui.Muted), PdfActiveBadge()) };
        var sketch = new TemplateSketch(template, hex == "#002E78" ? Brushes.Black : accent) { Width = landscape ? 520 : 390, Height = landscape ? 390 : 520 };
        var paper = new Border { Background = Brushes.White, BoxShadow = new BoxShadows(new BoxShadow { Blur = 22, OffsetY = 8, Color = Color.Parse("#22000000") }), Child = sketch };
        return Ui.Rows("Auto,*", header, new Viewbox { Margin = new Thickness(48, 16), Stretch = Stretch.Uniform, Child = paper });
    }
}
