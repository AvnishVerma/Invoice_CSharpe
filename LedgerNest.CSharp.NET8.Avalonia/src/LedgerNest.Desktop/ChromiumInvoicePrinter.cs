using PuppeteerSharp;

namespace LedgerNest.Desktop;

// Renders invoice HTML in Chromium and sends it to the operating system's default printer.
public static class ChromiumInvoicePrinter
{
    private static readonly SemaphoreSlim PrintLock = new(1, 1);
    internal static readonly TimeSpan RenderReadyDelay = TimeSpan.FromMilliseconds(750);
    internal static readonly TimeSpan PrintHandoffDelay = TimeSpan.FromSeconds(15);

    // Loads self-contained invoice HTML and submits Chromium's rendered page in kiosk-printing mode.
    public static async Task PrintHtmlAsync(string html, string printerName = WindowsPrinterService.DefaultPrinter, bool showPrinterDialog = false)
    {
        if (string.IsNullOrWhiteSpace(html)) throw new ArgumentException("Invoice HTML is required.", nameof(html));

        await PrintLock.WaitAsync();
        try
        {
            using var printerScope = showPrinterDialog ? null : WindowsPrinterService.UsePrinter(printerName);
            var executablePath = await ResolveChromiumExecutableAsync();
            await using var browser = await Puppeteer.LaunchAsync(CreateLaunchOptions(executablePath, showPrinterDialog));
            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html, new SetContentOptions
            {
                WaitUntil = [WaitUntilNavigation.Load],
                Timeout = 30_000
            });
            await page.EvaluateExpressionHandleAsync("document.fonts.ready");
            await page.BringToFrontAsync();
            await Task.Delay(RenderReadyDelay);
            await page.EvaluateExpressionAsync("window.print()");
            // window.print() can return before Windows finishes creating the spool job.
            await Task.Delay(PrintHandoffDelay);
        }
        finally
        {
            PrintLock.Release();
        }
    }

    // Creates the visible Chromium process options required for direct default-printer submission.
    private static LaunchOptions CreateLaunchOptions(string executablePath, bool showPrinterDialog)
    {
        var arguments = new List<string>
        {
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-popup-blocking",
            "--disable-backgrounding-occluded-windows",
            "--disable-renderer-backgrounding",
            "--disable-gpu"
        };
        if (!showPrinterDialog) arguments.Add("--kiosk-printing");
        if (OperatingSystem.IsWindows() && !showPrinterDialog)
        {
            arguments.Add("--start-minimized");
            arguments.Add("--window-position=-32000,-32000");
            arguments.Add("--window-size=800,600");
        }

        return new LaunchOptions
        {
            ExecutablePath = executablePath,
            // Physical-printer submission is unavailable from Chromium's headless mode.
            Headless = false,
            Args = [.. arguments]
        };
    }

    // Resolves an explicit browser path or caches PuppeteerSharp's compatible Chromium for later prints.
    private static async Task<string> ResolveChromiumExecutableAsync()
    {
        var configuredPath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(configuredPath))
                throw new FileNotFoundException("PUPPETEER_EXECUTABLE_PATH does not point to a Chromium executable.", configuredPath);
            return configuredPath;
        }

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cachePath = Path.Combine(string.IsNullOrWhiteSpace(localData) ? Path.GetTempPath() : localData, "LedgerNest", "Chromium");
        Directory.CreateDirectory(cachePath);
        var fetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = cachePath });
        var installed = await fetcher.DownloadAsync();
        return installed.GetExecutablePath();
    }
}
