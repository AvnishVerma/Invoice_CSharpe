using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private string dashboardLayout = "Default";
    private readonly HashSet<string> dismissedBanners = [];
    private Control Dashboard()
    {
        var invoices = Model.ActiveInvoices.OrderByDescending(i => i["Date"]).ThenByDescending(i => i.SourceId).ToArray();
        var paid = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Paid"], out var n) ? n : 0);
        var outstanding = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Outstanding"], out var n) ? n : decimal.TryParse(i["Total"], out var total) ? total : 0);
        var outOfStock = Model.Products.Where(p => decimal.TryParse(p["Stock"], out var stock) && stock <= 0).ToArray();
        var layout = Ui.Button("", () => { });
        layout.Content = Ui.Icon("dashboard", 20, Brushes.White);
        layout.Classes.Add("text");
        ToolTip.SetTip(layout, "Dashboard layout");
        var layoutMenu = new MenuFlyout();
        foreach (var option in new[] { ("Default", "Original layout"), ("Classic", "Charts + KPI grid"), ("Bento", "Hero chart + card grid"), ("Simple Feed", "Clean list view") })
        {
            var name = option.Item1;
            var item = new MenuItem { Tag = name, Header = Ui.Columns("22,*,24", Ui.Icon(name == "Default" ? "view_column" : name == "Classic" ? "dashboard" : name == "Bento" ? "view_column" : "receipt_long", 18, name == dashboardLayout ? Ui.Primary : Ui.Muted), Ui.Stack(2, Ui.Text(name, 14, name == dashboardLayout, name == dashboardLayout ? Ui.Primary : Ui.TextColor), Ui.Text(option.Item2, 12, color: Ui.Muted)), dashboardLayout == name ? Ui.Icon("check_circle", 16, Ui.Primary) : new Border()) };
            item.Click += (_, _) => { dashboardLayout = name; dismissedBanners.Add("New: Multiple dashboard layouts"); page.Content = Dashboard(); };
            layoutMenu.Items.Add(item);
        }
        layout.Flyout = layoutMenu;
        var refresh = Ui.Button("↻", () => page.Content = Dashboard()); refresh.Content = Ui.Icon("refresh", 22, Brushes.White); refresh.Classes.Add("text");
        var top = Ui.AppBar("", layout, refresh);
        var appbar = new Grid(); appbar.Children.Add(top); var heading = Ui.Text("Dashboard Overview", 20, color: Brushes.White); heading.IsHitTestVisible = false; heading.HorizontalAlignment = HorizontalAlignment.Center; appbar.Children.Add(heading);
        var greeting = new Border { Padding = new Thickness(28, 24), MinHeight = 104, CornerRadius = new CornerRadius(16), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = [new GradientStop(Color.Parse("#1E293B"), 0), new GradientStop(Color.Parse("#334155"), 1)] }, Child = Ui.Stack(6, Ui.Text("Welcome back, admin", 22, true, Brushes.White), Ui.Text("Here's your business at a glance", 13, color: Brush.Parse("#B8C0CB"))) };
        (string Label, string Value, string Icon, string Color)[] kpis =
        [
            ("Revenue", "Rs. " + paid.ToString("0.00"), "account_balance_wallet", "#8A2BE2"),
            ("Outstanding", "Rs. " + outstanding.ToString("0.0K"), "hourglass_top", "#D32F2F"),
            ("Invoices", invoices.Length.ToString(), "receipt_long", "#F97316"),
            ("Customers", Model.Customers.Count.ToString(), "people", "#1976D2"),
            ("Products", Model.Products.Count.ToString(), "inventory_2", "#2E7D32")
        ];
        Control KpiCard((string Label, string Value, string Icon, string Color) item, string? alert = null)
        {
            var badge = new Border { Width = 37, Height = 37, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.Parse(item.Color), .12), Child = Ui.Icon(item.Icon, 19, Brush.Parse(item.Color)) };
            Control header = alert == null ? Ui.Text(item.Label, 11, color: Ui.Muted) : Ui.Columns("*,Auto", Ui.Text(item.Label, 11, color: Ui.Muted), Ui.Text(alert, 11, color: Brushes.Red));
            return new Border { Padding = new Thickness(20), MinHeight = 78, CornerRadius = new CornerRadius(12), Background = Ui.CardSurface, BoxShadow = BoxShadows.Parse("0 8 22 0 #10000000"), Child = Ui.Columns("40,14,*", badge, new Border(), Ui.Stack(5, header, Ui.Text(item.Value, 16, true))) };
        }
        Control KpiGrid(IEnumerable<(string Label, string Value, string Icon, string Color)> items)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*,12,*,12,*") };
            var array = items.ToArray();
            for (var i = 0; i < array.Length; i++) { var card = KpiCard(array[i], array[i].Label == "Products" && outOfStock.Length > 0 ? $"{outOfStock.Length} out of stock" : null); Grid.SetColumn(card, i * 2); grid.Children.Add(card); }
            return grid;
        }
        Control ChartCard(double minHeight = 255) => Ui.Card(Ui.Rows("Auto,*", Ui.Text("Revenue — Last 6 Months", 13, true), new Grid { MinHeight = minHeight, Children = { new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8, Children = { Ui.Icon("bar_chart", 42, Brush.Parse("#D9D9D9")), Ui.Text("No payment data yet", 13, color: Ui.Muted) } } } }), 20);
        Control DonutCard() => Ui.Card(Ui.Stack(18, Ui.Text("Financial Overview", 13, true), new Ellipse { Width = 168, Height = 168, StrokeThickness = 38, Stroke = Brushes.Firebrick, Fill = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center }, Ui.Columns("*,Auto", Ui.Stack(10, Ui.Text("Collected", 12, color: Ui.Muted), Ui.Text("Outstanding", 12, color: Ui.Muted)), Ui.Stack(10, Ui.Text("Rs. " + paid.ToString("0.00"), 12, true), Ui.Text("Rs. " + outstanding.ToString("0.0K"), 12, true)))), 20);
        Control QuickActions() => Ui.Card(Ui.Stack(14, Ui.Text("Quick Actions", 13, true), ActionRow("add", "New Invoice", () => Model.NavigateCommand.Execute("New Invoice"), "#1565C0"), ActionRow("people", "Customers", () => Model.NavigateCommand.Execute("Customers"), "#1976D2"), ActionRow("bar_chart", "Reports", () => Model.NavigateCommand.Execute("Reports"), "#2E7D32")), 18);
        Control ActionRow(string icon, string title, Action action, string color)
        {
            var button = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, Command = new CommunityToolkit.Mvvm.Input.RelayCommand(action), Content = Ui.Columns("34,12,*,Auto", new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Color.Parse(color), .1), Child = Ui.Icon(icon, 17, Brush.Parse(color)) }, new Border(), Ui.Text(title, 13, true), Ui.Icon("chevron_right", 18, Ui.Muted)) };
            return button;
        }
        Control OutOfStockCard(bool wide = false)
        {
            var count = outOfStock.Length;
            Control rows = count == 0 ? Ui.Text("All products are in stock", 13, color: Ui.Muted) : Ui.Stack(12, outOfStock.Take(wide ? 3 : 1).Select(p => Ui.Columns("34,*,Auto,12,Auto", new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(7), Background = Brush.Parse("#FFE4E6"), Child = Ui.Icon("inventory_2", 17, Brushes.Firebrick) }, Ui.Stack(2, Ui.Text(p.Name, 13, true), Ui.Text(p["Type"].Length == 0 ? "Product" : p["Type"], 12, color: Ui.Muted)), Ui.Text("0 left", 12, color: Brushes.Red), new Border(), Ui.Button("＋ Stock", () => Model.NavigateCommand.Execute("Products")))).ToArray());
            var card = Ui.Card(Ui.Stack(14, Ui.Columns("*,Auto", Ui.Columns("28,8,*", Ui.Icon("inventory_2", 19, Brushes.Firebrick), new Border(), Ui.Text("Out of Stock", 13, true)), new Border { Padding = new Thickness(8, 3), CornerRadius = new CornerRadius(12), Background = Brush.Parse("#FFE4E6"), Child = Ui.Text(count.ToString(), 11, color: Brushes.Red) }), rows), 18);
            card.BorderBrush = Brush.Parse("#FFCDD2"); card.Background = new SolidColorBrush(Color.Parse("#FFF7F7"), .8);
            return card;
        }
        Control RecentTable(int take)
        {
            var stack = Ui.Stack(12, Ui.Columns("*,Auto", Ui.Text("Recent Invoices", 13, true), Ui.Text("Last " + take, 12, color: Ui.Muted)), Ui.Columns("*,*,Auto", Ui.Text("Invoice", 12, color: Ui.Muted), Ui.Text("Customer", 12, color: Ui.Muted), Ui.Text("Amount", 12, color: Ui.Muted)));
            if (invoices.Length == 0) stack.Children.Add(Ui.Empty("No invoices yet", "Create your first invoice to see it here"));
            foreach (var inv in invoices.Take(take)) stack.Children.Add(Ui.Columns("*,*,Auto", Ui.Text("#" + inv.Name, 12), Ui.Text(inv["Customer"], 12, color: Ui.Muted), Ui.Wrap(Ui.Text("Rs. " + inv["Total"], 12), Ui.Text(inv["Status"], 11, color: Ui.Muted), Ui.Button("✎", () => Model.LoadDocumentForEditing(inv)), Ui.Button("↓", () => { }))));
            return Ui.Card(stack, 18);
        }
        Control RecentFeed()
        {
            var stack = Ui.Stack(22, Ui.Columns("*,Auto", Ui.Columns("4,12,*", new Border { Height = 30, Background = Ui.Primary, CornerRadius = new CornerRadius(3) }, new Border(), Ui.Text("Recent Invoices", 22, true)), Ui.Text("Last 5 invoices", 13, color: Ui.Muted)));
            var index = 1;
            foreach (var inv in invoices.Take(5))
            {
                var row = new Border { Padding = new Thickness(20), CornerRadius = new CornerRadius(10), Background = Brush.Parse("#FBF5FF"), Child = Ui.Columns("52,16,*,Auto", new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(10), Background = Ui.Primary, Child = Ui.Text((index++).ToString(), 20, true, Brushes.White) }, new Border(), Ui.Stack(8, Ui.Wrap(Ui.Text("Invoice #" + inv.Name, 20, true), Ui.Text("Invoice", 12, color: Ui.Primary), Ui.Text(inv["Status"], 12, color: Brushes.Red)), Ui.Text(inv["Customer"], 14, color: Ui.Muted), Ui.Text(inv["Date"], 14, color: Ui.Muted)), Ui.Stack(12, new Border { Padding = new Thickness(20, 12), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#F3DDFB"), Child = Ui.Text("Rs. " + inv["Total"], 20, true, Brush.Parse("#A21CAF")) }, Ui.Wrap(Ui.Button("View", () => ShowPayment(inv)), Ui.Button("✎", () => Model.LoadDocumentForEditing(inv)), Ui.Button("↓", () => { }), Ui.Button("🖨", () => { }), Ui.Button("Delete", () => { })))) };
                stack.Children.Add(row);
            }
            if (invoices.Length == 0) stack.Children.Add(Ui.Empty("No invoices yet", "Create your first invoice to see it here"));
            return stack;
        }
        Control TopList(string title, IEnumerable<UiRecord> records, string metric)
        {
            var rows = records.Take(3).Select(r => Ui.Columns("28,*,Auto", new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(7), Background = Brush.Parse("#E8F2FF"), Child = Ui.Text(r.Name[..Math.Min(1, r.Name.Length)].ToUpperInvariant(), 11, true, Ui.Primary) }, Ui.Text(r.Name, 12), Ui.Text(metric, 12))).ToArray();
            return Ui.Card(Ui.Stack(12, Ui.Columns("28,*", Ui.Icon(title.Contains("Customer") ? "people" : "inventory_2", 18, title.Contains("Customer") ? Ui.Primary : Brush.Parse("#2E7D32")), Ui.Text(title, 13, true)), rows.Length == 0 ? Ui.Text("No data yet", 13, color: Ui.Muted) : Ui.Stack(10, rows)), 16);
        }
        var body = Ui.Stack(20, greeting);
        switch (dashboardLayout)
        {
            case "Classic":
                body.Children.Add(KpiGrid(kpis.Select(k => k.Label == "Revenue" ? (Label: "Revenue Collected", k.Value, k.Icon, k.Color) : k.Label == "Invoices" ? (Label: "Total Invoices", k.Value, k.Icon, k.Color) : k)));
                body.Children.Add(Ui.Columns("3*,16,2*", ChartCard(230), new Border(), DonutCard()));
                body.Children.Add(Ui.Columns("3*,16,2*", RecentTable(7), new Border(), Ui.Stack(16, OutOfStockCard(), QuickActions())));
                break;
            case "Bento":
                body.Children.Add(Ui.Columns("3*,16,2*", ChartCard(250), new Border(), Ui.Stack(16, KpiGrid([kpis[0], kpis[2]]), KpiGrid([kpis[1], ("Overdue", "0", "hourglass_top", "#D32F2F")]), KpiGrid([kpis[3], kpis[4]]))));
                body.Children.Add(Ui.Columns("3*,16,2*", Ui.Stack(16, RecentTable(8), TopList("Top Products", Model.Products, "2 units")), new Border(), Ui.Stack(16, QuickActions(), OutOfStockCard())));
                break;
            case "Simple Feed":
                body.Children.Add(KpiGrid(kpis));
                body.Children.Add(Ui.Columns("3*,16,2*", Ui.Stack(14, RecentTable(10), TopList("Top Customers", Model.Customers, "Rs. 0.00"), TopList("Top Products", Model.Products, "2 units")), new Border(), Ui.Stack(14, QuickActions(), OutOfStockCard())));
                break;
            default:
                body.Children.Add(KpiGrid(kpis));
                body.Children.Add(OutOfStockCard(true));
                body.Children.Add(RecentFeed());
                break;
        }
        body.MaxWidth = 1600; return Ui.Rows("Auto,*", appbar, new Border { Background = Ui.Surface, Child = Ui.Scroll(body, 28) });
    }
    private void Shortcuts() => ShowOverlay("Keyboard Shortcuts", Ui.Stack(16,
        Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print PDF")));
    private static Control Shortcut(string key, string description) => Ui.Columns("160,*", Ui.Card(Ui.Text(key, 13, true), 10), Ui.Text(description));
}
