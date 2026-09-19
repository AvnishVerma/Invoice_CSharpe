using System.Globalization;
using Microsoft.Playwright;

namespace LedgerNest.Desktop;

// Reuses Playwright Chromium to render HTML and submit it to a Windows printer without a PDF intermediary.
public sealed class PlaywrightHtmlPrintService : IHtmlPrintService
{
    private readonly SemaphoreSlim printLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? browserContext;
    private IPage? browserPage;
    private bool browserIsSilent;
    private bool disposed;

    // Returns installed printers through the Windows printing subsystem.
    public IReadOnlyList<string> GetInstalledPrinters() => PlatformPrinterService.GetInstalledPrinters();

    // Returns the current Windows default printer, or null outside supported Windows versions.
    public string? GetDefaultPrinter() => PlatformPrinterService.GetDefaultPrinter();

    // Starts Chromium and creates its reusable page before the first user-initiated print.
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await printLock.WaitAsync(cancellationToken);
        try
        {
            await EnsurePageAsync(silent: true, cancellationToken);
        }
        finally
        {
            printLock.Release();
        }
    }

    // Renders supplied HTML, waits for deterministic resource readiness, and invokes Chromium printing.
    public async Task PrintHtmlAsync(string html, string? printerName = null, HtmlPrintOptions? options = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(html)) throw new ArgumentException("Invoice HTML is required.", nameof(html));
        options ??= new HtmlPrintOptions();
        ValidateOptions(options);

        await printLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var usesUnixPrintDialog = (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) && !options.Silent;
            var selectedPrinter = usesUnixPrintDialog ? null : PlatformPrinterService.ResolvePrinter(printerName);
            using var printerScope = PlatformPrinterService.UseWindowsPrinter(selectedPrinter);
            var page = await EnsurePageAsync(options.Silent, cancellationToken);
            page.SetDefaultTimeout((float)options.RenderTimeout.TotalMilliseconds);
            await page.SetContentAsync(ApplyPrintCss(html, options), new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = (float)options.RenderTimeout.TotalMilliseconds
            }).WaitAsync(cancellationToken);
            await page.WaitForFunctionAsync("() => document.readyState === 'complete'").WaitAsync(cancellationToken);
            if (options.WaitForFonts)
                await page.EvaluateAsync("() => document.fonts ? document.fonts.ready : Promise.resolve()").WaitAsync(cancellationToken);
            if (options.WaitForImages)
                await WaitForImagesAsync(page, cancellationToken);
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { Media = Media.Print }).WaitAsync(cancellationToken);
            if ((OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) && options.Silent)
                await SubmitCupsPrintAsync(page, selectedPrinter, options, cancellationToken);
            else
            {
                await page.BringToFrontAsync().WaitAsync(cancellationToken);
                await page.EvaluateAsync("() => window.print()").WaitAsync(cancellationToken);
            }
        }
        finally
        {
            printLock.Release();
        }
    }

    // Creates or reuses the Chromium process needed for silent or interactive printing.
    private async Task<IBrowser> EnsureBrowserAsync(bool silent, CancellationToken cancellationToken)
    {
        if (browser is { IsConnected: true } && browserIsSilent == silent) return browser;
        await CloseBrowserSessionAsync();
        playwright ??= await Microsoft.Playwright.Playwright.CreateAsync().WaitAsync(cancellationToken);
        try
        {
            browser = await LaunchBrowserAsync(playwright, silent, cancellationToken);
        }
        catch (PlaywrightException ex) when (IsMissingBrowserExecutable(ex))
        {
            playwright.Dispose();
            playwright = null;
            await InstallChromiumAsync(cancellationToken);
            playwright = await Microsoft.Playwright.Playwright.CreateAsync().WaitAsync(cancellationToken);
            browser = await LaunchBrowserAsync(playwright, silent, cancellationToken);
        }
        browserIsSilent = silent;
        return browser;
    }

    // Returns the reusable page for the current print mode, creating it only when required.
    private async Task<IPage> EnsurePageAsync(bool silent, CancellationToken cancellationToken)
    {
        var activeBrowser = await EnsureBrowserAsync(silent, cancellationToken);
        if (browserPage is { IsClosed: false }) return browserPage;
        browserContext ??= await activeBrowser.NewContextAsync().WaitAsync(cancellationToken);
        browserPage = await browserContext.NewPageAsync().WaitAsync(cancellationToken);
        return browserPage;
    }

    // Clears reusable browser objects while tolerating an already-crashed Chromium process.
    private async Task CloseBrowserSessionAsync()
    {
        try { if (browserPage != null) await browserPage.CloseAsync(); }
        catch (PlaywrightException) { }
        try { if (browserContext != null) await browserContext.DisposeAsync(); }
        catch (PlaywrightException) { }
        try { if (browser != null) await browser.DisposeAsync(); }
        catch (PlaywrightException) { }
        browserPage = null;
        browserContext = null;
        browser = null;
    }

    // Launches the Playwright-managed Chromium instance with the requested print mode.
    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright activePlaywright, bool silent, CancellationToken cancellationToken) =>
        activePlaywright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = silent && !OperatingSystem.IsWindows(),
            Args = BuildChromiumArguments(silent)
        }).WaitAsync(cancellationToken);

    // Renders a temporary print spool document and submits it to CUPS on macOS or Linux.
    private static async Task SubmitCupsPrintAsync(IPage page, string? printerName, HtmlPrintOptions options, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ledgernest-print-{Guid.NewGuid():N}.pdf");
        try
        {
            await page.PdfAsync(new PagePdfOptions
            {
                Path = path,
                PrintBackground = options.PrintBackground,
                PreferCSSPageSize = true,
                Landscape = options.Landscape
            }).WaitAsync(cancellationToken);
            await PlatformPrinterService.SubmitCupsJobAsync(path, printerName, cancellationToken);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Downloads the matching Chromium revision once when Playwright was newly installed or upgraded.
    private static async Task InstallChromiumAsync(CancellationToken cancellationToken)
    {
        var exitCode = await Task.Run(
            () => Microsoft.Playwright.Program.Main(["install", "chromium"]),
            cancellationToken);
        if (exitCode != 0)
            throw new InvalidOperationException($"Playwright could not install Chromium (exit code {exitCode}). Check the internet connection and write access to the user profile.");
    }

    // Identifies the Playwright error that specifically indicates a missing managed browser executable.
    internal static bool IsMissingBrowserExecutable(PlaywrightException exception) =>
        exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase);

    // Creates Chromium flags for off-screen kiosk printing or an interactive print dialog.
    private static string[] BuildChromiumArguments(bool silent)
    {
        var arguments = new List<string>
        {
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-popup-blocking",
            "--allow-file-access-from-files",
            "--disable-backgrounding-occluded-windows",
            "--disable-renderer-backgrounding"
        };
        if (silent && OperatingSystem.IsWindows())
        {
            arguments.Add("--kiosk-printing");
            arguments.Add("--start-minimized");
            arguments.Add("--window-position=-32000,-32000");
            arguments.Add("--window-size=800,600");
        }
        return [.. arguments];
    }

    // Waits for every HTML image to load or fail with a useful rendering error.
    private static async Task WaitForImagesAsync(IPage page, CancellationToken cancellationToken)
    {
        const string script = """
            () => Promise.all(Array.from(document.images).map(image => {
                if (image.complete && image.naturalWidth > 0) return Promise.resolve();
                return new Promise((resolve, reject) => {
                    image.addEventListener('load', resolve, { once: true });
                    image.addEventListener('error', () => reject(new Error(`Image failed to load: ${image.currentSrc || image.src}`)), { once: true });
                });
            }))
            """;
        await page.EvaluateAsync(script).WaitAsync(cancellationToken);
    }

    // Injects centralized paper, margin, page-break, and background-print CSS into the document.
    internal static string ApplyPrintCss(string html, HtmlPrintOptions options)
    {
        var size = options.PaperSize switch
        {
            PaperSizeType.A4 => "A4",
            PaperSizeType.A5 => "A5",
            PaperSizeType.A6 => "A6",
            PaperSizeType.Thermal58mm => "58mm auto",
            PaperSizeType.Thermal80mm => "80mm auto",
            PaperSizeType.Custom => $"{Mm(options.WidthMm!.Value)} {Mm(options.HeightMm!.Value)}",
            _ => "A4"
        };
        if (options.Landscape && options.PaperSize is PaperSizeType.A4 or PaperSizeType.A5 or PaperSizeType.A6) size += " landscape";
        var thermal = options.PaperSize is PaperSizeType.Thermal58mm or PaperSizeType.Thermal80mm;
        var margins = thermal ? "0" : $"{Mm(options.MarginTopMm)} {Mm(options.MarginRightMm)} {Mm(options.MarginBottomMm)} {Mm(options.MarginLeftMm)}";
        var background = options.PrintBackground ? "print-color-adjust: exact; -webkit-print-color-adjust: exact;" : "";
        var css = $"<style data-ledgernest-print>@page {{ size: {size}; margin: {margins}; }} @media print {{ html, body {{ {background} }} .no-print {{ display: none !important; }} table, tr, td, th {{ break-inside: avoid; page-break-inside: avoid; }} }}</style>";
        var head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return head >= 0 ? html.Insert(head, css) : css + html;
    }

    // Validates custom dimensions, margins, and timeout before launching Chromium.
    private static void ValidateOptions(HtmlPrintOptions options)
    {
        if (options.PaperSize == PaperSizeType.Custom && (options.WidthMm is not > 0 || options.HeightMm is not > 0))
            throw new ArgumentException("Custom paper requires positive WidthMm and HeightMm values.", nameof(options));
        if (new[] { options.MarginTopMm, options.MarginBottomMm, options.MarginLeftMm, options.MarginRightMm }.Any(margin => margin < 0))
            throw new ArgumentException("Print margins cannot be negative.", nameof(options));
        if (options.RenderTimeout <= TimeSpan.Zero) throw new ArgumentException("RenderTimeout must be greater than zero.", nameof(options));
    }

    // Formats millimetres for culture-independent CSS output.
    private static string Mm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "mm";

    // Closes Chromium and Playwright so application shutdown leaves no child processes.
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await printLock.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            await CloseBrowserSessionAsync();
            playwright?.Dispose();
            playwright = null;
        }
        finally
        {
            printLock.Release();
            printLock.Dispose();
        }
    }
}
