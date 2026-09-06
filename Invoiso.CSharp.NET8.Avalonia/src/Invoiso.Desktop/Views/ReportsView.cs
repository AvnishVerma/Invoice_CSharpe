using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Invoiso.Desktop.Views;

namespace Invoiso.Desktop;

public partial class MainWindow
{
    private string reportTab = "Revenue";
    private Control Reports()
    {
        string[] names = ["Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report"];
        var host = new ContentControl();
        var nav = Ui.Stack(4, Ui.Text("Reports", 22, true));
        var buttons = new List<Button>();
        void Select(string name)
        {
            reportTab = name;
            foreach (var b in buttons) b.Classes.Set("selected", (string?)b.Content == name);
            host.Content = ReportContent(name);
        }
        foreach (var name in names) { var b = Ui.Button(name, () => Select(name)); b.Classes.Clear(); b.Classes.Add("nav"); buttons.Add(b); nav.Children.Add(b); }
        nav.Children.Add(new Separator()); nav.Children.Add(Ui.Text("CURRENCY", 11, true, Ui.Muted));
        nav.Children.Add(Ui.Field(new("Currency", "Current selected currency (INR)", "choice", ["Current selected currency (INR)", "All currencies"])));
        nav.Children.Add(Ui.Text("PERIOD", 11, true, Ui.Muted));
        var period = new ListBox { ItemsSource = new[] { "Last 30 days", "Last 3 months", "Last 6 months", "This year", "This FY", "Last FY", "Custom…" }, SelectedIndex = 0 };
        var dates = Ui.Fields([new("From Date", DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), "date"), new("To Date", DateTime.Today.ToString("yyyy-MM-dd"), "date")]); dates.IsVisible = false;
        period.SelectionChanged += (_, _) => dates.IsVisible = period.SelectedItem?.ToString() == "Custom…";
        nav.Children.Add(period); nav.Children.Add(dates); Select(reportTab);
        return Ui.Columns("200,*", new Border { Background = Brush.Parse("#FAFAFA"), BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0), Child = Ui.Scroll(nav, 16) }, host);
    }
    private Control ReportContent(string name)
    {
        var body = Ui.Stack(20, Ui.Header(name, "", Ui.Button("Export CSV"), Ui.Button("Export PDF")));
        switch (name)
        {
            case "Revenue":
                body.Children.Add(Ui.Stats(("Total Billed", "₹ 0.00", "", "#002E78"), ("Total Collected", "₹ 0.00", "", "#2E7D32"), ("Outstanding", "₹ 0.00", "", "#C62828"), ("Avg Invoice Value", "₹ 0.00", "", "#673AB7"), ("Total Profit", "₹ 0.00", "", "#059669")));
                body.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text("Monthly Revenue Trend", 18, true), Ui.Text("0 invoices in period · INR", 12, color: Ui.Muted), EmptyChart(), Ui.Wrap(Ui.Text("● Billed", 12, color: Ui.Primary), Ui.Text("● Collected", 12, color: Brushes.Green), Ui.Text("● Profit", 12, color: Brushes.Purple))))); break;
            case "Receivables":
                body.Children.Add(Ui.Stats(("Outstanding", "₹ 0.00", "", "#C62828"), ("Total Invoices", "0", "", "#002E78")));
                body.Children.Add(Ui.Card(Ui.Stack(16, Ui.Text("Payment Status Breakdown", 18, true), Ui.Empty("No invoices in this period"))));
                body.Children.Add(ReportTable("Aged Receivables (0)", "Customer", "Invoice ID", "Days Overdue", "Bucket", "Outstanding")); break;
            case "Tax":
                body.Children.Add(Ui.Stats(("Total Tax Collected", "₹ 0.00", "", "#002E78"), ("Tax Rate Buckets", "0", "", "#673AB7")));
                body.Children.Add(ReportTable("Tax Collected by Rate", "Tax Rate (%)", "Tax Collected", "Share")); break;
            case "Customers":
                var statement = new ContentControl { Content = ReportTable("Top Customers", "Customer", "Invoices", "Billed", "Collected", "Outstanding") };
                body.Children.Add(Ui.Wrap(Ui.Button("Overview", () => statement.Content = ReportTable("Top Customers", "Customer", "Invoices", "Billed", "Collected", "Outstanding")), Ui.Button("Statements", () => statement.Content = Ui.Stack(16, Ui.Field(new("Customer", "Select customer", "choice", new[] { "Select customer" }.Concat(Model.Customers.Select(c => c.Name)).ToArray())), Ui.Stats(("Opening", "₹ 0.00", "", "#002E78"), ("Invoiced", "₹ 0.00", "", "#673AB7"), ("Closing", "₹ 0.00", "", "#2E7D32")), ReportTable("Customer Statement", "Date", "Type", "Reference", "Debit", "Credit", "Balance"))))); body.Children.Add(statement); break;
            case "Products": body.Children.Add(ReportTable("Top Products", "Product / Service", "Units Sold", "Sales", "Discount Given", "Profit", "Margin")); break;
            case "Quotations": body.Children.Add(Ui.Stats(("Quotations Issued", "0", "", "#002E78"), ("Invoices in Period", "0", "", "#2E7D32"), ("Conversion Rate", "0%", "", "#673AB7"))); body.Children.Add(Ui.Card(Ui.Stack(12, Ui.Text("About Conversion Rate", 18, true), Ui.Text("Compare quotations and invoices issued in the selected period.")))); break;
            case "Invoice Status": body.Children.Add(Ui.Wrap(new RadioButton { Content = "All", GroupName = "status", IsChecked = true }, new RadioButton { Content = "Paid", GroupName = "status" }, new RadioButton { Content = "Partial", GroupName = "status" }, new RadioButton { Content = "Unpaid", GroupName = "status" })); body.Children.Add(ReportTable("Invoice Status", "Invoice / Customer", "Date", "Total", "Status", "Outstanding")); break;
            case "Daily Report": body.Children.Add(Ui.Fields([new("Period", "Today", "choice", ["Today", "Month & Year", "Custom Range"]), new("Date", DateTime.Today.ToString("yyyy-MM-dd"), "date")], 2)); body.Children.Add(ReportTable("Daily Sales & Profit", "Date", "Invoices", "Sales", "COGS", "Tax", "Profit")); break;
        }
        return Ui.Scroll(body, 24);
    }
    private static Control ReportTable(string title, params string[] columns) => Ui.Card(Ui.Stack(18, Ui.Text(title, 18, true), new Border { Background = Brush.Parse("#F5F5F5"), Padding = new Thickness(12), Child = Ui.Columns(string.Join(",", columns.Select(_ => "*")), columns.Select(c => (Control)Ui.Text(c, 12, true)).ToArray()) }, Ui.Empty("No data for this period")));
    private static Control EmptyChart()
    {
        var rows = Ui.Stack(0);
        for (var i = 4; i >= 0; i--) rows.Children.Add(new Border { Height = 42, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Text(i.ToString(), 11, color: Ui.Muted) });
        rows.Children.Add(Ui.Columns("*,*,*,*,*,*", Enumerable.Range(0, 6).Select(i => (Control)Ui.Text(DateTime.Today.AddMonths(i - 5).ToString("MMM"), 11, color: Ui.Muted)).ToArray())); return rows;
    }
}
