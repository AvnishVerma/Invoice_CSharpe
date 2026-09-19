# LedgerNest Desktop

LedgerNest is a cross-platform invoicing and business-management desktop application migrated from the legacy Flutter implementation.

## Technology

- .NET 10, pinned through `global.json`
- Avalonia UI 12.1
- CommunityToolkit.Mvvm
- Entity Framework Core 10 with SQLite
- Microsoft.Playwright and Chromium for HTML invoice printing
- Docnet.Core for the current in-app PDF preview
- ScottPlot for report charts

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

## Document preview and printing

PDF preview and PDF download remain separate document actions. Direct invoice printing starts from the self-contained HTML invoice template:

```text
Windows: HTML → Playwright Chromium → Windows print spooler
macOS/Linux silent: HTML → Chromium spool document → CUPS
macOS/Linux interactive: HTML → Chromium system print dialog
```

Playwright installs its matching Chromium revision automatically on the first print. Linux requires CUPS and the `lp` and `lpstat` commands. Printer selection is available under **Settings → PDF Settings → Printing**.

See [HTML_PRINTING.md](docs/HTML_PRINTING.md) for paper sizes, deployment setup, silent printing, and platform limitations.

## PDF exports

Invoice, quotation, receipt, and report exports support embedded Roboto fonts, company details, item tables, totals, A4/A5/A6 and thermal paper sizes, landscape orientation, and multipage output.

## Migration notes

The Flutter/Dart application is not translated mechanically. Its business concepts are mapped into C# domain entities, repositories, and ViewModels. Validate the legacy SQLite schema and business rules before introducing database migrations or claiming compatibility with existing data.

The September 10 issue review is tracked in [REPORTED_ISSUES_REVIEW.md](migration/REPORTED_ISSUES_REVIEW.md) and [REPORTED_ISSUES_STATUS.csv](migration/REPORTED_ISSUES_STATUS.csv).
