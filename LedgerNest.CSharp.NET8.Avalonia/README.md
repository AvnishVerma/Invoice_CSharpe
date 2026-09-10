# LedgerNest.CSharp

Initial C#/.NET 8 + Avalonia UI + SQLite migration scaffold for the uploaded legacy Flutter application.

## Target stack
- .NET 8
- Avalonia UI 11
- MVVM
- Microsoft.Extensions.DependencyInjection / Configuration
- Microsoft.EntityFrameworkCore.Sqlite
- CommunityToolkit.Mvvm

## Projects
- `src/LedgerNest.Domain` - entities and domain contracts
- `src/LedgerNest.Application` - use cases/services/DTOs
- `src/LedgerNest.Infrastructure` - EF Core SQLite persistence
- `src/LedgerNest.Desktop` - Avalonia desktop application

## Migration approach
The Flutter/Dart application is not translated mechanically. Its business concepts are mapped into C# domain entities, repositories and ViewModels. Existing SQLite schema/data should be migrated after validating the Dart schema and business rules.

## Run
```bash
dotnet restore
dotnet build
dotnet run --project src/LedgerNest.Desktop
```

## Reported UI fixes

The September 10 issue review is tracked in [the issue report](migration/REPORTED_ISSUES_REVIEW.md) and an [importable status CSV](migration/REPORTED_ISSUES_STATUS.csv). The Google Sheet has not been updated directly because an authenticated writable connection is unavailable.

Usernames are case-insensitive; passwords remain case-sensitive. Press Enter to submit login. Administrators see first-time setup after completing any mandatory password change.

To use a default customer, select **Use as default for new invoices** when saving a customer or selecting an existing customer in the invoice editor. **Clear default customer** is available in the selection dialog. The default is applied when starting a new document; existing drafts are preserved during navigation.

Company logos are saved with company settings and shown again when the screen reopens. Click **Save** after selecting an image. Row and toolbar three-dot actions open dropdown menus; saving a payment closes its dialog.

Document PDF exports use a formatted layout for invoices, quotations and receipts, including company details, item tables, totals, embedded Roboto fonts, A4/A5/A6 and thermal paper sizes, landscape orientation for sheet formats, and multipage receipt pagination. The headless UI checks generate sample receipt PDFs in the output folder passed to `tests/LedgerNest.UiChecks`.
