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
        string[] names = ["Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report", "Inventory"];
        return new ReportsPageView(new ReportsPageModel(names, reportTab, ReportContent, selected => { reportTab = selected; if (selected != "Customers") reportCustomerFilter = ""; }));
    }
    // Performs the report content action for this screen or workflow.
    private Control ReportContent(string name)
    {
        var report = Model.BuildReport(name);
        var body = Ui.Stack(name == "Revenue" ? 12 : 20);

        if (name == "Revenue")
        {
            var revenue = Model.BuildRevenueReport();
            body.Children.Add(RevenueStats(revenue));
            if (revenue.MissingCostItemCount > 0) body.Children.Add(MissingCostBanner(revenue.MissingCostItemCount));
            body.Children.Add(RevenueChartCard(revenue));
            body.Children.Add(RevenueBreakdownCard(revenue));
        }
        else if (name == "Receivables")
        {
            return new ReceivablesReportView(new ReceivablesReportViewModel(
                Model.BuildReceivablesReport(),
                () => ExportReportCsv("Receivables"),
                () => ExportReportPdf("Receivables")));
        }
        else if (name == "Tax")
        {
            body.Children.Add(ReportStats(("Total Tax Collected", Money(report.Collected), "account_balance_wallet", "#8B5CF6"), ("Tax Rate Buckets", Math.Max(1, report.Rows.Length - 1).ToString(), "bar_chart", "#0284C7")));
            body.Children.Add(ReportTable("Tax Collected by Rate", report.Rows, true, "Tax"));
        }
        else if (name == "Customers")
        {
            return new CustomerReportView(new CustomerReportViewModel(
                Model.BuildCustomerReport(),
                reportCustomerFilter,
                () => ExportReportCsv("Customers"),
                () => ExportReportPdf("Customers")));
        }
        else if (name == "Products")
        {
            return new ProductReportView(new ProductReportViewModel(
                Model.BuildProductReport(),
                () => ExportReportCsv("Products"),
                () => ExportReportPdf("Products")));
        }
        else if (name == "Quotations")
        {
            body.Children.Add(ReportStats(("Quotations Issued", "0", "request_quote", "#0284C7"), ("Invoices in Period", report.InvoiceCount.ToString(), "receipt_long", "#16A34A"), ("Conversion Rate", "0.0%", "bar_chart", "#7C3AED")));
            body.Children.Add(Ui.Card(Ui.Stack(9.6, Ui.LocalText("About Conversion Rate", 16, true), Ui.LocalText("Conversion rate = Invoices created ÷ Quotations issued × 100.", 13, color: Ui.Muted), Ui.LocalText("A rate above 100% means more invoices were raised than quotations in the selected period." , 13, color: Ui.Muted), Ui.LocalText("Note: this is a period-level ratio, not individual quote-to-invoice tracking.", 13, color: Ui.Muted)), 20));
        }
        else if (name == "Invoice Status")
        {
            return new InvoiceStatusReportView(new InvoiceStatusReportViewModel(
                Model.BuildInvoiceStatusReport(),
                () => ExportReportCsv("Invoice Status"),
                () => ExportReportPdf("Invoice Status")));
        }
        else if (name == "Daily Report")
        {
            return new DailyReportView(new DailyReportViewModel(
                Model.BuildDailySalesReport(),
                () => ExportReportCsv("Daily Report"),
                () => ExportReportPdf("Daily Report")));
        }
        else if (name == "Inventory")
        {
            return new InventoryReportView(new InventoryReportViewModel(
                Model.BuildInventoryReport(),
                () => ExportReportCsv("Inventory"),
                () => ExportReportPdf("Inventory")));
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
    private static string Money(decimal value) => CurrencyDisplay.Format(value, "0.00");

    // Builds the six KPI tiles shown at the top of the revenue report.
    private static Control RevenueStats(RevenueReportSnapshot report)
    {
        var stats = new[]
        {
            ("Total Billed", Money(report.Billed), "receipt_long", "#0D47A1"),
            ("Total Collected", Money(report.Collected), "check_circle", "#16A34A"),
            ("Outstanding", Money(report.Outstanding), "schedule", "#E53935"),
            ("Avg Invoice Value", Money(report.AverageInvoiceValue), "trending_up", "#7C3AED"),
            ("Total Profit", Money(report.TotalProfit), "savings", report.TotalProfit < 0 ? "#E53935" : "#16A34A"),
            ("Realized Profit", Money(report.RealizedProfit), "payments", report.RealizedProfit < 0 ? "#E53935" : "#16A34A")
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*"),
            RowDefinitions = new RowDefinitions("Auto,12,Auto")
        };
        for (var i = 0; i < stats.Length; i++)
        {
            var stat = stats[i];
            var tile = Ui.Card(Ui.Columns("Auto,12,*", new Border
            {
                Width = 28.8,
                Height = 28.8,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.Parse(stat.Item4), .12),
                Child = Ui.Icon(stat.Item3, 18, Brush.Parse(stat.Item4))
            }, new Border(), Ui.Stack(5.6, Ui.Text(stat.Item1, 12, color: Ui.Muted), Ui.Text(stat.Item2, 18, true, Brush.Parse(stat.Item4)))), 16);
            tile.Background = Ui.Palette("#FBF5FF", "#202B36");
            Grid.SetColumn(tile, i % 3 * 2);
            Grid.SetRow(tile, i / 3 * 2);
            grid.Children.Add(tile);
        }
        return grid;
    }

    // Builds the purchase-price warning shown when profit is based on incomplete cost data.
    private static Control MissingCostBanner(int itemCount)
    {
        var noun = itemCount == 1 ? "item" : "items";
        var verb = itemCount == 1 ? "has" : "have";
        var pronoun = itemCount == 1 ? "that item" : "those items";
        return new Border
        {
            Background = Ui.Palette("#FFFBEB", "#202B36"),
            BorderBrush = Brush.Parse("#FDE68A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9.6, 8),
            Child = Ui.Columns("Auto,8,*", Ui.Icon("warning_amber", 18, Brush.Parse("#D97706")), new Border(),
                Ui.Text($"{itemCount} {noun} sold in this period {verb} no purchase price set — profit/margin is understated for {pronoun} until a purchase price is added to the product.", 12, color: Brush.Parse("#92400E")))
        };
    }

    // Performs the report stats action for this screen or workflow.
    private static Control ReportStats(params (string Label, string Value, string Icon, string Color)[] stats)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", stats.Select(_ => "*"))) };
        for (var i = 0; i < stats.Length; i++)
        {
            var s = stats[i];
            var tile = Ui.Card(Ui.Columns("Auto,8,*", new Border
            {
                Width = 28.8,
                Height = 28.8,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.Parse(s.Color), .12),
                Child = Ui.Icon(s.Icon, 18, Brush.Parse(s.Color))
            }, new Border(), Ui.Stack(4, Ui.Text(s.Label, 11, true, Ui.Muted), Ui.Text(s.Value, 15, true, Brush.Parse(s.Color)))), 12);
            tile.Background = Ui.Palette("#FBF5FF", "#202B36");
            tile.Margin = new Thickness(i == 0 ? 0 : 10, 0, i == stats.Length - 1 ? 0 : 10, 0);
            Grid.SetColumn(tile, i);
            grid.Children.Add(tile);
        }
        return grid;
    }

    // Builds the grouped monthly chart using the ScottPlot Avalonia NuGet control.
    private Control RevenueChartCard(RevenueReportSnapshot report)
    {
        Control chartContent;
        if (report.Months.Length == 0)
        {
            chartContent = Ui.Empty("No invoice data for this period", "Create an invoice to populate the revenue trend.", "bar_chart");
        }
        else
        {
            var chart = new ScottPlot.Avalonia.AvaPlot { Height = 250 };
            for (var i = 0; i < report.Months.Length; i++)
            {
                var x = i + 1d;
                var billed = chart.Plot.Add.Bar(x - .018, (double)report.Months[i].Billed);
                billed.Color = ScottPlot.Color.FromHex("#3B82F6");
                billed.Bars[0].Size = .014;
                var collected = chart.Plot.Add.Bar(x, (double)report.Months[i].Collected);
                collected.Color = ScottPlot.Color.FromHex("#22C55E");
                collected.Bars[0].Size = .014;
                var profit = chart.Plot.Add.Bar(x + .018, (double)report.Months[i].Profit);
                profit.Color = ScottPlot.Color.FromHex("#7C3AED");
                profit.Bars[0].Size = .014;
            }
            chart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
                Enumerable.Range(1, report.Months.Length).Select(value => (double)value).ToArray(),
                report.Months.Select(month => month.Month.ToString("MMM yy")).ToArray());
            chart.Plot.Axes.SetLimitsX(.4, Math.Max(1, report.Months.Length) + .6);
            chart.Plot.Axes.Left.Min = Math.Min(0, (double)report.Months.Min(month => month.Profit) * 1.15);
            chart.Plot.Grid.XAxisStyle.IsVisible = false;
            chart.Plot.Legend.IsVisible = false;
            ReportChartTheme.Apply(chart);
            chart.Refresh();
            chartContent = chart;
        }

        var legend = Ui.Wrap(Legend("#3B82F6", "Billed"), Legend("#22C55E", "Collected"), Legend("#7C3AED", "Profit"));
        legend.HorizontalAlignment = HorizontalAlignment.Center;
        var card = Ui.Card(Ui.Stack(8,
            Ui.Columns("*,Auto", Ui.Stack(3.2, Ui.LocalText("Monthly Revenue Trend", 16, true), Ui.Text($"{report.InvoiceCount} invoices in period · INR", 12, color: Ui.Muted)),
                Ui.Wrap(Ui.Button("↓  Export CSV", async () => await ExportReportCsv("Revenue")), Ui.Button("↓  Export PDF", async () => await ExportReportPdf("Revenue")))),
            chartContent,
            legend), 20);
        card.Background = Ui.Palette("#FBF5FF", "#202B36");
        return card;
    }

    // Builds the monthly revenue breakdown and totals table beneath the chart.
    private static Control RevenueBreakdownCard(RevenueReportSnapshot report)
    {
        var table = Ui.Stack(0);
        string[] headers = ["Month", "Invoices", "Billed", "Collected", "Outstanding", "COGS", "Profit", "Margin"];
        table.Children.Add(RevenueBreakdownRow(headers, true));
        foreach (var month in report.Months)
        {
            table.Children.Add(RevenueBreakdownRow([
                month.Month.ToString("MMM yyyy"), month.InvoiceCount.ToString(), Money(month.Billed), Money(month.Collected),
                Money(month.Outstanding), Money(month.Cogs), Money(month.Profit), $"{month.MarginPercent:0.#}%"]));
        }
        if (report.Months.Length == 0)
            table.Children.Add(new Border { Padding = new Thickness(11.2, 14.4), Child = Ui.LocalText("No monthly revenue data for this period.", 13, color: Ui.Muted) });
        else
        {
            var revenue = report.Months.Sum(month => month.Profit + month.Cogs);
            var totalMargin = revenue == 0 ? 0 : report.Months.Sum(month => month.Profit) * 100 / revenue;
            table.Children.Add(RevenueBreakdownRow([
                "Total", report.Months.Sum(month => month.InvoiceCount).ToString(), Money(report.Months.Sum(month => month.Billed)),
                Money(report.Months.Sum(month => month.Collected)), Money(report.Months.Sum(month => month.Outstanding)),
                Money(report.Months.Sum(month => month.Cogs)), Money(report.Months.Sum(month => month.Profit)), $"{totalMargin:0.#}%"], false, true));
        }
        var scroll = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var card = Ui.Card(Ui.Stack(9.6, Ui.LocalText("Monthly Breakdown", 16, true), scroll), 20);
        card.Background = Ui.Palette("#FBF5FF", "#202B36");
        return card;
    }

    // Builds one aligned header, detail, or total row for the monthly breakdown.
    private static Control RevenueBreakdownRow(string[] values, bool header = false, bool total = false)
    {
        var columns = values.Select((value, index) => (Control)Ui.Text(
            header ? value.ToUpperInvariant() : value,
            header ? 11 : 13,
            header || total || index == 0,
            header ? Ui.Muted : index == 6 ? Brush.Parse("#16A34A") : index == 2 ? Brush.Parse("#2563EB") : null)).ToArray();
        return new Border
        {
            MinWidth = 820,
            Background = header || total ? Ui.Canvas : Ui.Palette("#FBF5FF", "#202B36"),
            BorderBrush = Ui.Outline,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, header ? 10 : 13),
            Child = Ui.Columns("1.05*,.65*,*,*,*,*,*,.65*", columns)
        };
    }

    // Performs the legend action for this screen or workflow.
    private static Control Legend(string color, string text) => Ui.Columns("Auto,8,*", new Border { Width = 13, Height = 13, CornerRadius = new CornerRadius(3), Background = Brush.Parse(color), VerticalAlignment = VerticalAlignment.Center }, new Border(), Ui.Text(text, 13, true));

    // Performs the report table action for this screen or workflow.
    private Control ReportTable(string title, string[][] rows, bool wrapInCard, string reportName = "")
    {
        var table = Ui.Stack(0);
        if (!string.IsNullOrWhiteSpace(title)) table.Children.Add(new Border { Padding = new Thickness(0, 0, 0, 8), Child = Ui.Columns("*,Auto", Ui.LocalText(title, 16, true), Ui.Wrap(Ui.Button("↓ Export CSV", async () => await ExportReportCsv(string.IsNullOrWhiteSpace(reportName) ? title : reportName)), Ui.Button("↓ Export PDF", async () => await ExportReportPdf(string.IsNullOrWhiteSpace(reportName) ? title : reportName)))) });
        if (rows.Length == 0) table.Children.Add(Ui.Empty("No data for this period"));
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            table.Children.Add(new Border
            {
                Background = index % 2 == 0 ? Ui.Canvas : Ui.CardSurface,
                BorderBrush = Ui.Outline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, index == 0 ? 10 : 14),
                Child = Ui.Columns(string.Join(",", row.Select(_ => "*")), row.Select((c, i) => (Control)Ui.Text(index == 0 ? c.ToUpperInvariant() : c, index == 0 ? 11 : 13, index == 0 || i == 0, index == 0 ? Ui.Muted : c.Contains(CurrencyDisplay.Symbol()) && c.Contains("70") ? Brush.Parse("#EF4444") : c.Contains(CurrencyDisplay.Symbol()) ? Brush.Parse("#16A34A") : null)).ToArray())
            });
        }
        table.Children.Add(new Border { Padding = new Thickness(11.2, 9.6), Child = Ui.Columns("*,Auto", Ui.LocalText("Rows per page: 10      1 – 1 of 1", 13), Ui.LocalText("‹   Page 1 of 1   ›", 13, true)) });
        var scroll = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        return wrapInCard ? Ui.Card(scroll, 20) : scroll;
    }
    // Performs the empty chart action for this screen or workflow.
    private static Control EmptyChart()
    {
        var rows = Ui.Stack(0);
        for (var i = 4; i >= 0; i--) rows.Children.Add(new Border { Height = 33.6, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Text(i.ToString(), 11, color: Ui.Muted) });
        rows.Children.Add(Ui.Columns("*,*,*,*,*,*", Enumerable.Range(0, 6).Select(i => (Control)Ui.Text(DateTime.Today.AddMonths(i - 5).ToString("MMM"), 11, color: Ui.Muted)).ToArray())); return rows;
    }
}
