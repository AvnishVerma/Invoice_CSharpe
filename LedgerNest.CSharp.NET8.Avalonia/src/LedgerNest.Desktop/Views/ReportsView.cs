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
    private Control Reports()
    {
        string[] names = ["Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report"];
        var host = new ContentControl();
        var nav = Ui.Stack(4);
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
        var content = Ui.Columns("200,*", new Border { Background = Ui.Surface, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 1, 0), Child = Ui.Scroll(nav, 16) }, host);
        return Ui.Rows("Auto,*", Ui.AppBar("Reports", Ui.Button("↻", () => host.Content = ReportContent(reportTab))), content);
    }
    private Control ReportContent(string name)
    {
        var report = Model.BuildReport(name);
        var body = Ui.Stack(20);

        if (name == "Revenue")
        {
            body.Children.Add(ReportStats(("Total Billed", Money(report.Billed), "receipt_long", "#0D47A1"), ("Total Collected", Money(report.Collected), "check_circle", "#16A34A"), ("Outstanding", Money(report.Outstanding), "hourglass_top", "#E53935"), ("Avg Invoice Value", Money(report.InvoiceCount == 0 ? 0 : report.Billed / report.InvoiceCount), "bar_chart", "#7C3AED"), ("Total Profit", Money(Model.Invoices.Sum(i => decimal.TryParse(i["Profit"], out var p) ? p : 0m)), "account_balance_wallet", "#16A34A")));
            body.Children.Add(ChartCard("Monthly Revenue Trend", $"{report.InvoiceCount} invoices in period · INR", report.Billed, report.Collected, Model.Invoices.Sum(i => decimal.TryParse(i["Profit"], out var p) ? p : 0m)));
        }
        else if (name == "Receivables")
        {
            body.Children.Add(DonutCard());
            body.Children.Add(ReportTable("Aged Receivables (1)", report.Rows, true));
        }
        else if (name == "Tax")
        {
            body.Children.Add(ReportStats(("Total Tax Collected", Money(report.Collected), "account_balance_wallet", "#8B5CF6"), ("Tax Rate Buckets", Math.Max(1, report.Rows.Length - 1).ToString(), "bar_chart", "#0284C7")));
            body.Children.Add(ReportTable("Tax Collected by Rate", report.Rows, true));
        }
        else if (name == "Customers")
        {
            var statement = new ContentControl { Content = CustomerRevenueCard(report.Rows) };
            body.Children.Add(Ui.Wrap(Ui.Button("✓  Overview", () => statement.Content = CustomerRevenueCard(Model.BuildReport("Customers").Rows), true), Ui.Button("Statements", () => statement.Content = Ui.Stack(16, Ui.Field(new("Customer", "Select customer", "choice", new[] { "Select customer" }.Concat(Model.Customers.Select(c => c.Name)).ToArray())), ReportTable("Customer Statement", Model.BuildReport("Invoice Status").Rows, true)))));
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
            body.Children.Add(ReportTable("", report.Rows, true));
        }
        else if (name == "Daily Report")
        {
            body.Children.Add(DailyReportCard(report.Rows));
        }
        else
        {
            body.Children.Add(ReportTable(name, report.Rows, true));
        }

        return Ui.Scroll(body, 18);
    }

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

    private static string Money(decimal value) => $"Rs. {value:0.00}";

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

    private static Control ChartCard(string title, string subtitle, decimal billed, decimal collected, decimal profit)
    {
        var chart = new Grid { Height = 300, RowDefinitions = new RowDefinitions("*,Auto") };
        var plot = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*"), RowDefinitions = new RowDefinitions("*,*,*,*,*") };
        for (var i = 0; i < 5; i++)
        {
            var line = new Border { BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1) };
            Grid.SetColumn(line, 1); Grid.SetRow(line, i); plot.Children.Add(line);
            var label = Ui.Text(i == 0 ? "216" : i == 1 ? "200" : i == 2 ? "150" : i == 3 ? "50" : "0", 11, color: Ui.Muted);
            Grid.SetColumn(label, 0); Grid.SetRow(label, i); plot.Children.Add(label);
        }
        var max = Math.Max(1m, Math.Max(billed, Math.Max(collected, profit)));
        var bars = new Grid { Width = 90, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, ColumnDefinitions = new ColumnDefinitions("*,8,*,8,*") };
        Border Bar(decimal value, string color) => new() { Width = 10, Height = (double)(180m * value / max), CornerRadius = new CornerRadius(5), Background = Brush.Parse(color), VerticalAlignment = VerticalAlignment.Bottom };
        var billedBar = Bar(billed, "#3B82F6"); var collectedBar = Bar(collected, "#22C55E"); var profitBar = Bar(profit, "#7C3AED");
        Grid.SetColumn(billedBar, 0); Grid.SetColumn(collectedBar, 2); Grid.SetColumn(profitBar, 4);
        bars.Children.Add(billedBar); bars.Children.Add(collectedBar); bars.Children.Add(profitBar);
        Grid.SetColumn(bars, 1); Grid.SetRowSpan(bars, 5); plot.Children.Add(bars);
        chart.Children.Add(plot);
        var legend = Ui.Wrap(Legend("#3B82F6", "Billed"), Legend("#22C55E", "Collected"), Legend("#7C3AED", "Profit"));
        legend.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(legend, 1); chart.Children.Add(legend);
        return Ui.Card(Ui.Stack(10, Ui.Columns("*,Auto", Ui.Stack(4, Ui.Text(title, 16, true), Ui.Text(subtitle, 12, color: Ui.Muted)), Ui.Button("↓ Export CSV", () => { })), chart), 20);
    }

    private static Control DonutCard()
    {
        var donut = new Grid { Width = 230, Height = 230 };
        donut.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Fill = Brush.Parse("#EF4444") });
        donut.Children.Add(new Border { Background = Brush.Parse("#22C55E"), Width = 230, Height = 115, VerticalAlignment = VerticalAlignment.Bottom });
        donut.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Fill = Brush.Parse("#FBF5FF"), Width = 120, Height = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        donut.Children.Add(Ui.Text("50%", 14, true, Brushes.White));
        var legend = Ui.Stack(14, Legend("#22C55E", "Paid  1  (50.0%)"), Legend("#F59E0B", "Partial  0  (0.0%)"), Legend("#EF4444", "Unpaid  1  (50.0%)"), Ui.Text("2 total invoices", 13, color: Ui.Muted));
        return Ui.Card(Ui.Stack(10, Ui.Text("Payment Status Breakdown", 16, true), Ui.Columns("260,30,*", donut, new Border(), legend)), 20);
    }

    private static Control CustomerRevenueCard(string[][] rows)
    {
        var name = rows.Length > 1 && rows[1].Length > 0 ? rows[1][0] : "Cash";
        var amount = rows.Length > 1 && rows[1].Length > 3 ? rows[1][3] : "Rs. 0.00";
        return Ui.Card(Ui.Stack(12, Ui.Columns("*,Auto", Ui.Text("Top 1 Customers by Revenue", 16, true), Ui.Button("↓ Export CSV", () => { })), Ui.Columns("130,*,110", Ui.Text(name, 13), new Border { Height = 22, CornerRadius = new CornerRadius(4), Background = Brush.Parse("#3B82F6") }, Ui.Text(amount, 13, true, Ui.Primary)), ReportTable("", rows, false)), 20);
    }

    private static Control ProductRevenueCard(string[][] rows)
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
        return Ui.Card(Ui.Stack(12, Ui.Columns("*,Auto", Ui.Text("Top 2 Products / Services by Revenue", 16, true), Ui.Wrap(Ui.Text("▣ Rank: Revenue", 12, true, Ui.Muted), Ui.Button("↓ Export CSV", () => { }))), list, ReportTable("", rows, false)), 20);
    }

    private static Control DailyReportCard(string[][] rows)
    {
        return Ui.Card(Ui.Stack(14, Ui.Columns("*,Auto", Ui.Text("Daily Sales & Profit", 16, true), Ui.Wrap(Ui.Button("↓ Export CSV", () => { }), Ui.Button("↓ Export PDF", () => { }))), Ui.Wrap(Ui.Button("Today", () => { }, true), Ui.Button("Last 30 days", () => { }), Ui.Button("Month & Year", () => { }), Ui.Button("Custom Range", () => { })), ReportTable("", rows, false)), 20);
    }

    private static Control Legend(string color, string text) => Ui.Columns("Auto,8,*", new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(3), Background = Brush.Parse(color), VerticalAlignment = VerticalAlignment.Center }, new Border(), Ui.Text(text, 13, true));

    private static Control ReportTable(string title, string[][] rows, bool wrapInCard)
    {
        var table = Ui.Stack(0);
        if (!string.IsNullOrWhiteSpace(title)) table.Children.Add(new Border { Padding = new Thickness(0, 0, 0, 10), Child = Ui.Columns("*,Auto", Ui.Text(title, 16, true), Ui.Button("↓ Export CSV", () => { })) });
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
    private static Control EmptyChart()
    {
        var rows = Ui.Stack(0);
        for (var i = 4; i >= 0; i--) rows.Children.Add(new Border { Height = 42, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Text(i.ToString(), 11, color: Ui.Muted) });
        rows.Children.Add(Ui.Columns("*,*,*,*,*,*", Enumerable.Range(0, 6).Select(i => (Control)Ui.Text(DateTime.Today.AddMonths(i - 5).ToString("MMM"), 11, color: Ui.Muted)).ToArray())); return rows;
    }
}
