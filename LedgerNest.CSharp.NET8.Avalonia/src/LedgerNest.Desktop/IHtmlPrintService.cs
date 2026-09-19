namespace LedgerNest.Desktop;

// Defines HTML rendering and Windows printer discovery for desktop printing.
public interface IHtmlPrintService : IAsyncDisposable
{
    Task PrintHtmlAsync(string html, string? printerName = null, HtmlPrintOptions? options = null, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetInstalledPrinters();
    string? GetDefaultPrinter();
}

// Identifies supported standard and receipt paper sizes.
public enum PaperSizeType
{
    A4,
    A5,
    A6,
    Thermal58mm,
    Thermal80mm,
    Custom
}

// Configures page geometry, readiness checks, and silent or interactive printing.
public sealed class HtmlPrintOptions
{
    public PaperSizeType PaperSize { get; set; } = PaperSizeType.A4;
    public double? WidthMm { get; set; }
    public double? HeightMm { get; set; }
    public double MarginTopMm { get; set; } = 10;
    public double MarginBottomMm { get; set; } = 10;
    public double MarginLeftMm { get; set; } = 10;
    public double MarginRightMm { get; set; } = 10;
    public bool Landscape { get; set; }
    public bool Silent { get; set; } = true;
    public bool PrintBackground { get; set; } = true;
    public bool WaitForImages { get; set; } = true;
    public bool WaitForFonts { get; set; } = true;
    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
