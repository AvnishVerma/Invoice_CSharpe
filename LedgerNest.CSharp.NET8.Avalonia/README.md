# LedgerNest Desktop

LedgerNest is a cross-platform invoicing and business-management desktop application migrated from the legacy Flutter implementation.

New contributors: start with the [Developer guide](docs/DEVELOPER_GUIDE.md) for the code structure, startup flow, UI patterns, settings, persistence, and verification commands.

## Technology

- .NET 10, pinned through `global.json`
- Avalonia UI 12.1
- CommunityToolkit.Mvvm
- Entity Framework Core 10 with SQLite
- SkiaSharp-based PDF generation with embedded fonts
- Docnet.Core/PDFium for direct Windows printer rendering
- ScottPlot for report charts

## Notifications

Application status and error messages use an injected Avalonia toast service with info, success, warning, and error variants, timed dismissal, manual dismissal, and persistent exception notifications. Its API and behavior are inspired by the MIT-licensed [WPF Toast Notifications](https://github.com/mike-eason/WPF_ToastNotifications) project. The archived WPF assembly itself is not referenced because WPF controls are Windows-only and incompatible with Avalonia on macOS and Linux.

## Projects

- `src/LedgerNest.Domain` — entities and domain contracts with no project dependencies
- `src/LedgerNest.Application` — application services and ViewModels
- `src/LedgerNest.Infrastructure` — EF Core SQLite persistence and dependency registration
- `src/LedgerNest.Desktop` — Avalonia views, startup, document rendering, and platform printing
- `tests/LedgerNest.UiChecks` — headless UI, calculation, persistence, report, and printing checks
- `invoiso-main` — legacy Flutter application used as the migration reference

## Build and run

```bash
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln --no-restore
dotnet run --project src/LedgerNest.Desktop
```

Run verification checks with:

```bash
dotnet run --project tests/LedgerNest.UiChecks
```

## PDF generation and printing

Invoices are generated as PDFs and submitted directly to the selected printer. Printing does not open a browser, PDF viewer, or external application:

```text
Invoice data → PDF generator → platform print service → printer
```

Windows renders the PDF internally and sends pages through the Windows spooler. macOS and Linux submit the PDF through CUPS using `lp`; printer discovery uses `lpstat`. Printer selection and per-job selection are available under **Settings → PDF Settings → Printing**. Temporary print files are removed after submission, including failed or cancelled jobs.

See [CrossPlatformPrinting.md](docs/CrossPlatformPrinting.md) for setup, architecture, paper sizes, licensing, and platform limitations.

## PDF exports

Invoice, quotation, receipt, and report exports support embedded Roboto fonts, company details, item tables, totals, A4/A5/A6 and thermal paper sizes, landscape orientation, and multipage output.

## Migration notes

The Flutter/Dart application is not translated mechanically. Its business concepts are mapped into C# domain entities, repositories, and ViewModels. Validate the legacy SQLite schema and business rules before introducing database migrations or claiming compatibility with existing data.

The September 10 issue review is tracked in [REPORTED_ISSUES_REVIEW.md](migration/REPORTED_ISSUES_REVIEW.md) and [REPORTED_ISSUES_STATUS.csv](migration/REPORTED_ISSUES_STATUS.csv).
