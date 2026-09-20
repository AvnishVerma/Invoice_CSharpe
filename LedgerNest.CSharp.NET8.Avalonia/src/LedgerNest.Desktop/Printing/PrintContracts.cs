namespace LedgerNest.Desktop.Printing;

// Describes one printer returned by the active operating system.
public sealed record PrinterInfo(string Name, bool IsDefault = false, string? Description = null, string? DriverName = null, string? PortName = null);

// Identifies virtual destinations that create files instead of producing a physical printout.
public static class PrinterClassifier
{
    private static readonly string[] FilePrinterNames =
    [
        "print to pdf", "adobe pdf", "pdfcreator", "cups-pdf",
        "microsoft xps document writer", "onenote", "fax"
    ];

    public static bool IsFilePrinter(string? printerName) =>
        !string.IsNullOrWhiteSpace(printerName) &&
        FilePrinterNames.Any(value => printerName.Contains(value, StringComparison.OrdinalIgnoreCase));
}

// Configures a native PDF print job.
public sealed class PrintOptions
{
    public bool ShowPrintDialog { get; init; }
    public bool Silent { get; init; } = true;
    public bool Landscape { get; init; }
    public string? PaperSize { get; init; }
    public string? PaperSource { get; init; }
    public int? Copies { get; init; }
    public bool Collate { get; init; }
}

// Defines printer discovery and native PDF job submission.
public interface IPrintService
{
    Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);
    Task<PrinterInfo?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default);
    Task PrintPdfAsync(string pdfFilePath, string? printerName = null, PrintOptions? options = null, CancellationToken cancellationToken = default);
}

// Selects the print service for the current operating system.
public interface IPrintServiceFactory
{
    IPrintService Create();
}

// Writes generated PDF bytes to a temporary file independently of the printer implementation.
public interface IPdfGenerator
{
    Task<string> GenerateAsync(ReadOnlyMemory<byte> pdfBytes, string documentName, CancellationToken cancellationToken = default);
}
