# Cross-platform PDF printing

LedgerNest targets .NET 10 and Avalonia Community on Windows, macOS, and Linux. Invoice data is rendered to the same PDF format on every platform. The application then creates a unique temporary PDF, passes it to an operating-system print service, and deletes it in a `finally` block after submission or failure. No browser, external PDF viewer, Avalonia Pro component, or Avalonia Enterprise component is used.

## Architecture

`DocumentPdf` performs platform-independent PDF rendering. `TemporaryPdfGenerator` implements `IPdfGenerator` and stores the resulting bytes under `Path.GetTempPath()`. `PrintServiceFactory` is the sole operating-system selection boundary and returns `WindowsPrintService`, `MacPrintService`, or `LinuxPrintService`. The UI depends only on `IPdfGenerator` and `IPrintService`.

Printer choices under **Settings → PDF Settings → Printing** are refreshed when the screen opens. Enabling **Show Printer Selection Dialog** displays LedgerNest's per-job printer chooser before submission. It uses the same live discovery service and does not open a document viewer.

## Windows

- Runtime: .NET 10 Desktop Runtime and a supported Windows printer driver.
- Discovery: `System.Drawing.Printing.PrinterSettings.InstalledPrinters`; the default comes from Windows printer settings.
- Printing: Docnet.Core/PDFium renders each PDF page in memory and `PrintDocument` sends it directly to the Windows spooler.
- Silent mode, printer name, copies, collation, landscape, and driver-supported paper names are applied where available.
- Per-job selection uses the Avalonia printer chooser because a WinForms print dialog would require a Windows-only target framework and would break the single cross-platform application target.

## macOS

- Runtime: .NET 10 Runtime and the CUPS client included with macOS.
- Discovery: `lpstat -e`; the default destination comes from `lpstat -d`.
- Printing: `lp` receives the PDF directly. LedgerNest does not launch Preview.app.
- Paper, landscape, copies, collation, and an explicit destination are passed as CUPS options.
- Per-job selection uses the LedgerNest printer chooser. The CUPS command-line API does not expose the macOS system print panel.

## Linux

- Runtime: .NET 10 Runtime and CUPS client commands `lp` and `lpstat`.
- Debian/Ubuntu: `sudo apt install cups cups-client`
- Fedora/RHEL: `sudo dnf install cups cups-client`
- Arch: `sudo pacman -S cups`
- Enable CUPS with the distribution's service manager, then verify discovery with `lpstat -e` and the default with `lpstat -d`.
- Printing uses `lp [-d printer] [options] file.pdf`. A missing CUPS client produces a clear `CUPS printing service is not available` error which is written to the application error log.

## PDF format and licensing

The existing `DocumentPdf` renderer is retained. It uses SkiaSharp, which is distributed under the MIT license, and embeds the bundled Roboto regular and bold fonts. The generated documents support tables, images/logos, multiple pages, Unicode text, and the Indian Rupee symbol. Current configured paper sizes are A4, A5, A6, 80 mm thermal, and 58 mm thermal, with centralized dimensions in `DocumentPdf`.

Docnet.Core 2.6.0 is retained for Windows PDF rasterization. Its managed wrapper is MIT licensed; its bundled PDFium native binaries carry the PDFium/BSD-style third-party notices included in the NuGet package. No iText or commercial PDF package was added.

The bundled Roboto font covers Latin and the Rupee symbol. Full Hindi output requires bundling and selecting a Devanagari-capable font in `DocumentPdf`; this remains a font asset limitation rather than a printing limitation.

## Failure handling and limitations

Printing is asynchronous and cancellation is forwarded to temporary-file writes, CUPS processes, and Windows page rendering between pages. Printer removal, invalid printer names, missing files, missing CUPS tools, invalid Windows printer settings, and spool failures surface as clear exceptions and are logged through `AppErrorLog`.

The Windows spool call and the CUPS `lp` command return after the operating system accepts the job. They cannot guarantee that paper was physically produced. Printer-specific tray names and custom media support depend on the installed driver or CUPS queue. Native operating-system print panels are not portable through Avalonia Community; the application provides its own per-job printer chooser instead.
