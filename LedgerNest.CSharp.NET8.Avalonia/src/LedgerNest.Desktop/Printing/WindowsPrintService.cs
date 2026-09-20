using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Docnet.Core;
using Docnet.Core.Models;

namespace LedgerNest.Desktop.Printing;

// Renders PDF pages with PDFium and sends them directly through the Windows print spooler.
[SupportedOSPlatform("windows6.1")]
public sealed class WindowsPrintService : IPrintService
{
    public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var defaultName = WindowsPrinterService.GetDefaultPrinter();
        IReadOnlyList<PrinterInfo> printers = WindowsPrinterService.GetInstalledPrinters()
            .Select(name => new PrinterInfo(name, name.Equals(defaultName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return Task.FromResult(printers);
    }

    public Task<PrinterInfo?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var name = WindowsPrinterService.GetDefaultPrinter();
        return Task.FromResult(name == null ? null : new PrinterInfo(name, true));
    }

    public async Task PrintPdfAsync(string pdfFilePath, string? printerName = null, PrintOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pdfFilePath)) throw new FileNotFoundException("The generated PDF file was not found.", pdfFilePath);
        options ??= new PrintOptions();
        if (options.ShowPrintDialog) throw new NotSupportedException("The native Windows print dialog is not available in this Avalonia Community build. Select a printer in PDF Settings.");
        var resolved = PlatformPrinterService.NormalizePrinterName(printerName);
        var installed = WindowsPrinterService.GetInstalledPrinters();
        if (resolved != null && !installed.Contains(resolved, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Printer '{resolved}' was not found.");
        if (resolved != null && PrinterClassifier.IsFilePrinter(resolved))
            throw new InvalidOperationException($"Printer '{resolved}' saves to a file. Select a physical printer for direct printing.");
        if (resolved == null)
        {
            var defaultPrinter = WindowsPrinterService.GetDefaultPrinter();
            resolved = PrinterClassifier.IsFilePrinter(defaultPrinter)
                ? installed.FirstOrDefault(name => !PrinterClassifier.IsFilePrinter(name))
                : defaultPrinter;
            if (string.IsNullOrWhiteSpace(resolved))
                throw new InvalidOperationException("No physical printer is available. Select or install a physical printer before printing.");
        }
        await Task.Run(() => Print(pdfFilePath, resolved, options, cancellationToken), cancellationToken);
    }

    private static void Print(string path, string? printerName, PrintOptions options, CancellationToken cancellationToken)
    {
        var bytes = File.ReadAllBytes(path);
        using var reader = DocLib.Instance.GetDocReader(bytes, new PageDimensions(2.0));
        var pageIndex = 0;
        using var document = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printerName)) document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid) throw new InvalidOperationException($"Printer '{printerName ?? "default"}' is unavailable.");
        document.PrinterSettings.Copies = (short)Math.Clamp(options.Copies ?? 1, 1, short.MaxValue);
        document.PrinterSettings.Collate = options.Collate;
        document.DefaultPageSettings.Landscape = options.Landscape;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        if (options.Silent) document.PrintController = new StandardPrintController();
        var configuredSize = document.PrinterSettings.PaperSizes.Cast<PaperSize>()
            .FirstOrDefault(size => size.PaperName.Equals(options.PaperSize, StringComparison.OrdinalIgnoreCase));
        if (configuredSize != null) document.DefaultPageSettings.PaperSize = configuredSize;
        document.PrintPage += (_, args) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = reader.GetPageReader(pageIndex);
            using var bitmap = CreateBitmap(page.GetImage(), page.GetPageWidth(), page.GetPageHeight());
            var bounds = args.MarginBounds;
            var scale = Math.Min(bounds.Width / (float)bitmap.Width, bounds.Height / (float)bitmap.Height);
            var width = bitmap.Width * scale;
            var height = bitmap.Height * scale;
            args.Graphics?.DrawImage(bitmap, bounds.Left + (bounds.Width - width) / 2, bounds.Top, width, height);
            pageIndex++;
            args.HasMorePages = pageIndex < reader.GetPageCount();
        };
        document.Print();
    }

    private static Bitmap CreateBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var sourceStride = width * 4;
            for (var row = 0; row < height; row++)
                Marshal.Copy(pixels, row * sourceStride, IntPtr.Add(data.Scan0, row * data.Stride), sourceStride);
        }
        finally { bitmap.UnlockBits(data); }
        return bitmap;
    }
}
