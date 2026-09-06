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
        var report = Model.BuildReport(name);
        var body = Ui.Stack(20, Ui.Header(name, "", Ui.Button("Export CSV", async () => await ExportReportCsv(name)), Ui.Button("Export PDF", async () => await ExportReportPdf(name))));
        body.Children.Add(Ui.Stats(
            ("Total Billed", Money(report.Billed), "", "#002E78"),
            ("Total Collected", Money(report.Collected), "", "#2E7D32"),
            ("Outstanding", Money(report.Outstanding), "", "#C62828"),
            ("Invoices", report.InvoiceCount.ToString(), "", "#673AB7")));

        if (name == "Customers")
        {
            var statement = new ContentControl { Content = ReportTable("Top Customers", report.Rows) };
            body.Children.Add(Ui.Wrap(Ui.Button("Overview", () => statement.Content = ReportTable("Top Customers", Model.BuildReport("Customers").Rows)), Ui.Button("Statements", () => statement.Content = Ui.Stack(16, Ui.Field(new("Customer", "Select customer", "choice", new[] { "Select customer" }.Concat(Model.Customers.Select(c => c.Name)).ToArray())), ReportTable("Customer Statement", Model.BuildReport("Invoice Status").Rows)))));
            body.Children.Add(statement);
        }
        else
        {
            body.Children.Add(ReportTable(name switch
            {
                "Revenue" => "Revenue Summary",
                "Receivables" => "Aged Receivables",
                "Tax" => "Tax Collected by Date",
                "Products" => "Top Products",
                "Quotations" => "Quotation Conversion",
                "Invoice Status" => "Invoice Status",
                "Daily Report" => "Daily Sales",
                _ => name
            }, report.Rows));
        }

        return Ui.Scroll(body, 24);
    }

    private async Task ExportReportCsv(string name)
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


    private async Task ExportReportPdf(string name)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            Title = $"Export {name} PDF",
            SuggestedFileName = $"ledgernest-{name.ToLowerInvariant().Replace(" ", "-")}-report.pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = [new FilePickerFileType("PDF files") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }]
        });
        if (file == null) return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(Model.ExportReportPdf(name));
        ShowOverlay("Report Exported", Ui.Text($"Saved {file.Name}."), Ui.Button("Close", CloseOverlay, true));
    }

    private static string Money(decimal value) => $"₹ {value:0.00}";

    private static Control ReportTable(string title, string[][] rows)
    {
        var table = Ui.Stack(0);
        if (rows.Length == 0) return Ui.Card(Ui.Stack(18, Ui.Text(title, 18, true), Ui.Empty("No data for this period")));
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            table.Children.Add(new Border
            {
                Background = index == 0 ? Brush.Parse("#F5F5F5") : Ui.CardSurface,
                BorderBrush = Ui.Outline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12),
                Child = Ui.Columns(string.Join(",", row.Select(_ => "*")), row.Select(c => (Control)Ui.Text(c, 12, index == 0, index == 0 ? Ui.Muted : null)).ToArray())
            });
        }

        if (rows.Length == 1) table.Children.Add(Ui.Empty("No data for this period"));
        return Ui.Card(Ui.Stack(18, Ui.Text(title, 18, true), new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }), 16);
    }
    private static Control EmptyChart()
    {
        var rows = Ui.Stack(0);
        for (var i = 4; i >= 0; i--) rows.Children.Add(new Border { Height = 42, BorderBrush = Ui.Outline, BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Text(i.ToString(), 11, color: Ui.Muted) });
        rows.Children.Add(Ui.Columns("*,*,*,*,*,*", Enumerable.Range(0, 6).Select(i => (Control)Ui.Text(DateTime.Today.AddMonths(i - 5).ToString("MMM"), 11, color: Ui.Muted)).ToArray())); return rows;
    }
}
