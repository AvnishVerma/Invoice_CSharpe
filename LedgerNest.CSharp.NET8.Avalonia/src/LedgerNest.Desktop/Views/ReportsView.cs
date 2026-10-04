using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using LedgerNest.Desktop.Views;
using Avalonia.Platform.Storage;
using System.Text;
using LedgerNest.Domain;

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
        string[] names = ["Revenue", "Receivables", "Tax", "Customers", "Products", "Quotations", "Invoice Status", "Daily Report", "Inventory", "Barcodes"];
        return new ReportsPageView(new ReportsPageModel(names, reportTab, ReportContent, selected => { reportTab = selected; if (selected != "Customers") reportCustomerFilter = ""; }));
    }
    // Performs the report content action for this screen or workflow.
    private Control ReportContent(string name)
    {
        var report = Model.BuildReport(name);


        if (name == "Revenue")
        {
            return new RevenueReportView { DataContext = new RevenueReportViewModel(Model.BuildRevenueReport(), () => ExportReportCsv("Revenue"), () => ExportReportPdf("Revenue")) };
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
            return new SimpleReportView { DataContext = new SimpleReportViewModel(new ReportStatisticsModel([
                new("Total Tax Collected", CurrencyDisplay.Format(report.Collected, "0.00"), "account_balance_wallet", ReportMetricKind.Violet),
                new("Tax Rate Buckets", Math.Max(1, report.Rows.Length - 1).ToString(), "bar_chart", ReportMetricKind.Sky)]),
                new ReportTableViewModel("Tax Collected by Rate", report.Rows, true, () => ExportReportCsv("Tax"), () => ExportReportPdf("Tax")), "", []) };
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
            return new SimpleReportView { DataContext = new SimpleReportViewModel(new ReportStatisticsModel([
                new("Quotations Issued", "0", "request_quote", ReportMetricKind.Sky),
                new("Invoices in Period", report.InvoiceCount.ToString(), "receipt_long", ReportMetricKind.Green),
                new("Conversion Rate", "0.0%", "bar_chart", ReportMetricKind.Purple)]), null, "About Conversion Rate", [
                "Conversion rate = Invoices created ÷ Quotations issued × 100.",
                "A rate above 100% means more invoices were raised than quotations in the selected period.",
                "Note: this is a period-level ratio, not individual quote-to-invoice tracking."
            ]) };
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
        else if (name == "Barcodes")
        {
            return new BarcodeReportView(new BarcodeReportViewModel(Model.Products, ExportBarcodeLabelsPdf));
        }
        else
        {
            return new SimpleReportView { DataContext = new SimpleReportViewModel(new ReportStatisticsModel([]), new ReportTableViewModel(name, report.Rows, true, () => ExportReportCsv(name), () => ExportReportPdf(name)), "", []) };
        }


    }

    private async Task ExportBarcodeLabelsPdf(BarcodeLabelData[] labels)
    {
        var bytes = BarcodeLabelPdf.Create(labels, Model.InvoiceSetting("Currency").Value);
        if (OperatingSystem.IsMacOS())
        {
            var path = FilePickerHelpers.MacDownloadsPdfPath("ledgernest-barcode-labels");
            await File.WriteAllBytesAsync(path, bytes); Model.Status = $"Saved PDF to {path}"; return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions("Export Barcode Labels", "ledgernest-barcode-labels"));
        if (file == null) return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        ShowOverlay("Barcode Labels Exported", new DialogMessage($"Saved {labels.Length} labels to {file.Name}."), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
            ShowOverlay("Report Exported", new DialogMessage($"Saved {file.Name}."), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
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
            ShowOverlay("Report Exported", new DialogMessage($"Saved {file.Name}."), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
        }
        catch (Exception ex)
        {
            AppErrorLog.Write(ex, $"Exporting report PDF {name}");
            Model.Status = $"Could not save PDF. Log: {AppErrorLog.Path}";
        }
    }

}
