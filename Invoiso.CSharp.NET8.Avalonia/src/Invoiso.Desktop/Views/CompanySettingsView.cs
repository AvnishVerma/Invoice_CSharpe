using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

public partial class MainWindow
{
    private Control CompanySettingsView()
    {
        var sections = Model.Settings["Company Info"];
        var fields = sections[1].Fields;
        FormField F(string label) => fields.First(f => f.Label == label);
        var previewName = Ui.Text(F("Company Name").Value, 18, true);
        previewName.Bind(TextBlock.TextProperty, new Binding(nameof(FormField.Value)) { Source = F("Company Name") });
        var logo = Ui.Button("", () => { }); logo.Width = 180; logo.Height = 180;
        logo.Content = Ui.Stack(12, Ui.Icon("upload", 40, Ui.Primary), Ui.Text("Upload Logo", 14, color: Ui.Muted), Ui.Text("Click to browse", 12, color: Ui.Muted));
        logo.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Company Logo", FileTypeFilter = [Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll] });
            if (files.Count == 0) return;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                if (stream.Length > 2 * 1024 * 1024) { Model.Status = "Logo must be 2 MB or smaller."; return; }
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                if (bitmap.PixelSize.Width > 1080 || bitmap.PixelSize.Height > 1080) { bitmap.Dispose(); Model.Status = "Logo must be at most 1080 × 1080 pixels."; return; }
                logo.Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }; sections[0].Fields[0].Value = files[0].Path.LocalPath;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException) { Model.Status = "The selected image could not be opened."; }
        };
        var save = Ui.Button("Save", () => { if (fields.Select(f => f.Validate()).ToArray().All(v => v)) Model.Status = "Company information saved for this session."; }, true); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        var logoPanel = new Border { Background = Brush.Parse("#FAFAFA"), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0), Child = Ui.Rows("*,Auto", Ui.Scroll(Ui.Stack(16, Ui.Text("COMPANY LOGO", 12, true, Ui.Muted), logo, Ui.Field(sections[0].Fields[1]), previewName, Ui.Text("Max 1080×1080 px · 2 MB\nPNG or JPG only", 12, color: Ui.Muted)), 24), new Border { Padding = new Thickness(16), Child = save }) };
        var details = Ui.Stack(16, Ui.Text("COMPANY DETAILS", 12, true, Ui.Muted), Ui.Fields([F("Company Name"), F("GSTIN")], 2), Ui.Fields([F("PAN"), F("FSSAI Code")], 2), Ui.Fields([F("Country"), F("Phone"), F("Email")], 3), Ui.Field(F("Website")), Ui.Field(F("Address")), new Border { Height = 8 }, Ui.Text("BUSINESS TYPE", 12, true, Ui.Muted), Ui.Card(Ui.Stack(8, Ui.Text("Business Type", 16), Ui.Text("Controls item type options in the product list and invoices", 12, color: Ui.Muted), Ui.Field(sections[2].Fields[0])), 16), new Border { Height = 8 }, Ui.Text("PAYMENT SETTINGS", 12, true, Ui.Muted));
        foreach (var field in sections[3].Fields) details.Children.Add(Ui.Card(Ui.Field(field)));
        foreach (var section in sections.Skip(4))
        {
            var accounts = Ui.Stack(12, Ui.Fields(section.Fields, 2));
            details.Children.Add(Ui.Text(section.Title, 12, true, Ui.Muted)); details.Children.Add(accounts);
            details.Children.Add(Ui.Button("＋ Add Account", () => accounts.Children.Add(Ui.Card(Ui.Fields(section.Fields.Select(f => new FormField(f.Label, kind: f.Kind)).ToArray(), 2)))));
        }
        var layout = Ui.Columns("240,*", logoPanel, Ui.Scroll(details, 32));
        var language = new ComboBox { ItemsSource = new[] { "English", "हिन्दी", "नेपाली", "བོད་ཡིག", "Español", "Français", "中文" }, SelectedIndex = 0, Width = 115, IsEnabled = false };
        ToolTip.SetTip(language, "Localized desktop strings have not yet been migrated.");
        var theme = new ComboBox { ItemsSource = new[] { "Light", "Dark", "System" }, SelectedIndex = 0, Width = 100, IsEnabled = false };
        ToolTip.SetTip(theme, "Dark-theme visual parity has not yet been verified.");
        return Ui.Rows("Auto,*", Ui.AppBar("Company Information", language, theme), layout);
    }
    private Control PdfSettingsView()
    {
        var sections = Model.Settings["PDF Settings"];
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
            foreach (var hex in new[] { "#002E78", "#1565C0", "#2E7D32", "#6A1B9A", "#C62828", "#E65100", "#263238" })
            { var b = Ui.Button("", () => { color.Value = hex; Display(); }); b.Background = Brush.Parse(hex); b.Width = 30; b.Height = 30; b.MinHeight = 30; b.CornerRadius = new CornerRadius(15); swatches.Children.Add(b); }
            controls.Children.Add(Ui.Card(Ui.Stack(12, swatches, Ui.Field(color), Ui.Button("Template default", () => { color.Value = "#002E78"; Display(); })), 12));
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
        var layout = Ui.Columns("260,12,320,12,*", templates, new Border(), settings, new Border(), previewCard); layout.Margin = new Thickness(12);
        var host = new ContentControl { Content = layout }; bool? previousNarrow = null;
        host.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 900; if (previousNarrow == narrow) return; previousNarrow = narrow;
            host.Content = null; layout.Children.Clear();
            if (narrow)
            { templates.Height = 400; settings.Height = 520; previewCard.Height = 520; host.Content = Ui.Scroll(Ui.Stack(12, templates, settings, previewCard), 12); }
            else
            {
                foreach (var item in new[] { templates, settings, previewCard }) { if (item.Parent is Panel p) p.Children.Remove(item); item.Height = double.NaN; }
                Grid.SetColumn(templates, 0); Grid.SetColumn(settings, 2); Grid.SetColumn(previewCard, 4); layout.Children.Add(templates); layout.Children.Add(settings); layout.Children.Add(previewCard); host.Content = layout;
            }
        };
        pageSize.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(FormField.Value)) return;
            selectedTemplate.Value = pageSize.Value switch { "A5" => "Grid Classic", "A6" => "Compact", "Thermal 80mm" or "Thermal 58mm" => "Thermal", _ => "Classic" }; Display();
        };
        Display();
        var header = new Border { Background = Brush.Parse("#FAFAFA"), Padding = new Thickness(16, 12), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Header("PDF Settings", "Customize invoice, quotation and receipt PDF templates", Ui.Button("Reset to Default", () => { pageSize.Value = "A4"; selectedTemplate.Value = "Classic"; color.Value = "#002E78"; Display(); }), Ui.Button("Save Settings", () => Model.Status = "PDF settings saved for this session.", true)) };
        return Ui.Rows("Auto,*", header, host);
    }
    private static string TemplateDescription(string template) => template switch
    {
        "Modern" => "Bold header with contemporary styling", "Minimal" => "Simple and distraction-free",
        "Executive" => "Premium business layout with structured billing blocks", "Compact" => "Space-efficient receipt layout, ideal for A6 printing",
        "Thermal" => "Narrow receipt layout for 80mm and 58mm thermal printers", "Grid Classic" => "Old-style bordered tabular bill, for A4, A5 and A6",
        _ => "Traditional layout with clean structure"
    };
    private static Control InvoicePreview(string template, string hex)
    {
        var accent = Color.TryParse(hex, out var c) ? new SolidColorBrush(c) : Ui.Primary;
        var header = new Border { Background = Brush.Parse("#FAFAFA"), Padding = new Thickness(24, 16), Child = Ui.Stack(4, Ui.Text(template, 22, true), Ui.Text(TemplateDescription(template), 16, color: Ui.Muted)) };
        var sketch = new TemplateSketch(template, accent) { Width = 390, Height = 520 };
        return Ui.Rows("Auto,*", header, new Viewbox { Margin = new Thickness(16, 32), Stretch = Stretch.Uniform, Child = sketch });
    }
}
