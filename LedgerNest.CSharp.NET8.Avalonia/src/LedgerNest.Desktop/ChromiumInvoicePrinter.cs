using PuppeteerSharp;

namespace LedgerNest.Desktop;

// Sends generated invoice PDFs to the operating system's default printer through Chromium.
public static class ChromiumInvoicePrinter
{
    private static readonly SemaphoreSlim PrintLock = new(1, 1);

    // Downloads or reuses PuppeteerSharp's compatible Chromium and prints the supplied PDF in kiosk mode.
    public static async Task PrintPdfAsync(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath)) throw new ArgumentException("A PDF path is required.", nameof(pdfPath));
        if (!File.Exists(pdfPath)) throw new FileNotFoundException("The invoice PDF could not be found.", pdfPath);

        await PrintLock.WaitAsync();
        try
        {
            var executablePath = await ResolveChromiumExecutableAsync();
            await using var browser = await Puppeteer.LaunchAsync(CreateLaunchOptions(executablePath));
            await using var page = await browser.NewPageAsync();
            await page.GoToAsync(new Uri(Path.GetFullPath(pdfPath)).AbsoluteUri, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Load],
                Timeout = 30_000
            });
            await page.EvaluateExpressionAsync("window.print()");
            await Task.Delay(2_000);
        }
        finally
        {
            PrintLock.Release();
        }
    }

    // Creates the visible Chromium process options required for direct default-printer submission.
    private static LaunchOptions CreateLaunchOptions(string executablePath) => new()
    {
        ExecutablePath = executablePath,
        Headless = false,
        Args =
        [
            "--kiosk-printing",
            "--allow-file-access-from-files",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-popup-blocking"
        ]
    };

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
