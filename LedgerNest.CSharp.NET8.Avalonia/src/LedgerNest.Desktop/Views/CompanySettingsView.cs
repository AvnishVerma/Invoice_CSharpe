using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;
using Avalonia.Styling;

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
        logo.Content = Ui.Stack(12, Ui.Icon("upload", 40, Ui.Primary), Ui.Text("Upload Logo", 14, color: Ui.Muted), Ui.Text("Click to browse", 12, color: Ui.Muted));
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
            Ui.Stack(8,
                Ui.Text("Business Type", 16),
                Ui.Text("Controls item type options in the product list and invoices", 12, color: Ui.Muted),
                CompanyBusinessTypeSegments(businessType))), 16);
        var qrCard = Ui.Card(Ui.Columns("Auto,16,*",
            Ui.Icon("credit_card", 25, Ui.Muted),
            new Border(),
            Ui.Field(sections[3].Fields[0], "Show QR Code on Invoices")), 16);
        var bankCard = Ui.Card(Ui.Columns("Auto,16,*",
            Ui.Icon("account_balance", 25, Ui.Muted),
            new Border(),
            Ui.Field(sections[3].Fields[1], "Show Bank Details on Invoices")), 16);
        var details = Ui.Stack(16,
            Ui.Text("COMPANY DETAILS", 12, true, Ui.Muted),
            Ui.Fields([F("Company Name"), F("GSTIN")], 2),
            Ui.Fields([F("PAN"), F("FSSAI Code")], 2),
            Ui.Fields([F("Country"), F("Phone"), F("Email")], 3),
            Ui.Field(F("Website")),
            Ui.Field(F("Address")),
            new Border { Height = 8 },
            Ui.Text("BUSINESS TYPE", 12, true, Ui.Muted),
            businessCard,
            new Border { Height = 8 },
            Ui.Text("PAYMENT SETTINGS", 12, true, Ui.Muted),
            qrCard,
            Ui.Text("UPI ACCOUNTS", 12, true, Ui.Muted));

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
        details.Children.Add(Ui.Text("BANK ACCOUNTS", 12, true, Ui.Muted));
        details.Children.Add(bankRows);
        var addBank = Ui.Button("＋ Add Bank Account", Model.AddBankAccount);
        addBank.Classes.Add("text");
        addBank.HorizontalAlignment = HorizontalAlignment.Left;
        details.Children.Add(addBank);
        var language = new ComboBox { ItemsSource = new[] { "English", "हिन्दी", "नेपाली", "བོད་ཡིག", "Español", "Français", "中文" }, SelectedItem = Model.Language, Width = 115 };
        language.SelectionChanged += (_, _) => Model.SetLanguage(language.SelectedItem?.ToString() ?? "English");
        ToolTip.SetTip(language, "Stores the preferred language; full translated desktop strings are still being migrated.");
        var theme = new ComboBox { ItemsSource = new[] { "Light", "Dark", "System" }, SelectedItem = Model.ThemeMode, Width = 100 };
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
                button.Background = selected ? Brush.Parse("#E8DEF8") : Brushes.Transparent;
                button.Foreground = selected ? Brush.Parse("#4F4268") : Brushes.Black;
            }
        }
        foreach (var (label, iconName) in new[] { ("Product", "inventory_2"), ("Service", "build"), ("Both", "check") })
        {
            var button = Ui.Button(label, () => { field.Value = label; UpdateSelection(); });
            button.Tag = label;
            button.Padding = new Thickness(12, 6);
            button.MinHeight = 32;
            button.CornerRadius = new CornerRadius(0);
            button.Content = Ui.Columns("Auto,7,*", Ui.Icon(iconName, 17, Brushes.Black), new Border(), Ui.Text(label, 14));
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
        if (global::Avalonia.Application.Current != null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = mode switch
            {
                "Dark" => ThemeVariant.Dark,
                "System" => ThemeVariant.Default,
                _ => ThemeVariant.Light
            };
        }
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
                b.BorderBrush = template == selectedTemplate.Value ? Ui.Primary : Ui.Outline; b.BorderThickness = new Thickness(template == selectedTemplate.Value ? 2 : 1); b.Background = template == selectedTemplate.Value ? Brush.Parse("#ECE8F3") : Brush.Parse("#FAFAFA");
            }
            var controls = Ui.Stack(16, Ui.Columns("*,Auto", Ui.Text(selectedTemplate.Value, 18, true), Ui.Text("Active", 11, true, Ui.Primary)), Ui.Text(TemplateDescription(selectedTemplate.Value), 12.5, color: Ui.Muted));
            if (selectedTemplate.Value is "Compact" or "Grid Classic" or "Thermal") { controls.Children.Add(Ui.Text("DISPLAY OPTIONS", 12, true, Ui.Muted)); controls.Children.Add(Ui.Fields(sections[2].Fields)); }
            controls.Children.Add(Ui.Text("THEME COLOR", 12, true, Ui.Muted));
            var swatches = Ui.Wrap();
            foreach (var hex in new[] { "#002E78", "#2563EB", "#047857", "#7C2D12", "#6D28D9" })
            { var b = Ui.Button("", () => { color.Value = hex; Display(); }); b.Background = Brush.Parse(hex); b.Width = 30; b.Height = 30; b.MinHeight = 30; b.CornerRadius = new CornerRadius(15); swatches.Children.Add(b); }
            controls.Children.Add(Ui.Card(Ui.Stack(12, swatches, Ui.Field(color), Ui.Button("Template default", () => { color.Value = "#002E78"; Display(); })), 12));
            controls.Children.Add(Ui.Text("PRINTING", 12, true, Ui.Muted));
            controls.Children.Add(Ui.Card(Ui.Fields(sections.Single(section => section.Title == "PRINTING").Fields), 12));
            controls.Children.Add(Ui.Card(Ui.Stack(12, Ui.Text("Need a custom template?", 14, true), Ui.Text("Make your invoice look exactly the way you want.", 12, color: Ui.Muted), Ui.Button("Explore Customization", () => { settingsTab = "Customize"; page.Content = SettingsView(); })), 14));
            options.Content = Ui.Scroll(controls, 16);
            preview.Content = InvoicePreview(selectedTemplate.Value, color.Value);
        }
        foreach (var template in selectedTemplate.Options)
        {
            var button = Ui.Button(template, () => { selectedTemplate.Value = template; Display(); }); button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Content = Ui.Columns("76,*", new TemplateSketch(template, Brush.Parse("#1A237E"), false) { Width = 64, Height = 80 }, Ui.Stack(4, Ui.Text(template, 14, true), Ui.Text(TemplateDescription(template), 12, color: Ui.Muted)));
            buttons.Add(button); templateList.Children.Add(button);
        }
        var templates = Ui.Card(Ui.Rows("Auto,Auto,*", Ui.Stack(8, Ui.Text("PAGE SIZE", 12, true, Ui.Muted), Ui.Field(pageSize)), new Border { Padding = new Thickness(0, 16, 0, 10), Child = Ui.Text("TEMPLATES", 12, true, Ui.Muted) }, Ui.Scroll(templateList, 0)), 12);
        var settings = Ui.Card(options, 0); var previewCard = Ui.Card(Ui.Rows("Auto,*,Auto", new Border { Padding = new Thickness(16, 10), Child = Ui.Text("Preview", 13, true) }, preview, new Border { Padding = new Thickness(16, 10), Child = Ui.Text("Preview may slightly differ in the final PDF.", 12, color: Ui.Muted) }), 0);
        System.ComponentModel.PropertyChangedEventHandler pageSizeChanged = (_, e) =>
        {
            if (e.PropertyName != nameof(FormField.Value)) return;
            selectedTemplate.Value = pageSize.Value switch { "A5" => "Grid Classic", "A6" => "Compact", "Thermal 80mm" or "Thermal 58mm" => "Thermal", _ => "Classic" }; Display();
        };
        Display();
        var showPrinterDialog = sections.Single(section => section.Title == "PRINTING").Fields.Single(field => field.Label == "Show Printer Selection Dialog");
        var header = Ui.Header("PDF Settings", "Customize invoice, quotation and receipt PDF templates", Ui.Button("Reset to Default", () => { pageSize.Value = "A4"; selectedTemplate.Value = "Classic"; color.Value = "#002E78"; printer.Value = PlatformPrinterService.DefaultPrinter; showPrinterDialog.IsChecked = false; Display(); }), Ui.Button("Save Settings", () => Model.SaveSettings("PDF Settings"), true));
        var view = new PdfSettingsShellView(header, templates, settings, previewCard);
        view.AttachedToVisualTree += async (_, _) =>
        {
            pageSize.PropertyChanged += pageSizeChanged;
            try
            {
                var discovered = await printService.GetPrintersAsync();
                printer.Options = new[] { PlatformPrinterService.DefaultPrinter }
                    .Concat(discovered.Select(item => item.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!printer.Options.Contains(printer.Value, StringComparer.OrdinalIgnoreCase))
                    printer.Value = discovered.FirstOrDefault(item => item.IsDefault)?.Name ?? printer.Options[0];
                Display();
            }
            catch (Exception ex)
            {
                AppErrorLog.Write(ex, "Refreshing printer choices");
                Model.Status = "Printers could not be refreshed. Check the printing service configuration.";
            }
        };
        view.DetachedFromVisualTree += (_, _) => pageSize.PropertyChanged -= pageSizeChanged;
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
    private static Control InvoicePreview(string template, string hex)
    {
        var accent = Color.TryParse(hex, out var c) ? new SolidColorBrush(c) : Ui.Primary;
        var header = new Border { Background = Ui.Surface, Padding = new Thickness(24, 16), Child = Ui.Stack(4, Ui.Text(template, 22, true), Ui.Text(TemplateDescription(template), 16, color: Ui.Muted)) };
        var sketch = new TemplateSketch(template, accent) { Width = 390, Height = 520 };
        return Ui.Rows("Auto,*", header, new Viewbox { Margin = new Thickness(16, 32), Stretch = Stretch.Uniform, Child = sketch });
    }
}
