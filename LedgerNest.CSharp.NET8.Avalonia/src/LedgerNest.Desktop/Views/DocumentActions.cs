using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Docnet.Core;
using Docnet.Core.Models;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;
using System.ComponentModel;
using System.Diagnostics;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    // Performs the show document preview action for this screen or workflow.
    internal void ShowDocumentPreview(UiRecord document)
    {
        // Performs the money action for this screen or workflow.
        static string Money(string value) => decimal.TryParse(value, out var amount) ? $"Rs. {amount:0.00}" : string.IsNullOrWhiteSpace(value) ? "Rs. 0.00" : value;
        var preview = new DocumentPreviewModel
        {
            Title = $"Invoice #{document.Name}",
            CustomerText = $"Customer: {document["Customer"]}",
            DateText = $"Date: {document["Date"]}",
            SubtotalText = Money(document["Subtotal"].Length == 0 ? document["Total"] : document["Subtotal"]),
            TaxLabelText = $"Tax ({TaxLabel(document)}):",
            TaxText = Money(document["Tax"]),
            TotalText = Money(document["Total"])
        };
        foreach (var item in Model.PreviewItemsFor(document))
        {
            preview.Items.Add(new DocumentPreviewItemModel($"{item.Description} x{item.Quantity:0.###}", $"Rs. {item.LineTotal:0.00}"));
        }
        ShowOverlay("", new DocumentPreviewView(preview), Ui.Button("Close", CloseOverlay, true), width: 650);
    }

    // Performs the show pdf preview action for this screen or workflow.
    internal async void ShowPdfPreview(UiRecord document)
    {
        string? path = null;
        try
        {
            var bytes = TryExportDocumentPdf(document);
            if (bytes == null) return;
            path = Path.Combine(Path.GetTempPath(), $"ledgernest-preview-{FilePickerHelpers.SanitizeFileName(document.Name)}-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            var pages = RenderPdfPreviewPages(bytes);
            var previewModel = new PdfPreviewModel
            {
                Title = $"{document.Name} PDF",
                PageCountText = pages.Count == 1 ? "1 page" : $"{pages.Count} pages",
                MaxPreviewHeight = Math.Max(360, Bounds.Height * .72)
            };
            foreach (var page in pages) previewModel.Pages.Add(page);
            Model.Status = $"Rendered PDF preview for {document.Name}.";
            ShowOverlay("PDF Preview", new PdfPreviewView(previewModel),
                Ui.Wrap(Ui.Button("Download PDF", async () => await DownloadDocumentPdf(document)), Ui.Button("Open Externally", () => { if (path != null) OpenPdfFile(path); }), Ui.Button("Close", CloseOverlay, true)),
                width: 860);
        }
        catch (Exception ex)
        {
            NotifyError(path == null ? "Could not create PDF preview. The error has been logged." : $"Could not render PDF preview. Preview file: {path}. The error has been logged.", ex, $"Previewing invoice PDF {document.Name}");
        }
    }

    // Performs the render pdf preview pages action for this screen or workflow.
    private static List<Image> RenderPdfPreviewPages(byte[] bytes)
    {
        var pages = new List<Image>();
        using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.65));
        for (var pageIndex = 0; pageIndex < reader.GetPageCount(); pageIndex++)
        {
            using var pageReader = reader.GetPageReader(pageIndex);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var pixels = pageReader.GetImage();
            var source = CreateBitmapFromBgra(pixels, width, height);
            pages.Add(new Image { Source = source, Stretch = Stretch.Uniform, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Center });
        }
        return pages.Count == 0 ? [new Image { Height = 1 }] : pages;
    }

    // Performs the create bitmap from bgra action for this screen or workflow.
    private static WriteableBitmap CreateBitmapFromBgra(byte[] pixels, int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var locked = bitmap.Lock();
        var sourceStride = width * 4;
        var rows = Math.Min(height, pixels.Length / sourceStride);
        for (var row = 0; row < rows; row++)
            Marshal.Copy(pixels, row * sourceStride, IntPtr.Add(locked.Address, row * locked.RowBytes), Math.Min(sourceStride, locked.RowBytes));
        return bitmap;
    }

    // Performs the tax label action for this screen or workflow.
    private static string TaxLabel(UiRecord document)
    {
        var total = decimal.TryParse(document["Total"], out var totalValue) ? totalValue : 0m;
        var tax = decimal.TryParse(document["Tax"], out var taxValue) ? taxValue : 0m;
        var beforeTax = total - tax;
        if (beforeTax <= 0m || tax <= 0m) return "0%";
        return Math.Round(tax * 100m / beforeTax).ToString("0") + "%";
    }

    // Performs the download document pdf action for this screen or workflow.
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

    // Performs the print document pdf action for this screen or workflow.
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

    // Performs the open pdf file action for this screen or workflow.
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

    // Performs the send pdf to printer action for this screen or workflow.
    private static async Task SendPdfToPrinter(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            await SendPdfToWindowsPrinter(path);
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

    // Performs the Windows PDF print action by using the registered print verb or Edge when no PDF app is associated.
    private static async Task SendPdfToWindowsPrinter(string path)
    {
        try
        {
            using var print = Process.Start(new ProcessStartInfo(path)
            {
                Verb = "print",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            if (print == null) throw new InvalidOperationException("The system PDF print command could not be started.");
            return;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1155)
        {
            await PrintPdfWithEdge(path, ex);
        }
    }

    // Performs the Edge fallback print action for Windows machines without a PDF file association.
    private static async Task PrintPdfWithEdge(string path, Exception originalException)
    {
        var edge = FindMicrosoftEdgeExecutable();
        if (edge == null)
            throw new InvalidOperationException("Windows has no PDF print handler for .pdf files, and Microsoft Edge was not found. Install or associate a PDF reader, or install Microsoft Edge to enable direct PDF printing.", originalException);

        using var print = Process.Start(new ProcessStartInfo(edge)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"--kiosk-printing --disable-print-preview {QuoteArgument(new Uri(path).AbsoluteUri)}"
        });
        if (print == null) throw new InvalidOperationException("Microsoft Edge PDF printing could not be started.", originalException);

        await Task.Delay(TimeSpan.FromSeconds(3));
    }

    // Performs the Microsoft Edge executable discovery action for Windows print fallback.
    private static string? FindMicrosoftEdgeExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    // Performs command-line argument quoting for file URLs passed to print helper processes.
    private static string QuoteArgument(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    // Performs the delete document from dashboard action for this screen or workflow.
    private void DeleteDocumentFromDashboard(UiRecord document)
    {
        Confirm("Confirm Delete", $"Move {document.Name} to trash?", () =>
        {
            Model.SetDocumentTrash(document, true);
            page.Content = Dashboard();
        });
    }

    // Performs the try export document pdf action for this screen or workflow.
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

    // Performs the notify error action for this screen or workflow.
    private void NotifyError(string message, Exception exception, string context)
    {
        AppErrorLog.Write(exception, context);
        Model.Status = $"{message} Log: {AppErrorLog.Path}";
    }
}
