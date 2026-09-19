# HTML direct printing

LedgerNest prints invoices through these platform paths:

```text
Windows: Invoice HTML → Playwright Chromium → Windows print spooler
macOS/Linux silent: Invoice HTML → headless Chromium spool PDF → CUPS `lp`
macOS/Linux interactive: Invoice HTML → Chromium system print dialog
```

Windows direct printing does not create a PDF and does not open a PDF viewer. Silent macOS and Linux printing creates a private temporary spool PDF because CUPS requires printable output; LedgerNest deletes it immediately after `lp` accepts the job. PDF preview and PDF download remain separate user actions.

## Development setup

Restore and build the solution, then install the Chromium revision required by the restored Playwright package:

```powershell
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln
pwsh src/LedgerNest.Desktop/bin/Debug/net9.0/playwright.ps1 install chromium
```

For a release build, replace `Debug` with `Release`. On a clean Windows machine, the first print automatically installs the matching Chromium build for the application user. The command above can still be used during deployment to install it in advance and avoid the one-time download during the first print.

## Printer configuration

Open **Settings → PDF Settings → Printing**. Choose **System default printer** or a specific installed printer. On Windows, LedgerNest temporarily makes an explicitly selected printer the Windows default for the serialized Chromium job and restores the previous default afterward. On macOS and Linux, LedgerNest discovers destinations with `lpstat` and passes the selected destination to `lp`.

Enable **Show Printer Selection Dialog** for interactive printing. Leave it disabled for silent printing. Windows uses minimized, off-screen Chromium kiosk printing. macOS and Linux use headless Chromium plus CUPS for silent jobs. Interactive printing displays Chromium's system print dialog on all three platforms.

macOS includes CUPS printing tools. Linux installations must provide the `lp` and `lpstat` commands, commonly through the distribution's `cups-client` package, and the CUPS service must be configured.

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

Chromium is reused across consecutive jobs of the same mode. Print jobs are serialized because Windows printer selection temporarily changes the process user's default printer. The browser is recreated after a crash or when switching between silent and interactive modes, and it is disposed during application shutdown.

## Platform limitation

Chromium does not submit physical printer jobs from true headless mode. Windows silent printing therefore uses headed Chromium with kiosk printing while placing its window off-screen and minimized. On macOS and Linux, Chromium creates the CUPS spool document in headless mode and `lp` performs the native handoff. No arbitrary print delay is used.
