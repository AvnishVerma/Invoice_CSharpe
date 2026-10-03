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
        logo.Content = Ui.Stack(9.6, Ui.Icon("upload", 40, Ui.Primary), Ui.Text("Upload Logo", 14, color: Ui.Muted), Ui.Text("Click to browse", 12, color: Ui.Muted));
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
        var companyFields = Ui.Stack(12.8,
            Ui.Fields([F("Company Name"), F("GSTIN")], 2),
            Ui.Fields([F("PAN"), F("FSSAI Code")], 2),
            Ui.Fields([F("Country"), F("Phone"), F("Email")], 3),
            Ui.Field(F("Website")),
            Ui.Field(F("Address")));

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
        var addUpi = Ui.Button("＋ Add UPI Account", Model.AddUpiAccount);
        addUpi.Classes.Add("text");
        addUpi.HorizontalAlignment = HorizontalAlignment.Left;
        var addBank = Ui.Button("＋ Add Bank Account", Model.AddBankAccount);
        addBank.Classes.Add("text");
        addBank.HorizontalAlignment = HorizontalAlignment.Left;
        var details = new CompanyDetailsFormView(companyFields, businessCard, qrCard, upiRows, addUpi, bankCard, bankRows, addBank);
        details.AttachedToVisualTree += (_, _) => { Model.UpiAccounts.CollectionChanged += upiChanged; Model.BankAccounts.CollectionChanged += bankChanged; };
        details.DetachedFromVisualTree += (_, _) => { Model.UpiAccounts.CollectionChanged -= upiChanged; Model.BankAccounts.CollectionChanged -= bankChanged; };
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
            button.Content = Ui.Columns("Auto,7,*", Ui.Icon(iconName, 17, Ui.TextColor), new Border(), Ui.Text(label, 14));
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
    // Loads the AXAML-defined PDF settings screen with its binding model.
    private Control PdfSettingsView() => new PdfSettingsView { DataContext = new PdfSettingsViewModel(Model, printService) };
}
