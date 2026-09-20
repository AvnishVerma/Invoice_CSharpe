using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Docnet.Core;
using Docnet.Core.Models;
using System.Runtime.InteropServices;
using LedgerNest.Desktop.Views;
using LedgerNest.Desktop.Printing;

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

    // Renders the generated PDF inside LedgerNest without opening an external viewer.
    internal async Task ShowPdfPreviewAsync(UiRecord document)
    {
        try
        {
            Model.Status = "Rendering PDF preview…";
            var bytes = Model.ExportDocumentPdf(document);
            var rendered = await Task.Run(() => RenderPdfPages(bytes));
            var preview = new PdfPreviewModel
            {
                Title = $"{document.Name} PDF Preview",
                PageCountText = rendered.Count == 1 ? "1 page" : $"{rendered.Count} pages",
                MaxPreviewHeight = Math.Max(360, Bounds.Height * .68)
            };
            foreach (var page in rendered) preview.Pages.Add(CreatePreviewBitmap(page.Pixels, page.Width, page.Height));
            ShowOverlay("PDF Preview", new PdfPreviewView(preview),
                Ui.Wrap(
                    Ui.Button("Download PDF", async () => await DownloadDocumentPdf(document)),
                    Ui.Button("Print", async () => await PrintDocumentAsync(document), true),
                    Ui.Button("Close", CloseOverlay)),
                width: 860);
            Model.Status = $"PDF preview ready for {document.Name}.";
        }
        catch (Exception ex)
        {
            NotifyError("Could not render the PDF preview. The error has been logged.", ex, $"Previewing invoice PDF {document.Name}");
        }
    }

    internal static List<RenderedPdfPage> RenderPdfPages(byte[] bytes)
    {
        using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1.65));
        var pages = new List<RenderedPdfPage>();
        for (var index = 0; index < reader.GetPageCount(); index++)
        {
            using var page = reader.GetPageReader(index);
            pages.Add(new RenderedPdfPage(page.GetImage(), page.GetPageWidth(), page.GetPageHeight()));
        }
        if (pages.Count == 0) throw new InvalidDataException("The PDF contains no pages.");
        return pages;
    }

    private static WriteableBitmap CreatePreviewBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var locked = bitmap.Lock();
        var sourceStride = width * 4;
        for (var row = 0; row < height; row++)
            Marshal.Copy(pixels, row * sourceStride, IntPtr.Add(locked.Address, row * locked.RowBytes), Math.Min(sourceStride, locked.RowBytes));
        return bitmap;
    }

    internal sealed record RenderedPdfPage(byte[] Pixels, int Width, int Height);

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
    internal async Task DownloadDocumentPdf(UiRecord document)
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

    // Generates a temporary PDF, sends it to the active native print service, and always removes the temporary file.
    internal async Task PrintDocumentAsync(UiRecord document)
    {
        string? path = null;
        try
        {
            Model.Status = "Generating invoice PDF…";
            var bytes = Model.ExportDocumentPdf(document);
            path = await pdfGenerator.GenerateAsync(bytes, document.Name);
            var print = Model.GetPrintConfiguration();
            var printerName = print.PrinterName;
            var options = print.Options;
            if (options.ShowPrintDialog)
            {
                var selection = await ShowPrinterSelectionDialogAsync(printerName);
                if (!selection.Confirmed)
                {
                    Model.Status = "Printing cancelled.";
                    return;
                }
                printerName = selection.PrinterName;
                options = new PrintOptions
                {
                    Silent = true,
                    Landscape = options.Landscape,
                    PaperSize = options.PaperSize,
                    PaperSource = options.PaperSource,
                    Copies = options.Copies,
                    Collate = options.Collate
                };
            }
            await printService.PrintPdfAsync(path, printerName, options);
            Model.Status = $"Sent {document.Name} to the printer.";
        }
        catch (Exception ex)
        {
            NotifyError("Could not send the invoice to the default printer. The error has been logged.", ex, $"Direct-printing invoice {document.Name}");
        }
        finally
        {
            if (path != null)
            {
                try { File.Delete(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { AppErrorLog.Write(ex, $"Cleaning temporary print file {path}"); }
            }
        }
    }

    // Shows a responsive printer chooser when the per-job selection setting is enabled.
    private async Task<(bool Confirmed, string? PrinterName)> ShowPrinterSelectionDialogAsync(string? configuredPrinter)
    {
        var discovered = await printService.GetPrintersAsync();
        var choices = new[] { PlatformPrinterService.DefaultPrinter }
            .Concat(discovered.Where(printer => !PrinterClassifier.IsFilePrinter(printer.Name)).Select(printer => printer.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var selected = choices.Contains(configuredPrinter, StringComparer.OrdinalIgnoreCase)
            ? configuredPrinter
            : discovered.FirstOrDefault(printer => printer.IsDefault && !PrinterClassifier.IsFilePrinter(printer.Name))?.Name ?? choices[0];
        var picker = new ComboBox
        {
            ItemsSource = choices,
            SelectedItem = selected,
            MinWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var dialog = new Window
        {
            Title = "Select Printer",
            Width = 480,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var cancel = Ui.Button("Cancel", () => dialog.Close((false, (string?)null)));
        var print = Ui.Button("Print", () => dialog.Close((true, picker.SelectedItem?.ToString())), true);
        dialog.Content = new Border
        {
            Padding = new Thickness(24),
            Child = Ui.Rows("Auto,*,Auto",
                Ui.Text("Choose a printer", 20, true),
                new StackPanel { Spacing = 8, Margin = new Thickness(0, 20), Children = { Ui.Text("Printer", 12, true, Ui.Muted), picker } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, print } })
        };
        return await dialog.ShowDialog<(bool Confirmed, string? PrinterName)>(this);
    }

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
