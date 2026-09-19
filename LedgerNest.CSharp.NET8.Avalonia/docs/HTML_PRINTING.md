# HTML direct printing

LedgerNest prints invoices through this path:

```text
Invoice data → self-contained HTML/CSS → Microsoft.Playwright Chromium → Windows printer
```

The normal print action does not create a PDF and does not open a PDF viewer. PDF preview and PDF download remain separate user actions.

## Development setup

Restore and build the solution, then install the Chromium revision required by the restored Playwright package:

```powershell
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln
pwsh src/LedgerNest.Desktop/bin/Debug/net9.0/playwright.ps1 install chromium
```

For a release build, replace `Debug` with `Release`. On a clean Windows machine, run the browser installation command once for the application user. The application reports a clear setup error if the matching Chromium build is missing.

## Printer configuration

Open **Settings → PDF Settings → Printing**. Choose **Windows default printer** or a specific installed printer. LedgerNest validates an explicitly selected printer, temporarily makes it the Windows default for the serialized print job, and restores the previous default afterward.

Enable **Show Printer Selection Dialog** for interactive printing. Leave it disabled for silent kiosk printing. Interactive printing displays Chromium's system print dialog; silent printing launches the reusable Chromium process minimized and off-screen.

## Paper configuration

The PDF Settings page maps its page selection into `HtmlPrintOptions`:

| Setting | Print size |
|---|---|
| A4 | `A4` |
| A5 | `A5` |
| A6 | `A6` |
| Thermal 58mm | `58mm auto`, zero margins |
| Thermal 80mm | `80mm auto`, zero margins |

The service API also supports custom dimensions:

```csharp
await printService.PrintHtmlAsync(html, printerName, new HtmlPrintOptions
{
    PaperSize = PaperSizeType.Custom,
    WidthMm = 100,
    HeightMm = 150,
    MarginTopMm = 5,
    MarginBottomMm = 5,
    Silent = true
}, cancellationToken);
```

## Rendering and lifecycle

The service waits for document readiness, `document.fonts.ready`, and all images before printing. It injects centralized print CSS for paper size, margins, background colors, non-printable elements, and table row page breaks. Existing invoice logos are embedded as data URLs.

Chromium is reused across consecutive jobs of the same mode. Print jobs are serialized because selecting a specific printer temporarily changes the process user's Windows default printer. The browser is recreated after a crash or when switching between silent and interactive modes, and it is disposed during application shutdown.

## Platform limitation

Chromium does not submit physical printer jobs from true headless mode. Silent printing therefore uses headed Chromium with kiosk printing while placing its window off-screen and minimized. `window.print()` returns after Chromium completes the print handoff; no arbitrary print delay or temporary PDF is used.
