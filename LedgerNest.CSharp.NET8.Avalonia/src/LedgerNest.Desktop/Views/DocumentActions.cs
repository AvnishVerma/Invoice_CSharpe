using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void ShowDocumentPreview(UiRecord document)
    {
        static string Money(string value) => decimal.TryParse(value, out var amount) ? $"Rs. {amount:0.00}" : string.IsNullOrWhiteSpace(value) ? "Rs. 0.00" : value;
        var items = Model.PreviewItemsFor(document);
        Control itemRows = items.Length == 0
            ? Ui.Text("No items available", 13, color: Ui.Muted)
            : Ui.Stack(6, items.Select(item => Ui.Columns("*,Auto", Ui.Text($"{item.Description} x{item.Quantity:0.###}", 13), Ui.Text($"Rs. {item.LineTotal:0.00}", 13))).ToArray());
        var content = Ui.Stack(18,
            Ui.Text($"Invoice #{document.Name}", 24),
            Ui.Stack(4, Ui.Text($"Customer: {document["Customer"]}", 13), Ui.Text($"Date: {document["Date"]}", 13)),
            Ui.Stack(10, Ui.Text("Items:", 14, true), itemRows),
            new Border { Height = 1, Background = Ui.Outline },
            Ui.Stack(6,
                Ui.Columns("*,Auto", Ui.Text("Subtotal:", 13, true), Ui.Text(Money(document["Subtotal"].Length == 0 ? document["Total"] : document["Subtotal"]), 13)),
                Ui.Columns("*,Auto", Ui.Text($"Tax ({TaxLabel(document)}):", 13, true), Ui.Text(Money(document["Tax"]), 13)),
                Ui.Columns("*,Auto", Ui.Text("Total:", 14, true), Ui.Text(Money(document["Total"]), 14, true))));
        ShowOverlay("", content, Ui.Button("Close", CloseOverlay, true), width: 650);
    }

    internal async void ShowPdfPreview(UiRecord document)
    {
        string? path = null;
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            path = Path.Combine(Path.GetTempPath(), $"ledgernest-preview-{FilePickerHelpers.SanitizeFileName(document.Name)}-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            OpenPdfFile(path);
            Model.Status = $"Opened PDF preview for {document.Name}.";
            ShowOverlay("PDF Preview", Ui.Stack(14,
                Ui.Text($"PDF preview opened for {document.Name}.", 16, true),
                Ui.Text("Use Download PDF to save a copy of the generated file.")),
                Ui.Wrap(Ui.Button("Download PDF", async () => await DownloadDocumentPdf(document)), Ui.Button("Close", CloseOverlay, true)),
                width: 520);
        }
        catch (Exception ex)
        {
            NotifyError(path == null ? "Could not create PDF preview. The error has been logged." : $"Could not open PDF preview. Preview file: {path}. The error has been logged.", ex, $"Previewing invoice PDF {document.Name}");
        }
    }

    private static string TaxLabel(UiRecord document)
    {
        var total = decimal.TryParse(document["Total"], out var totalValue) ? totalValue : 0m;
        var tax = decimal.TryParse(document["Tax"], out var taxValue) ? taxValue : 0m;
        var beforeTax = total - tax;
        if (beforeTax <= 0m || tax <= 0m) return "0%";
        return Math.Round(tax * 100m / beforeTax).ToString("0") + "%";
    }

    private async Task DownloadDocumentPdf(UiRecord document)
    {
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            if (OperatingSystem.IsMacOS())
            {
                var path = FilePickerHelpers.MacDownloadsPdfPath(document.Name);
                await File.WriteAllBytesAsync(path, bytes);
                Model.Status = $"Saved PDF to {path}";
                return;
            }
            var file = await StorageProvider.SaveFilePickerAsync(FilePickerHelpers.PdfSaveOptions($"Save {document.Name} PDF", document.Name));
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await LedgerNest.Infrastructure.BackupStreamWriter.WriteAsync(stream, bytes);
            Model.Status = $"Saved {file.Name}.";
        }
        catch (Exception ex)
        {
            NotifyError("Could not save PDF. The error has been logged.", ex, $"Saving invoice PDF {document.Name}");
        }
    }

    internal async Task PrintDocumentPdf(UiRecord document)
    {
        string? path = null;
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            path = Path.Combine(Path.GetTempPath(), $"ledgernest-{FilePickerHelpers.SanitizeFileName(document.Name)}-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            await SendPdfToPrinter(path);
            Model.Status = $"Sent {document.Name} to printer.";
        }
        catch (Exception ex)
        {
            NotifyError(path == null ? "Could not create print PDF. The error has been logged." : $"Could not send PDF to printer. Saved print file: {path}. The error has been logged.", ex, $"Printing invoice PDF {document.Name}");
        }
    }

    private static void OpenPdfFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return;
        }

        var command = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        using var process = Process.Start(new ProcessStartInfo(command, path) { UseShellExecute = false, CreateNoWindow = true });
        if (process == null) throw new InvalidOperationException("The system PDF preview command could not be started.");
    }

    private static async Task SendPdfToPrinter(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var process = Process.Start(new ProcessStartInfo(path) { Verb = "print", UseShellExecute = true, CreateNoWindow = true });
            if (process == null) throw new InvalidOperationException("The system print command could not be started.");
            return;
        }

        var command = OperatingSystem.IsMacOS() ? "lp" : File.Exists("/usr/bin/lp") || File.Exists("/bin/lp") ? "lp" : "lpr";
        using var print = Process.Start(new ProcessStartInfo(command, path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        });
        if (print == null) throw new InvalidOperationException("The system print command could not be started.");
        await print.WaitForExitAsync();
        if (print.ExitCode != 0)
        {
            var error = await print.StandardError.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(error)) error = await print.StandardOutput.ReadToEndAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Print command exited with code {print.ExitCode}." : error.Trim());
        }
    }

    private void DeleteDocumentFromDashboard(UiRecord document)
    {
        Confirm("Confirm Delete", $"Move {document.Name} to trash?", () =>
        {
            Model.SetDocumentTrash(document, true);
            page.Content = Dashboard();
        });
    }

    private byte[]? TryExportDocumentPdf(UiRecord document)
    {
        try
        {
            return Model.ExportDocumentPdf(document);
        }
        catch (Exception ex)
        {
            NotifyError($"Could not export {document.Name}. The error has been logged.", ex, $"Exporting invoice PDF {document.Name}");
            return null;
        }
    }

    private void NotifyError(string message, Exception exception, string context)
    {
        AppErrorLog.Write(exception, context);
        Model.Status = $"{message} Log: {AppErrorLog.Path}";
    }
}
