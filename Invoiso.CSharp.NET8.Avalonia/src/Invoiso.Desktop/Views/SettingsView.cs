using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

public partial class MainWindow
{
    private string settingsTab = "Company Info";
    private Control SettingsView()
    {
        string[] tabs = ["Company Info", "Backup", "Users", "PDF Settings", "Invoice Settings", "Product Details", "Customize", "Accessibility", "Software Info"];
        string[] icons = ["business", "backup", "people", "settings", "receipt_long", "view_column", "tune", "accessibility_new", "info_outline"];
        var rail = Ui.Stack(4);
        var content = new ContentControl();
        var buttons = new List<Button>();
        void Select(string name)
        {
            settingsTab = name;
            foreach (var b in buttons) b.Classes.Set("selected", (string?)b.Tag == name);
            content.Content = name switch {
                "Company Info" => CompanySettingsView(), "PDF Settings" => PdfSettingsView(),
                "Users" => new ManagementView(Model, "User", this), "Backup" => BackupView(), "Customize" => CustomizationView(), "Software Info" => SoftwareInfo(),
                _ => SettingsForm(name)
            };
        }
        for (var i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i]; var button = Ui.Button("", () => Select(tab));
            button.Content = Ui.Stack(4, Ui.Icon(icons[i], 24), Ui.Text(tab, 11));
            foreach (var child in ((StackPanel)button.Content).Children) child.HorizontalAlignment = HorizontalAlignment.Center;
            button.Tag = tab; button.Classes.Clear(); button.Classes.Add("nav"); button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.Margin = new Thickness(4, 0); button.Padding = new Thickness(8); button.MinHeight = 72; buttons.Add(button); rail.Children.Add(button);
        }
        Select(settingsTab);
        return Ui.Columns("110,*", new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0), Child = Ui.Scroll(rail, 0) }, content);
    }
    private Control SettingsForm(string name)
    {
        if (name == "Invoice Settings") return InvoiceSettingsView();
        if (!Model.Settings.TryGetValue(name, out var sections)) return Ui.Empty("No settings available");
        var stack = Ui.Stack(16);
        foreach (var section in sections) stack.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text(section.Title, 18, true), Ui.Fields(section.Fields)), 24));
        if (name == "Accessibility") stack.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text("Keyboard Shortcuts", 20, true), Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print PDF"))));
        stack.Children.Add(Ui.Button("Save Settings", () => Model.Status = "Settings saved for this session.", true));
        stack.MaxWidth = 900; return Ui.Rows("Auto,*", Ui.AppBar(name), Ui.Scroll(stack, 28));
    }
    private Control InvoiceSettingsView()
    {
        var sections = Model.Settings["Invoice Settings"];
        string[] order = ["General", "Branding", "Tax", "Items", "Customer", "Columns", "Custom Fields"];
        string[] labels = ["General", "Branding", "Tax & GST", "Invoice Items", "Customer Details", "Invoice Columns", "Custom Fields"];
        var content = new ContentControl(); var buttons = new List<Button>(); var nav = Ui.Stack(4);
        void Select(int index)
        {
            foreach (var button in buttons) button.Classes.Set("selected", (int?)button.Tag == index);
            var section = sections.First(s => s.Title == order[index]);
            Control fields;
            if (index == 0)
            {
                FormField F(string label) => section.Fields.First(f => f.Label == label);
                fields = Ui.Stack(16, Ui.Fields([F("Invoice Prefix"), F("Starting Number")], 2), Ui.Fields([F("Leading Zeros"), F("Currency")], 2), Ui.Fields([F("Date Format"), F("Time Format")], 2), Ui.Fields([F("Show time in PDF"), F("Quantity Column")], 2), Ui.Field(F("Additional Information")), Ui.Field(F("Thank You Note")), Ui.Field(F("Hide Invoice Number")));
            }
            else fields = Ui.Fields(section.Fields);
            var card = Ui.Card(Ui.Stack(32, Ui.Columns("4,12,*", new Border { Height = 24, Background = Ui.Primary, CornerRadius = new CornerRadius(2) }, new Border(), Ui.Text(labels[index], 20, true)), fields), 32);
            card.Background = Brush.Parse("#FAFAFA"); card.MaxWidth = 900; card.CornerRadius = new CornerRadius(16); card.BoxShadow = BoxShadows.Parse("0 3 8 0 #18000000");
            content.Content = Ui.Scroll(card, 28);
        }
        for (var i = 0; i < order.Length; i++)
        {
            var index = i; var button = Ui.Button(labels[i], () => Select(index)); button.Tag = index; button.Classes.Clear(); button.Classes.Add("nav"); buttons.Add(button); nav.Children.Add(button);
        }
        var promo = Ui.Card(Ui.Stack(12, Ui.Text("Need more fields on your invoices?", 14, true, Ui.Primary), Ui.Text("Add PO number, project code, department, or any custom field.", 12, color: Ui.Muted), Ui.Button("See Options", () => { settingsTab = "Customize"; page.Content = SettingsView(); })), 14);
        var save = Ui.Button("Save", () => { if (sections.SelectMany(s => s.Fields).Select(f => f.Validate()).ToArray().All(v => v)) Model.Status = "Invoice settings saved for this session."; }, true); save.HorizontalAlignment = HorizontalAlignment.Stretch;
        var rail = new Border { Background = Brush.Parse("#FAFAFA"), Child = Ui.Rows("*,Auto,Auto", Ui.Scroll(nav, 12), new Border { Padding = new Thickness(16), Child = promo }, new Border { Padding = new Thickness(16, 0, 16, 16), Child = save }) };
        var layout = Ui.Columns("240,*", rail, content);
        layout.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < 900;
            layout.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "240,*"); layout.RowDefinitions = new RowDefinitions(narrow ? "Auto,*" : "*");
            Grid.SetColumn(content, narrow ? 0 : 1); Grid.SetRow(content, narrow ? 1 : 0);
            rail.Height = narrow ? 140 : double.NaN; promo.IsVisible = !narrow;
        };
        Select(0); return Ui.Rows("Auto,*", Ui.AppBar("Invoice Settings"), layout);
    }
    private Control BackupView() => Ui.Rows("Auto,*", Ui.AppBar("Backup Management"), Ui.Scroll(Ui.Stack(20, Ui.Wrap(Ui.Button("＋ Create Backup"), Ui.Button("↑ Restore from File")), Ui.Empty("No backups found", "Create a backup to protect your data")), 28));
    private Control CustomizationView()
    {
        var cards = Ui.Stack(20, Ui.Text("MADE FOR YOUR BUSINESS", 12, true, Ui.Primary), Ui.Text("Customize Invoiso", 28, true));
        foreach (var (title, description) in new[] { ("Custom PDF Template", "An invoice design tailored to your business and branding."), ("Custom Fields", "Capture the additional details your business needs."), ("White Label", "Your brand, logo and identity throughout the application."), ("Industry Build", "A tailored workflow for your industry.") }) cards.Children.Add(Ui.Card(Ui.Stack(12, Ui.Text(title, 20, true), Ui.Text(description, 14, color: Ui.Muted), Ui.Button("Request Customization")), 24));
        cards.MaxWidth = 900; return Ui.Scroll(cards, 28);
    }
    private Control SoftwareInfo() => Ui.Rows("Auto,*", Ui.AppBar("Software Information"), Ui.Scroll(Ui.Stack(24, Ui.Logo(), Ui.Card(Ui.Stack(18, Ui.Text("App Details", 18, true), Ui.Text("App Name       Invoiso"), Ui.Text("Platform          Desktop"), Ui.Text("License           See legacy LICENSE"))), Ui.Card(Ui.Stack(18, Ui.Text("Developer", 18, true), Ui.Text("Website          invoiso.co.in"), Ui.Button("Check for Updates"))), Ui.Button("Change Password", ShowChangePassword), Ui.Button("First-time Setup", ShowOnboarding)), 28));
}
