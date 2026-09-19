# LedgerNest

LedgerNest is a cross-platform invoicing and business-management desktop application built with .NET 10, Avalonia UI, MVVM, EF Core, and SQLite. The active solution is located in `LedgerNest.CSharp.NET8.Avalonia/`; the directory name is retained to avoid disrupting existing development paths.

## Requirements

- .NET SDK 10.0.200 or a compatible later feature band
- Windows, macOS, or Linux desktop environment
- Linux printing: CUPS with the `lp` and `lpstat` commands

## Build and run

```bash
cd LedgerNest.CSharp.NET8.Avalonia
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln --no-restore
dotnet run --project src/LedgerNest.Desktop
```

The first print automatically installs the Playwright-managed Chromium revision for the current user. See [HTML direct printing](LedgerNest.CSharp.NET8.Avalonia/docs/HTML_PRINTING.md) for deployment and platform details.

## Verification

```bash
cd LedgerNest.CSharp.NET8.Avalonia
dotnet run --project tests/LedgerNest.UiChecks
```

The verification project covers UI rendering, invoice calculations, persistence, reports, document export, and printer configuration.

## Repository layout

- `LedgerNest.CSharp.NET8.Avalonia/src/LedgerNest.Domain` — entities and domain contracts
- `LedgerNest.CSharp.NET8.Avalonia/src/LedgerNest.Application` — application services and ViewModels
- `LedgerNest.CSharp.NET8.Avalonia/src/LedgerNest.Infrastructure` — EF Core SQLite persistence
- `LedgerNest.CSharp.NET8.Avalonia/src/LedgerNest.Desktop` — Avalonia desktop application
- `LedgerNest.CSharp.NET8.Avalonia/tests/LedgerNest.UiChecks` — headless UI and behavior checks
- `LedgerNest.CSharp.NET8.Avalonia/invoiso-main` — legacy Flutter reference application
