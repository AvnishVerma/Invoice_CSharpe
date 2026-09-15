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
            Tile("Customers", Model.Customers.Count.ToString(), "👥", "#1976D2"),
            Tile("Products", Model.Products.Count.ToString(), "▣", "#2E7D32"),
            Tile("Invoices", invoices.Length.ToString(), "▤", "#F97316"),
            Tile("Revenue Collected", "Rs. " + paid.ToString("0.00"), "▣", "#8A2BE2"),
            Tile("Outstanding", "Rs. " + outstanding.ToString("0.00"), "⌛", "#D32F2F")
        };

        var recent = invoices.Take(5).Select((invoice, index) => new DashboardInvoiceModel
        {
            Number = index + 1,
            InvoiceTitle = "Invoice #" + invoice.Name,
            CustomerLine = "☉ " + invoice["Customer"],
            CustomerName = invoice["Customer"],
            DateLine = "▣ " + invoice["Date"],
            RawDate = invoice["Date"],
            TotalText = "Rs. " + invoice["Total"],
            Status = invoice["Status"],
            StatusBrush = StatusColor(invoice["Status"]),
            StatusBackground = Brush.Parse(StatusBackground(invoice["Status"])),
            ViewCommand = new RelayCommand(() => ShowDocumentPreview(invoice)),
            EditCommand = new RelayCommand(() => Model.LoadDocumentForEditing(invoice)),
            CloneCommand = new RelayCommand(() => Model.CloneDocumentForEditing(invoice)),
            PdfCommand = new RelayCommand(() => ShowPdfPreview(invoice)),
            PrintCommand = new AsyncRelayCommand(async () => await PrintDocumentPdf(invoice)),
            PaymentCommand = new RelayCommand(() => ShowPayment(invoice)),
            DeleteCommand = new RelayCommand(() => DeleteDocumentFromDashboard(invoice))
        });

        return new DashboardPageView(new DashboardPageModel(tiles, recent, () => page.Content = Dashboard(), dashboardLayout, selected => dashboardLayout = selected));
    }

    // Performs the tile creation action for dashboard KPI cards.
    private static DashboardTileModel Tile(string label, string value, string icon, string color)
    {
        var accent = Brush.Parse(color);
        return new DashboardTileModel(label, value, icon, accent, new SolidColorBrush(Color.Parse(color), .12));
    }

    // Performs the status color selection action for dashboard invoice badges.
    private static IBrush StatusColor(string status) => status switch { "Paid" => Brush.Parse("#4CAF50"), "Partial" => Brush.Parse("#FF9800"), "Unpaid" => Brush.Parse("#F44336"), _ => Brush.Parse("#D32F2F") };

    // Performs the status background selection action for dashboard invoice badges.
    private static string StatusBackground(string status) => status switch { "Paid" => "#E8F5E9", "Partial" => "#FFF3E0", "Unpaid" => "#FFEBEE", _ => "#FFEBEE" };

    // Performs the shortcuts action for this screen or workflow.
    private void Shortcuts() => ShowOverlay("Keyboard Shortcuts", Ui.Stack(16,
        Shortcut("Ctrl + Q", "New invoice"), Shortcut("Ctrl + S", "Save invoice"), Shortcut("Ctrl + F", "Search products"), Shortcut("Ctrl + M", "Add custom item"), Shortcut("Ctrl + O", "Preview PDF"), Shortcut("Ctrl + P", "Print PDF")));

    // Performs the shortcut row creation action for the shortcut overlay.
    private static Control Shortcut(string key, string description) => Ui.Columns("160,*", Ui.Card(Ui.Text(key, 13, true), 10), Ui.Text(description));
}
