using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

public partial class MainWindow
{
    private string dashboardLayout = "Default";
    private readonly HashSet<string> dismissedBanners = [];
    private Control Dashboard()
    {
        Control Banner(string title, string subtitle, string background, string foreground)
        {
            var b = Ui.Card(Ui.Columns("*,Auto", Ui.Stack(4, Ui.Text(title, 13, true, Brush.Parse(foreground)), Ui.Text(subtitle, 12, color: Brush.Parse(foreground))), Ui.Wrap(Ui.Button("Got it", () => { dismissedBanners.Add(title); page.Content = Dashboard(); }), Ui.Button("×", () => { dismissedBanners.Add(title); page.Content = Dashboard(); }))), 12);
            b.Background = Brush.Parse(background); b.Margin = new Thickness(0, 0, 0, 20); b.IsVisible = !dismissedBanners.Contains(title); return b;
        }
        var layout = new ComboBox { ItemsSource = new[] { "Default", "Classic", "Simple Feed", "Bento" }, SelectedItem = dashboardLayout };
        layout.SelectionChanged += (_, _) => { if (layout.SelectedItem is string selected && selected != dashboardLayout) { dashboardLayout = selected; page.Content = Dashboard(); } };
        var refresh = Ui.Button("↻", () => page.Content = Dashboard()); refresh.Content = Ui.Icon("refresh", 22, Brushes.White); refresh.Classes.Add("text");
        var top = Ui.AppBar("", layout, refresh);
        var appbar = new Grid(); appbar.Children.Add(top); var heading = Ui.Text("Dashboard Overview", 20, color: Brushes.White); heading.HorizontalAlignment = HorizontalAlignment.Center; appbar.Children.Add(heading);
        var greeting = new Border { Padding = new Thickness(28, 24), CornerRadius = new CornerRadius(16), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = [new GradientStop(Color.Parse("#1E293B"), 0), new GradientStop(Color.Parse("#334155"), 1)] }, Child = Ui.Columns("*,Auto", Ui.Stack(6, Ui.Text("Welcome back, admin", 22, true, Brushes.White), Ui.Text("Here's your business at a glance", 13, color: Brush.Parse("#B8C0CB"))), Ui.Stack(2, Ui.Text(DateTime.Today.ToString("dddd"), 12, color: Brush.Parse("#B8C0CB")), Ui.Text(DateTime.Today.ToString("MMM d, yyyy"), 18, true, Brushes.White))) };
        var stats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*,16,*,16,*,16,*") };
        var values = new[] { ("Customers", Model.Customers.Count.ToString(), "people", "#1565C0"), ("Products", Model.Products.Count.ToString(), "inventory_2", "#2E7D32"), ("Invoices", Model.Invoices.Count.ToString(), "receipt_long", "#E65100"), ("Revenue Collected", "Rs. 0.00", "account_balance_wallet", "#6A1B9A"), ("Outstanding", "Rs. " + Model.Invoices.Sum(i => decimal.TryParse(i["Total"], out var n) ? n : 0).ToString("0.00"), "hourglass_top", "#C62828") };
        for (var i = 0; i < values.Length; i++)
        {
            var (label, value, icon, hex) = values[i];
            var badge = new Border { Width = 37, Height = 37, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.Parse(hex), .1), Child = Ui.Icon(icon, 19, Brush.Parse(hex)) };
            var card = new Border { Padding = new Thickness(18), CornerRadius = new CornerRadius(14), Background = Brush.Parse("#FAFAFA"), BoxShadow = BoxShadows.Parse("0 3 12 0 #0F000000"), Child = Ui.Columns("37,12,*", badge, new Border(), Ui.Stack(4, Ui.Text(label, 11, color: Ui.Muted), Ui.Text(value, 16, true))) };
            Grid.SetColumn(card, i * 2); stats.Children.Add(card);
        }
        var recent = Ui.Stack(20, Ui.Columns("*,Auto", Ui.Columns("4,12,*", new Border { Height = 24, Background = Ui.Primary, CornerRadius = new CornerRadius(2) }, new Border(), Ui.Text("Recent Invoices", 22, true)), Ui.Text("Last 5 invoices", 13, color: Ui.Muted)));
        if (Model.Invoices.Count == 0) recent.Children.Add(Ui.Empty("No invoices yet", "Create your first invoice to see it here"));
        else foreach (var inv in Model.Invoices.Reverse().Take(5)) recent.Children.Add(Ui.Card(Ui.Columns("*,Auto", Ui.Stack(5, Ui.Text(inv.Name, 15, true), Ui.Text(inv["Customer"], 13, color: Ui.Muted)), Ui.Wrap(Ui.Text("₹ " + inv["Total"], 18, true), Ui.Button("View", () => ShowPayment(inv))))));
        var body = Ui.Stack(0,
            Banner("New: Multiple dashboard layouts", "Switch between Default, Classic, Bento, and Simple Feed using the grid icon in the top-right.", "#EFF6FF", "#2563EB"),
            Banner("New: Dark mode", "We're still polishing it — switch it on from Settings > Company Info and let us know what looks off.", "#F5F3FF", "#7C3AED"),
            Banner("New: Keyboard shortcuts", "Ctrl+Q for a new invoice, Ctrl+S to save, and more.", "#ECFDF5", "#059669"), greeting, stats, recent);
        greeting.MinHeight = 104; greeting.Margin = new Thickness(0, 0, 0, 28); stats.Margin = new Thickness(0, 0, 0, 36);
        if (dashboardLayout == "Bento") body.Children.Add(Ui.Columns("*,*", Ui.Card(Ui.Stack(12, Ui.Text("Revenue", 18, true), Ui.Empty("No revenue data yet"))), Ui.Card(Ui.Stack(12, Ui.Text("Quick Actions", 18, true), Ui.Button("＋ New Invoice", () => Model.NavigateCommand.Execute("New Invoice")), Ui.Button("Customers", () => Model.NavigateCommand.Execute("Customers")), Ui.Button("Products", () => Model.NavigateCommand.Execute("Products"))))));
        if (dashboardLayout == "Simple Feed") { stats.IsVisible = false; greeting.IsVisible = false; }
        if (dashboardLayout == "Classic") greeting.IsVisible = false;
        body.MaxWidth = 1600; return Ui.Rows("Auto,*", appbar, new Border { Background = Brush.Parse("#FAFAFA"), Child = Ui.Scroll(body, 28) });
    }
    private void Shortcuts() => ShowOverlay("Keyboard Shortcuts", Ui.Stack(16,
        Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print PDF")));
    private static Control Shortcut(string key, string description) => Ui.Columns("160,*", Ui.Card(Ui.Text(key, 13, true), 10), Ui.Text(description));
}
