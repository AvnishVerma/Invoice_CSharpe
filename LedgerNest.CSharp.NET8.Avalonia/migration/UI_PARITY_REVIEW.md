# UI migration review

The Avalonia migration now includes the main navigation, invoice editor, customer/product/user forms, document lists, settings sections, reporting surfaces, and authentication/onboarding forms. Invoice calculations use decimal arithmetic ported from the legacy calculator.

Validation: the headless runner passes 309 checks, including navigation, form validation, invoice calculations, SQLite reload behavior, settings, language/theme preferences, user persistence and password auth, CSV import/export, JSON and database-file backup/restore, report summaries, historical product reporting with cost/discount profit data and CSV export, document and report PDF export, payment status updates, responsive invoice panes, dark theme persistence, and rendering at desktop and narrow widths. Build succeeds with zero warnings and errors.

Run from the repository root:

```sh
dotnet build LedgerNest.CSharp.NET8.Avalonia/tests/LedgerNest.UiChecks/LedgerNest.UiChecks.csproj
dotnet run --project LedgerNest.CSharp.NET8.Avalonia/tests/LedgerNest.UiChecks -- /tmp/ledgernest-ui-captures /tmp/invoiso-legacy-captures
```

The optional second argument compares matching reference PNGs and writes comparison.json. Legacy captures use the Flutter application with its light theme, Roboto fonts, seeded SQLite database and a 1440×900 viewport. Screenshot comparison reports differences; it does not assert parity.

Pixel parity remains incomplete. Remaining review includes dashboard variants, settings spacing, template previews, dialog details, icons and responsive layouts. Native Flutter and Avalonia text/control rasterization also differs.

Customer, product, invoice, payment, user, company information, invoice settings, and PDF settings now persist through SQLite and reload on a new ViewModel instance. Password reset challenge flow, full legacy PDF templates and advanced report services are not fully migrated; some controls are intentionally disabled. Dark theme, localization and alternate legacy layouts require additional work. The original Flutter source and unrelated legacy archive are preserved.

## LedgerNest branding and component split

The requested redesign introduces LedgerNest, the tagline “Invoices, organized.”, a vector receipt mark, and teal/navy brand colors. Brand identity is defined in `src/LedgerNest.Desktop/Branding.cs`; application button colors are styled in `App.axaml`. Namespaces, project files, assemblies, and the local application data folder now use the LedgerNest name.

The window now delegates its shell, overlays, and record forms to `ShellView.cs`, `OverlayView.cs`, and `RecordDialogs.cs`. `BrandLogo` is a reusable visual component. `InvoiceWorkspace` owns the responsive invoice panels: the left pane contains customer information and items; the right pane contains invoice details, options, and totals. The divider supports dragging and keyboard arrows. Below 1000 pixels of available workspace width, panels stack vertically.

The intentional branding and panel changes supersede exact legacy screenshot parity on these surfaces. Reference comparisons remain useful for unrelated forms and behavior.

## Parity measurement status

The requested target is now 100% functionality and UI parity. The previously reported 95% values were coverage estimates without a complete feature checklist or measured screenshot comparison; they do not establish that the target has been reached.

Functional parity remains unverified until legacy behaviors are inventoried and each has a reproducible pass/fail result. Known gaps include full PDF templates, password reset challenges, translated desktop strings and invoice editing/settings behavior.

Pixel similarity remains unmeasured: matching legacy reference captures are unavailable in this environment, and Flutter is not installed to produce them. Headless Avalonia captures verify rendering only. Intentional LedgerNest branding and split-pane changes need to be excluded from any agreed legacy visual comparison.

## Document type persistence review

Legacy invoices store a type (see `invoiso-main/lib/database/database_helper.dart` and `lib/models/invoice.dart`). C# now preserves Invoice, Quotation and Receipt types on save, reload and JSON backup/restore, and product sales exclude non-invoice documents. Existing C# databases gain a Type column defaulting to Invoice; the upgrade does not claim Flutter database compatibility or recover document types already lost by prior versions.

Focused regression checks cover all three document types, JSON round trips, report exclusion, and repeatable upgrades of a pre-type C# database.


## Full form and workflow review

Verified customer business-name and all exposed product fields across saves, edits, reloads and JSON backups. Existing C# databases receive missing columns with safe defaults. Product selection now uses the legacy per-unit default discount and tax-inclusive flag. New Invoice/Quotation/Receipt list buttons open a fresh editor of the selected type, with matching create-button labels. Password changes use the signed-in account and default-admin login prompts for a password change.

The 302-check runner includes these behavior checks and clicks each new document action. Screenshot inspection caught invisible toolbar actions (teal on teal); header action labels/icons now use white. Captures: `/tmp/ledgernest-full-form-captures`.

### Remaining observed gaps (not an exhaustive inventory)

| Area | Evidence in current implementation | Status |
| --- | --- | --- |
| Document editing | Management document Edit action has no handler | Missing |
| Trash/delete | Management uses an in-memory GUID set | Not persistent |
| Document numbering | SaveInvoice uses record count and INV prefix | Legacy per-type sequence/settings unmatched |
| Invoice details | Domain Invoice does not persist due date, notes, tax mode, additional costs or customer snapshots | Incomplete |
| PDF output | SimplePdf provides one basic page; bulk export selects the first document | Templates/pagination/bulk behavior incomplete |
| Recovery | Generate Challenge and Reset Password lack handlers | Missing |
| Onboarding | Company, invoice and appearance preferences now save atomically; completion is persisted | Core persistence verified; logo portability and automatic launch still need review |
| Authentication | Signed-in password workflow corrected; full session/role enforcement and mandatory first-login change remain unaudited | Incomplete |
| Localization | Language preference persists; desktop strings remain English | Missing translations |
| Visual comparison | No matching legacy PNG baseline; Flutter unavailable here | Unmeasured |

No 100% claim is supported by this review. Legacy Flutter tests were not run; legacy source was not modified.

Setup regression checks verify reload, completion, and rejection of fractional starting numbers without partial writes. Latest run: 309 checks passed; captures in `/tmp/ledgernest-setup-captures`.
