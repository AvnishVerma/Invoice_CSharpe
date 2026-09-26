using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private string dashboardLayout = "Default";
    // Performs the dashboard action by preparing dashboard data and loading the XAML dashboard view.
    private Control Dashboard()
    {
        var invoices = Model.ActiveInvoices.OrderByDescending(i => i["Date"]).ThenByDescending(i => i.SourceId).ToArray();
        var paid = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Paid"], out var n) ? n : 0);
        var outstanding = Model.ActiveInvoices.Sum(i => decimal.TryParse(i["Outstanding"], out var n) ? n : decimal.TryParse(i["Total"], out var total) ? total : 0);
        var tiles = new[]
        {
            Tile("Revenue Collected", ShortMoney(paid), "account_balance_wallet", "#8A2BE2"),
            Tile("Outstanding", CurrencyDisplay.Symbol() + " " + outstanding.ToString("0.00"), "hourglass_top", "#D32F2F"),
            Tile("Total Invoices", invoices.Length.ToString(), "receipt_long", "#F97316"),
            Tile("Customers", Model.Customers.Count.ToString(), "people", "#1976D2"),
            Tile("Products", Model.Products.Count.ToString(), "inventory_2", "#2E7D32")
        };

        var recent = invoices.Take(5).Select((invoice, index) => new DashboardInvoiceModel
        {
            Number = index + 1,
            InvoiceTitle = "Invoice #" + invoice.Name,
            CustomerLine = "👤 " + invoice["Customer"],
            CustomerName = invoice["Customer"],
            DateLine = "📅 " + invoice["Date"],
            RawDate = invoice["Date"],
            TotalText = CurrencyDisplay.Symbol(invoice["Currency"]) + " " + invoice["Total"],
            Status = invoice["Status"],
            StatusBrush = StatusColor(invoice["Status"]),
            StatusBackground = Brush.Parse(StatusBackground(invoice["Status"])),
            ViewCommand = new RelayCommand(() => ShowDocumentPreview(invoice)),
            EditCommand = new RelayCommand(() => Model.LoadDocumentForEditing(invoice)),
            CloneCommand = new RelayCommand(() => Model.CloneDocumentForEditing(invoice)),
            PdfCommand = new AsyncRelayCommand(async () => await ShowPdfPreviewAsync(invoice)),
            PrintCommand = new AsyncRelayCommand(async () => await PrintDocumentAsync(invoice)),
            PaymentCommand = new RelayCommand(() => ShowPayment(invoice)),
            DeleteCommand = new RelayCommand(() => DeleteDocumentFromDashboard(invoice))
        });

        var quickActions = new[]
        {
            QuickAction("New Invoice", "add_circle_outline", "#0B4A9A", () => Model.NavigateCommand.Execute("New Invoice")),
            QuickAction("Customers", "group_add", "#1976D2", () => Model.NavigateCommand.Execute("Customers")),
            QuickAction("Reports", "bar_chart", "#2E7D32", () => Model.NavigateCommand.Execute("Reports"))
        };
        var topCustomers = Model.ActiveInvoices
            .GroupBy(i => string.IsNullOrWhiteSpace(i["Customer"]) ? "Cash" : i["Customer"])
            .Select(g => new DashboardSummaryLineModel(g.Key, ShortMoney(g.Sum(i => decimal.TryParse(i["Total"], out var n) ? n : 0m)), Initial(g.Key)))
            .OrderByDescending(x => x.Value)
            .Take(3)
            .DefaultIfEmpty(new DashboardSummaryLineModel("No customers yet", CurrencyDisplay.Format(0), "—"))
            .ToArray();
        var topProducts = Model.Products
            .Take(3)
            .Select(p => new DashboardSummaryLineModel(p.Name, ProductUnits(p), Initial(p.Name)))
            .DefaultIfEmpty(new DashboardSummaryLineModel("No products yet", "0 units", "—"))
            .ToArray();
        var outOfStock = Model.Products.FirstOrDefault(p => !HasUnlimitedStock(p) && decimal.TryParse(p["Stock"], out var stock) && stock <= 0);
        var outOfStockCount = Model.Products.Count(p => !HasUnlimitedStock(p) && decimal.TryParse(p["Stock"], out var stock) && stock <= 0);

        return new DashboardPageView(new DashboardPageModel(tiles, recent, quickActions, topCustomers, topProducts, ShortMoney(paid), CurrencyDisplay.Symbol() + " " + outstanding.ToString("0.00"), outOfStockCount.ToString(), outOfStock?.Name ?? "No product", () => page.Content = Dashboard(), dashboardLayout, selected => dashboardLayout = selected));
    }

    // Performs the quick action creation action for dashboard shortcut cards.
    private static DashboardQuickActionModel QuickAction(string label, string icon, string color, Action action)
    {
        var accent = Brush.Parse(color);
        return new DashboardQuickActionModel(label, icon, accent, new SolidColorBrush(Color.Parse(color), .12), new RelayCommand(action));
    }

    // Performs the tile creation action for dashboard KPI cards.
    private static DashboardTileModel Tile(string label, string value, string icon, string color)
    {
        var accent = Brush.Parse(color);
        return new DashboardTileModel(label, value, icon, accent, new SolidColorBrush(Color.Parse(color), .12));
    }

    // Performs the compact money formatting action for dashboard values.
    private static string ShortMoney(decimal value) => value >= 1000m ? CurrencyDisplay.Symbol() + " " + (value / 1000m).ToString("0.#") + "K" : CurrencyDisplay.Symbol() + " " + value.ToString("0.00");

    // Performs the product unit summary action for dashboard values.
    private static string ProductUnits(UiRecord product) => HasUnlimitedStock(product) ? "∞ units" : (decimal.TryParse(product["Stock"], out var stock) ? stock.ToString("0.###") : "0") + " units";

    // Performs the product unlimited stock check action for dashboard values.
    private static bool HasUnlimitedStock(UiRecord product) => bool.TryParse(product["Unlimited stock"], out var unlimited) && unlimited;

    // Performs the dashboard initials action for compact summary avatars.
    private static string Initial(string value) => string.IsNullOrWhiteSpace(value) ? "—" : char.ToUpperInvariant(value.Trim()[0]).ToString();

    // Performs the status color selection action for dashboard invoice badges.
    private static IBrush StatusColor(string status) => status switch { "Paid" => Brush.Parse("#4CAF50"), "Partial" => Brush.Parse("#FF9800"), "Unpaid" => Brush.Parse("#F44336"), _ => Brush.Parse("#D32F2F") };

    // Performs the status background selection action for dashboard invoice badges.
    private static string StatusBackground(string status) => status switch { "Paid" => "#E8F5E9", "Partial" => "#FFF3E0", "Unpaid" => "#FFEBEE", _ => "#FFEBEE" };

    // Performs the shortcuts action for this screen or workflow.
    private void Shortcuts() => ShowOverlay("Keyboard Shortcuts", Ui.Stack(12.8,
        Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + P", "Print invoice")));

    // Performs the shortcut row creation action for the shortcut overlay.
    private static Control Shortcut(string key, string description) => Ui.Columns("160,*", Ui.Card(Ui.Text(key, 13, true), 10), Ui.Text(description));
}
