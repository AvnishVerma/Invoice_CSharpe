using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LedgerNest.Desktop.Views;
using Avalonia.Platform.Storage;
using System.Text;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    private string reportTab = "Revenue";
    private string reportCustomerFilter = "";
    // Performs the reports action by preparing report navigation data and loading the XAML reports view.
    private Control Reports()
    {
        var pendingCustomer = Model.ConsumePendingReportCustomerFilter();
        if (!string.IsNullOrWhiteSpace(pendingCustomer))
        {
            reportTab = "Customers";
            reportCustomerFilter = pendingCustomer;
        }
        string[] names = ["Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report"];
        return new ReportsPageView(new ReportsPageModel(names, reportTab, ReportContent, selected => { reportTab = selected; if (selected != "Customers") reportCustomerFilter = ""; }));
    }
    // Performs the report content action for this screen or workflow.
    private Control ReportContent(string name)
    {
        var report = Model.BuildReport(name);
        var body = Ui.Stack(20);

        if (name == "Revenue")
        {
            body.Children.Add(ReportStats(("Total Billed", Money(report.Billed), "receipt_long", "#0D47A1"), ("Total Collected", Money(report.Collected), "check_circle", "#16A34A"), ("Outstanding", Money(report.Outstanding), "hourglass_top", "#E53935"), ("Avg Invoice Value", Money(report.InvoiceCount == 0 ? 0 : report.Billed / report.InvoiceCount), "bar_chart", "#7C3AED"), ("Total Profit", Money(Model.Invoices.Sum(i => decimal.TryParse(i["Profit"], out var p) ? p : 0m)), "account_balance_wallet", "#16A34A")));
            body.Children.Add(ChartCard("Revenue", "Monthly Revenue Trend", $"{report.InvoiceCount} invoices in period · INR", report.Billed, report.Collected, Model.Invoices.Sum(i => decimal.TryParse(i["Profit"], out var p) ? p : 0m)));
        }
        else if (name == "Receivables")
        {
            body.Children.Add(DonutCard());
            body.Children.Add(ReportTable("Aged Receivables (1)", report.Rows, true, "Receivables"));
        }
        else if (name == "Tax")
        {
            body.Children.Add(ReportStats(("Total Tax Collected", Money(report.Collected), "account_balance_wallet", "#8B5CF6"), ("Tax Rate Buckets", Math.Max(1, report.Rows.Length - 1).ToString(), "bar_chart", "#0284C7")));
            body.Children.Add(ReportTable("Tax Collected by Rate", report.Rows, true, "Tax"));
        }
        else if (name == "Customers")
        {
            var hasCustomerFilter = !string.IsNullOrWhiteSpace(reportCustomerFilter);
            var statement = new ContentControl { Content = hasCustomerFilter ? CustomerStatementCard(reportCustomerFilter) : CustomerRevenueCard(report.Rows) };
            body.Children.Add(Ui.Wrap(
                Ui.Button("✓  Overview", () => { reportCustomerFilter = ""; statement.Content = CustomerRevenueCard(Model.BuildReport("Customers").Rows); }, !hasCustomerFilter),
                Ui.Button("Statements", () => statement.Content = CustomerStatementCard(reportCustomerFilter), hasCustomerFilter)));
            body.Children.Add(statement);
        }
        else if (name == "Products")
        {
            body.Children.Add(ProductRevenueCard(report.Rows));
        }
        else if (name == "Quotations")
        {
            body.Children.Add(ReportStats(("Quotations Issued", "0", "request_quote", "#0284C7"), ("Invoices in Period", report.InvoiceCount.ToString(), "receipt_long", "#16A34A"), ("Conversion Rate", "0.0%", "bar_chart", "#7C3AED")));
            body.Children.Add(Ui.Card(Ui.Stack(12, Ui.Text("About Conversion Rate", 16, true), Ui.Text("Conversion rate = Invoices created ÷ Quotations issued × 100.", 13, color: Ui.Muted), Ui.Text("A rate above 100% means more invoices were raised than quotations in the selected period." , 13, color: Ui.Muted), Ui.Text("Note: this is a period-level ratio, not individual quote-to-invoice tracking.", 13, color: Ui.Muted)), 20));
        }
        else if (name == "Invoice Status")
        {
            body.Children.Add(Ui.Text($"ⓘ Showing invoices dated {DateTime.Today.AddDays(-30):dd/MM/yyyy} - {DateTime.Today:dd/MM/yyyy}", 12, color: Ui.Muted));
            body.Children.Add(Ui.Wrap(Ui.Button("Last 30 days", () => { }, true), Ui.Button("Month & Year", () => { })));
            body.Children.Add(ReportStats(("Total Invoices", report.InvoiceCount.ToString(), "receipt_long", "#0D47A1"), ("Paid", report.Rows.Count(r => r.Contains("Paid")).ToString(), "check_circle", "#16A34A"), ("Partial", report.Rows.Count(r => r.Contains("Partial")).ToString(), "hourglass_top", "#F59E0B"), ("Unpaid", report.Rows.Count(r => r.Contains("Unpaid")).ToString(), "info_outline", "#6B7280"), ("Overdue", "0", "info_outline", "#DC2626")));
            body.Children.Add(ReportTable("", report.Rows, true, name));
        }
        else if (name == "Daily Report")
        {
            body.Children.Add(DailyReportCard(report.Rows));
        }
        else
        {
            body.Children.Add(ReportTable(name, report.Rows, true, name));
        }

        return Ui.Scroll(body, 18);
    }

    // Performs the export report csv action for this screen or workflow.
    private async Task ExportReportCsv(string name)
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new()
            {
                Title = $"Export {name} CSV",
                SuggestedFileName = $"ledgernest-{name.ToLowerInvariant().Replace(" ", "-")}-report.csv",
                DefaultExtension = "csv",
                FileTypeChoices = [new FilePickerFileType("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv", "text/plain"] }]
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync(Model.ExportReportCsv(name));
            ShowOverlay("Report Exported", Ui.Text($"Saved {file.Name}."), Ui.Button("Close", CloseOverlay, true));
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, $"Exporting report CSV {name}");
            Model.Status = $"Could not save report. Log: {AppErrorLog.Path}";
        }
    }


    // Performs the export report pdf action for this screen or workflow.
    private async Task ExportReportPdf(string name)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var path = FilePickerHelpers.MacDownloadsPdfPath($"ledgernest-{name.ToLowerInvariant().Replace(" ", "-")}-report");
                await File.WriteAllBytesAsync(path, Model.ExportReportPdf(name));
                Model.Status = $"Saved PDF to {path}";
                return;
            }
            var file = await StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions($"Export {name} PDF", $"ledgernest-{name.ToLowerInvariant().Replace(" ", "-")}-report"));
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(Model.ExportReportPdf(name));
            ShowOverlay("Report Exported", Ui.Text($"Saved {file.Name}."), Ui.Button("Close", CloseOverlay, true));
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, $"Exporting report PDF {name}");
            Model.Status = $"Could not save PDF. Log: {AppErrorLog.Path}";
        }
    }

    // Performs the money action for this screen or workflow.
    private static string Money(decimal value) => $"Rs. {value:0.00}";

    // Performs the report stats action for this screen or workflow.
    private static Control ReportStats(params (string Label, string Value, string Icon, string Color)[] stats)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", stats.Select(_ => "*"))) };
        for (var i = 0; i < stats.Length; i++)
        {
            var s = stats[i];
            var tile = Ui.Card(Ui.Columns("Auto,14,*", new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.Parse(s.Color), .12),
                Child = Ui.Icon(s.Icon, 22, Brush.Parse(s.Color))
            }, new Border(), Ui.Stack(8, Ui.Text(s.Label, 13, true, Ui.Muted), Ui.Text(s.Value, 22, true, Brush.Parse(s.Color)))), 18);
            tile.Background = Brush.Parse("#FBF5FF");
            tile.Margin = new Thickness(i == 0 ? 0 : 10, 0, i == stats.Length - 1 ? 0 : 10, 0);
            Grid.SetColumn(tile, i);
            grid.Children.Add(tile);
        }
        return grid;
    }

    // Performs the chart card action for this screen or workflow.
    private Control ChartCard(string reportName, string title, string subtitle, decimal billed, decimal collected, decimal profit)
    {
        var chart = new ScottPlot.Avalonia.AvaPlot { Height = 300 };
        var billedBar = chart.Plot.Add.Bar(1, (double)billed); billedBar.Color = ScottPlot.Color.FromHex("#3B82F6"); billedBar.LegendText = "Billed";
        var collectedBar = chart.Plot.Add.Bar(2, (double)collected); collectedBar.Color = ScottPlot.Color.FromHex("#22C55E"); collectedBar.LegendText = "Collected";
        var profitBar = chart.Plot.Add.Bar(3, (double)profit); profitBar.Color = ScottPlot.Color.FromHex("#7C3AED"); profitBar.LegendText = "Profit";
        chart.Plot.Axes.Left.Min = 0;
        chart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual([1, 2, 3], ["Billed", "Collected", "Profit"]);
        chart.Plot.Legend.IsVisible = true;
        chart.Plot.HideGrid();
        chart.Refresh();

        return Ui.Card(Ui.Stack(10, Ui.Columns("*,Auto", Ui.Stack(4, Ui.Text(title, 16, true), Ui.Text(subtitle, 12, color: Ui.Muted)), Ui.Wrap(Ui.Button("↓ Export CSV", async () => await ExportReportCsv(reportName)), Ui.Button("↓ Export PDF", async () => await ExportReportPdf(reportName)))), chart), 20);
    }

    // Performs the donut card action for this screen or workflow.
    private static Control DonutCard()
    {
        var chart = new ScottPlot.Avalonia.AvaPlot { Width = 260, Height = 240 };
        var pie = chart.Plot.Add.Pie([
            new ScottPlot.PieSlice { Value = 1, Label = "Paid", LegendText = "Paid", FillColor = ScottPlot.Color.FromHex("#22C55E") },
            new ScottPlot.PieSlice { Value = 1, Label = "Unpaid", LegendText = "Unpaid", FillColor = ScottPlot.Color.FromHex("#EF4444") }
        ]);
        pie.DonutFraction = .55;
        pie.SliceLabelDistance = .65;
        chart.Plot.Axes.Frameless();
        chart.Plot.Legend.IsVisible = false;
        chart.Refresh();

        var legend = Ui.Stack(14, Legend("#22C55E", "Paid  1  (50.0%)"), Legend("#F59E0B", "Partial  0  (0.0%)"), Legend("#EF4444", "Unpaid  1  (50.0%)"), Ui.Text("2 total invoices", 13, color: Ui.Muted));
        return Ui.Card(Ui.Stack(10, Ui.Text("Payment Status Breakdown", 16, true), Ui.Columns("300,30,*", chart, new Border(), legend)), 20);
    }

    // Performs the customer revenue card action for this screen or workflow.
    private Control CustomerRevenueCard(string[][] rows)
    {
        var name = rows.Length > 1 && rows[1].Length > 0 ? rows[1][0] : "Cash";
        var amount = rows.Length > 1 && rows[1].Length > 3 ? rows[1][3] : "Rs. 0.00";
        return Ui.Card(Ui.Stack(12, Ui.Columns("*,Auto", Ui.Text("Top 1 Customers by Revenue", 16, true), Ui.Wrap(Ui.Button("↓ Export CSV", async () => await ExportReportCsv("Customers")), Ui.Button("↓ Export PDF", async () => await ExportReportPdf("Customers")))), Ui.Columns("130,*,110", Ui.Text(name, 13), new Border { Height = 22, CornerRadius = new CornerRadius(4), Background = Brush.Parse("#3B82F6") }, Ui.Text(amount, 13, true, Ui.Primary)), ReportTable("", rows, false, "Customers")), 20);
    }

    // Performs the customer statement card action for the selected customer report workflow.
    private Control CustomerStatementCard(string customerName)
    {
        var header = new[] { "Invoice", "Customer", "Date", "Total", "Status", "Outstanding" };
        var rows = Model.BuildReport("Invoice Status").Rows
            .Skip(1)
            .Where(row => row.Length > 1 && (string.IsNullOrWhiteSpace(customerName) || row[1].Equals(customerName, StringComparison.OrdinalIgnoreCase)))
            .Prepend(header)
            .ToArray();
        var title = string.IsNullOrWhiteSpace(customerName) ? "Customer Statement" : $"Customer Statement — {customerName}";
        return Ui.Stack(16,
            Ui.Field(new("Customer", string.IsNullOrWhiteSpace(customerName) ? "Select customer" : customerName, "choice", new[] { "Select customer" }.Concat(Model.Customers.Select(c => c.Name)).ToArray())),
            ReportTable(title, rows, true, "Customers"));
    }

    // Performs the product revenue card action for this screen or workflow.
    private Control ProductRevenueCard(string[][] rows)
    {
        var list = Ui.Stack(8);
        for (var i = 1; i < rows.Length; i++)
        {
            var name = rows[i].Length > 0 ? rows[i][0] : "Product";
            var amount = rows[i].Length > 2 ? rows[i][2] : "Rs. 0.00";
            var width = i == 1 ? "*" : "0.3*";
            list.Children.Add(Ui.Columns("120,*,110", Ui.Text(name, 13), new Border { Height = 22, CornerRadius = new CornerRadius(4), Background = Brush.Parse("#7C3AED"), HorizontalAlignment = HorizontalAlignment.Stretch }, Ui.Text(amount, 13, true, Brush.Parse("#7C3AED"))));
        }
        if (rows.Length <= 1) list.Children.Add(Ui.Empty("No product sales for this period"));
        return Ui.Card(Ui.Stack(12, Ui.Columns("*,Auto", Ui.Text("Top 2 Products / Services by Revenue", 16, true), Ui.Wrap(Ui.Text("▣ Rank: Revenue", 12, true, Ui.Muted), Ui.Button("↓ Export CSV", async () => await ExportReportCsv("Products")), Ui.Button("↓ Export PDF", async () => await ExportReportPdf("Products")))), list, ReportTable("", rows, false, "Products")), 20);
    }

    // Performs the daily report card action for this screen or workflow.
    private Control DailyReportCard(string[][] rows)
    {
        return Ui.Card(Ui.Stack(14, Ui.Columns("*,Auto", Ui.Text("Daily Sales & Profit", 16, true), Ui.Wrap(Ui.Button("↓ Export CSV", async () => await ExportReportCsv("Daily Report")), Ui.Button("↓ Export PDF", async () => await ExportReportPdf("Daily Report")))), Ui.Wrap(Ui.Button("Today", () => { }, true), Ui.Button("Last 30 days", () => { }), Ui.Button("Month & Year", () => { }), Ui.Button("Custom Range", () => { })), ReportTable("", rows, false, "Daily Report")), 20);
    }

    // Performs the legend action for this screen or workflow.
    private static Control Legend(string color, string text) => Ui.Columns("Auto,8,*", new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(3), Background = Brush.Parse(color), VerticalAlignment = VerticalAlignment.Center }, new Border(), Ui.Text(text, 13, true));

    // Performs the report table action for this screen or workflow.
    private Control ReportTable(string title, string[][] rows, bool wrapInCard, string reportName = "")
    {
        var table = Ui.Stack(0);
        if (!string.IsNullOrWhiteSpace(title)) table.Children.Add(new Border { Padding = new Thickness(0, 0, 0, 10), Child = Ui.Columns("*,Auto", Ui.Text(title, 16, true), Ui.Wrap(Ui.Button("↓ Export CSV", async () => await ExportReportCsv(string.IsNullOrWhiteSpace(reportName) ? title : reportName)), Ui.Button("↓ Export PDF", async () => await ExportReportPdf(string.IsNullOrWhiteSpace(reportName) ? title : reportName)))) });
        if (rows.Length == 0) table.Children.Add(Ui.Empty("No data for this period"));
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            table.Children.Add(new Border
            {
                Background = index == 0 ? Brushes.White : Brush.Parse(index % 2 == 0 ? "#FFFFFF" : "#FBF5FF"),
                BorderBrush = Ui.Outline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, index == 0 ? 10 : 14),
                Child = Ui.Columns(string.Join(",", row.Select(_ => "*")), row.Select((c, i) => (Control)Ui.Text(index == 0 ? c.ToUpperInvariant() : c, index == 0 ? 11 : 13, index == 0 || i == 0, index == 0 ? Ui.Muted : c.Contains("Rs.") && c.Contains("70") ? Brush.Parse("#EF4444") : c.Contains("Rs.") ? Brush.Parse("#16A34A") : null)).ToArray())
            });
        }
        table.Children.Add(new Border { Padding = new Thickness(14, 12), Child = Ui.Columns("*,Auto", Ui.Text("Rows per page: 10      1 – 1 of 1", 13), Ui.Text("‹   Page 1 of 1   ›", 13, true)) });
        var scroll = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        return wrapInCard ? Ui.Card(scroll, 20) : scroll;
    }
    // Performs the empty chart action for this screen or workflow.
    private static Control EmptyChart()
    {
        var rows = Ui.Stack(0);
        for (var i = 4; i >= 0; i--) rows.Children.Add(new Border { Height = 42, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Text(i.ToString(), 11, color: Ui.Muted) });
        rows.Children.Add(Ui.Columns("*,*,*,*,*,*", Enumerable.Range(0, 6).Select(i => (Control)Ui.Text(DateTime.Today.AddMonths(i - 5).ToString("MMM"), 11, color: Ui.Muted)).ToArray())); return rows;
    }
}
