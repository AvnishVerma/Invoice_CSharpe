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
    // Performs the dashboard action for this screen or workflow.
    private Control Dashboard()
    {
        var invoices = Model.ActiveInvoices.OrderByDescending(i => i["Date"]).ThenByDescending(i => i.SourceId).ToArray();
        var paid = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Paid"], out var n) ? n : 0);
        var outstanding = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Outstanding"], out var n) ? n : decimal.TryParse(i["Total"], out var total) ? total : 0);
        var outOfStock = Model.Products.Where(p => !HasUnlimitedStock(p) && decimal.TryParse(p["Stock"], out var stock) && stock <= 0).ToArray();
        var layout = new Button { Padding = new Thickness(8, 6), MinHeight = 36, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
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
            ("Customers", Model.Customers.Count.ToString(), "people", "#1976D2"),
            ("Products", Model.Products.Count.ToString(), "inventory_2", "#2E7D32"),
            ("Invoices", invoices.Length.ToString(), "receipt_long", "#F97316"),
            ("Revenue Collected", "Rs. " + paid.ToString("0.00"), "account_balance_wallet", "#8A2BE2"),
            ("Outstanding", "Rs. " + outstanding.ToString("0.00"), "hourglass_top", "#D32F2F")
        ];
        // Performs the has unlimited stock action for this screen or workflow.
        static bool HasUnlimitedStock(UiRecord product) => bool.TryParse(product["Unlimited stock"], out var unlimited) && unlimited;

        Control KpiCard((string Label, string Value, string Icon, string Color) item, string? alert = null)
        {
            var badge = new Border { Width = 37, Height = 37, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.Parse(item.Color), .12), Child = Ui.Icon(item.Icon, 19, Brush.Parse(item.Color)) };
            Control header = alert == null ? Ui.Text(item.Label, 11, color: Ui.Muted) : Ui.Columns("*,Auto", Ui.Text(item.Label, 11, color: Ui.Muted), Ui.Text(alert, 11, color: Brushes.Red));
            return new Border { Padding = new Thickness(20), MinHeight = 78, CornerRadius = new CornerRadius(12), Background = Ui.Canvas, BoxShadow = BoxShadows.Parse("0 8 22 0 #10000000"), Child = Ui.Columns("40,14,*", badge, new Border(), Ui.Stack(5, header, Ui.Text(item.Value, 16, true))) };
        }
        Control KpiGrid(IEnumerable<(string Label, string Value, string Icon, string Color)> items)
        {
            var array = items.ToArray();
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Range(0, array.Length).SelectMany(i => i == 0 ? ["*"] : new[] { "12", "*" }))) };
            for (var i = 0; i < array.Length; i++) { var card = KpiCard(array[i], array[i].Label == "Products" && outOfStock.Length > 0 ? $"{outOfStock.Length} out of stock" : null); Grid.SetColumn(card, i * 2); grid.Children.Add(card); }
            return grid;
        }
        Control ChartCard(double minHeight = 255) => Ui.Card(Ui.Rows("Auto,*", Ui.Text("Revenue — Last 6 Months", 13, true), new Grid { MinHeight = minHeight, Children = { new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 8, Children = { Ui.Icon("bar_chart", 42, Brush.Parse("#D9D9D9")), Ui.Text("No payment data yet", 13, color: Ui.Muted) } } } }), 20);
        Control DonutCard() => Ui.Card(Ui.Stack(18, Ui.Text("Financial Overview", 13, true), new Ellipse { Width = 168, Height = 168, StrokeThickness = 38, Stroke = Brushes.Firebrick, Fill = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Center }, Ui.Columns("*,Auto", Ui.Stack(10, Ui.Text("Collected", 12, color: Ui.Muted), Ui.Text("Outstanding", 12, color: Ui.Muted)), Ui.Stack(10, Ui.Text("Rs. " + paid.ToString("0.00"), 12, true), Ui.Text("Rs. " + outstanding.ToString("0.0K"), 12, true)))), 20);
        Control QuickActions() => Ui.Card(Ui.Stack(14, Ui.Text("Quick Actions", 13, true), ActionRow("add", "New Invoice", () => Model.NavigateCommand.Execute("New Invoice"), "#1565C0"), ActionRow("people", "Customers", () => Model.NavigateCommand.Execute("Customers"), "#1976D2"), ActionRow("bar_chart", "Reports", () => Model.NavigateCommand.Execute("Reports"), "#2E7D32")), 18);
        Control ActionRow(string icon, string title, Action action, string color)
        {
            var button = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Command = new CommunityToolkit.Mvvm.Input.RelayCommand(action), Content = Ui.Columns("34,12,*,Auto", new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Color.Parse(color), .1), Child = Ui.Icon(icon, 17, Brush.Parse(color)) }, new Border(), Ui.Text(title, 13, true), Ui.Icon("chevron_right", 18, Ui.Muted)) };
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
            foreach (var inv in invoices.Take(take)) stack.Children.Add(Ui.Columns("*,*,Auto", Ui.Text("#" + inv.Name, 12), Ui.Text(inv["Customer"], 12, color: Ui.Muted), Ui.Wrap(Ui.Text("Rs. " + inv["Total"], 12), Ui.Text(inv["Status"], 11, color: Ui.Muted), Ui.Button("✎", () => Model.LoadDocumentForEditing(inv)), Ui.Button("↓", async () => await DownloadDocumentPdf(inv)))));
            return Ui.Card(stack, 18);
        }
        Control RecentFeed()
        {
            var stack = Ui.Stack(18, Ui.Columns("*,Auto", Ui.Columns("4,12,*", new Border { Height = 30, Background = Ui.Primary, CornerRadius = new CornerRadius(3) }, new Border(), Ui.Text("Recent Invoices", 22, true)), Ui.Text("Last 5 invoices", 13, color: Ui.Muted)));
            var index = 1;
            foreach (var inv in invoices.Take(5))
            {
                var typeBadge = Pill("Invoice", Ui.Primary, "#EAF2FF");
                var status = inv["Status"];
                var statusBadge = Pill(status, StatusColor(status), StatusBackground(status));
                var row = new Border
                {
                    Padding = new Thickness(16),
                    CornerRadius = new CornerRadius(8),
                    Background = Brush.Parse("#FCF6FF"),
                    Child = Ui.Columns("54,16,*,Auto",
                        new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(9), Background = Ui.Primary, VerticalAlignment = VerticalAlignment.Center, Child = Ui.Text((index++).ToString(), 20, true, Brushes.White) },
                        new Border(),
                        Ui.Stack(7,
                            Ui.Wrap(Ui.Text("Invoice #" + inv.Name, 18, true), typeBadge, statusBadge),
                            Ui.Columns("22,*", Ui.Icon("person", 15, Ui.Muted), Ui.Text(inv["Customer"], 14)),
                            Ui.Columns("22,*", Ui.Icon("calendar_today", 15, Ui.Muted), Ui.Text(inv["Date"], 14))),
                        Ui.Stack(10,
                            new Border { Padding = new Thickness(18, 10), CornerRadius = new CornerRadius(7), Background = Brush.Parse("#F3DDFB"), HorizontalAlignment = HorizontalAlignment.Right, Child = Ui.Text("Rs. " + inv["Total"], 20, true, Brush.Parse("#A21CAF")) },
                            DashboardActions(inv)))
                };
                stack.Children.Add(row);
            }
            if (invoices.Length == 0) stack.Children.Add(Ui.Empty("No invoices yet", "Create your first invoice to see it here"));
            return stack;
        }

        Control DashboardActions(UiRecord inv)
        {
            var panel = Ui.Wrap(
                DashboardIcon("visibility", "View", () => { ShowDocumentPreview(inv); return Task.CompletedTask; }, "#4CAF50"),
                DashboardIcon("edit", "Edit", () => { Model.LoadDocumentForEditing(inv); return Task.CompletedTask; }, "#2196F3"),
                DashboardIcon("receipt_long", "Duplicate", () => { Model.Status = "Duplicate invoice is not yet migrated."; return Task.CompletedTask; }, "#0097A7"),
                DashboardIcon("picture_as_pdf", "PDF", async () => await DownloadDocumentPdf(inv), "#FF9800"),
                DashboardIcon("download", "Download", async () => await DownloadDocumentPdf(inv), "#673AB7"),
                DashboardIcon("print", "Print", async () => await PrintDocumentPdf(inv), "#607D8B"),
                DashboardIcon("account_balance_wallet", "Payment", () => { ShowPayment(inv); return Task.CompletedTask; }, "#9C27B0"),
                DashboardIcon("delete", "Delete", () => { DeleteDocumentFromDashboard(inv); return Task.CompletedTask; }, "#F44336"));
            foreach (var child in panel.Children) child.Margin = new Thickness(0, 0, 6, 0);
            return panel;
        }

        Button DashboardIcon(string icon, string label, Func<Task> action, string color)
        {
            var button = Ui.Button(label, async () => await action());
            button.Content = Ui.Icon(icon, 17, Brush.Parse(color));
            button.Width = 42; button.Height = 42; button.MinWidth = 42; button.MinHeight = 42;
            button.Padding = new Thickness(0);
            button.Background = new SolidColorBrush(Color.Parse(color), .12);
            button.BorderBrush = new SolidColorBrush(Color.Parse(color), .32);
            button.Tag = label;
            return button;
        }

        // Performs the pill action for this screen or workflow.
        static Border Pill(string text, IBrush color, string background) => new() { Padding = new Thickness(9, 4), CornerRadius = new CornerRadius(4), Background = Brush.Parse(background), BorderBrush = color, BorderThickness = new Thickness(1), Child = Ui.Text(text, 12, false, color) };
        // Performs the status color action for this screen or workflow.
        static IBrush StatusColor(string status) => status switch { "Paid" => Brush.Parse("#4CAF50"), "Partial" => Brush.Parse("#FF9800"), "Unpaid" => Brush.Parse("#F44336"), _ => Brush.Parse("#D32F2F") };
        // Performs the status background action for this screen or workflow.
        static string StatusBackground(string status) => status switch { "Paid" => "#E8F5E9", "Partial" => "#FFF3E0", "Unpaid" => "#FFEBEE", _ => "#FFEBEE" };
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
                body.Children.Add(RecentFeed());
                break;
        }
        return new DashboardPageView(appbar, body);
    }
    // Performs the shortcuts action for this screen or workflow.
    private void Shortcuts() => ShowOverlay("Keyboard Shortcuts", Ui.Stack(16,
        Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print PDF")));
    // Performs the shortcut action for this screen or workflow.
    private static Control Shortcut(string key, string description) => Ui.Columns("160,*", Ui.Card(Ui.Text(key, 13, true), 10), Ui.Text(description));
}
