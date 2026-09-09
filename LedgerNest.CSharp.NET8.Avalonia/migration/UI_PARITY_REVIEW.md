# UI migration review

The Avalonia migration now includes the main navigation, invoice editor, customer/product/user forms, document lists, settings sections, reporting surfaces, and authentication/onboarding forms. Invoice calculations use decimal arithmetic ported from the legacy calculator.

Validation: the headless runner passes 621 checks, including navigation, form validation, invoice calculations, SQLite reload behavior, settings, language/theme preferences, user persistence and password auth, CSV import/export, JSON and database-file backup/restore, report summaries, historical product reporting with cost/discount profit data and CSV export, document and report PDF export, payment status updates, responsive invoice panes, dark theme persistence, and rendering at desktop and narrow widths. Build succeeds with zero warnings and errors.

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
| Document editing | Unpaid version-1 documents load/save through Edit with stale/payment guards | Implemented subset; paid corrections, stock and audit behavior pending |
| Trash/delete | Document trash/restore and customer/product/user permanent deletion persist | Core deletion workflows verified |
| Document numbering | Separate eight-digit sequences, invoice starting setting, transactional allocation and live editor preview verified | PDF prefix/leading-zero formatting remains incomplete |
| Invoice details | New invoices persist versioned customer/editor snapshots including dates, tax configuration, notes and charges | Storage and unpaid editor reload/save implemented; complete legacy fields remain incomplete |
| PDF output | SimplePdf provides one basic page; bulk export selects the first document | Templates/pagination/bulk behavior incomplete |
| Recovery | Generate Challenge and Reset Password lack handlers | Missing |
| Onboarding | Company, invoice and appearance preferences now save atomically; completion is persisted | Core persistence verified; logo portability and automatic launch still need review |
| Authentication | Signed-in password workflow corrected; full session/role enforcement and mandatory first-login change remain unaudited | Incomplete |
| Localization | Language preference persists; desktop strings remain English | Missing translations |
| Visual comparison | No matching legacy PNG baseline; Flutter unavailable here | Unmeasured |

No 100% claim is supported by this review. Legacy Flutter tests were not run; legacy source was not modified.

Setup regression checks verify reload, completion, and rejection of fractional starting numbers without partial writes. Latest run: 309 checks passed; captures in `/tmp/ledgernest-setup-captures`.

Document numbering regression checks cover non-consuming previews, independent type sequences, two already-open editors, restart/backup continuity and earlier C# INV-prefixed records. Build passed with zero warnings/errors; all 318 checks passed. Reviewed quotation capture: `/tmp/ledgernest-numbering-captures/create-quotation.png`.


Document trash now persists a nullable deletion timestamp, following the legacy invoice service. JSON and database backups preserve it; restore retains line items and payment history. Reports, dashboard and default document CSV exports exclude trash, while numbering includes it. Permanent deletion is available only from trash and removes the document, items and payments together. Existing C# databases gain the column without losing records. This does not migrate a Flutter database.

Validation: build passed with zero warnings/errors; 346 checks passed, including old-schema upgrade, restart, both backup formats, report exclusion, payment rejection for trash, permanent deletion, and UI clicks for Move to Trash/Restore. Reviewed `/tmp/ledgernest-trash-captures/invoice-trash.png`. Full functional and pixel parity remain incomplete.


Customer, product and user deletion now removes database records after the existing UI confirmation. Customer deletion preserves invoice customer names and detaches catalog references; product deletion retains historical invoice items and cost snapshots. New invoices store the customer name independently of the customer record, and JSON backup preserves it. Deleting the signed-in user clears the session. Default-user initialization is recorded so an emptied user table does not recreate default credentials on restart.

Validation: build passed with zero warnings/errors; 361 checks passed. Added checks cover deletion across restart, historical customer/product reports, detached references, JSON round trips and deleted-user authentication. Captures: `/tmp/ledgernest-record-delete-captures`. Full parity remains incomplete.


Tax reporting now sums saved invoice tax amounts rather than estimating 18% embedded tax from grand totals. The screen, report CSV and report PDF share the corrected report data. Tax totals are retained in the UI records on creation and reload without rounding before aggregation. Quotations and trashed invoices remain excluded.

Validation: build passed with zero warnings/errors; 376 checks passed. Added cases cover mixed rates, inclusive prices, global tax, no tax, invoice discounts, reload, JSON restore, CSV output and trash exclusion. Captures: `/tmp/ledgernest-tax-captures`. This fixes tax totals; full legacy tax-report detail and date filtering still require migration.


Newly saved invoices now initialize paid and outstanding values immediately. Previously those fields appeared only after reload or payment, causing the receivables report and revenue outstanding summary to omit new debt. Regression checks follow a discounted, taxed invoice through creation, partial payment and settlement, checking record fields and reports both immediately and after reload.

Validation: build passed with zero warnings/errors; 397 checks passed. Captures: `/tmp/ledgernest-receivables-captures`. Full functional and visual parity remain incomplete.

Snapshot validation: build passed with zero warnings/errors; 434 checks passed, including CheckInvoiceSnapshots. Captures: `/tmp/ledgernest-snapshot-captures`. Full parity remains incomplete.

Unpaid-edit batch validation: build passed with zero warnings/errors; all 460 checks passed. Reviewed light-theme edit capture at `/tmp/ledgernest-edit-captures/invoice-edit.png`. Hosted CI and complete legacy/production readiness remain unverified.


Theme/lifecycle follow-up: shared surfaces, labels, outlines, invoice accents and wordmark now update in place for light/dark mode. The window detaches reused shell controls and old model listeners when its DataContext changes; invoice editor subscriptions retain the original model for cleanup. Regression checks exercise a live model replacement, old-model event isolation, theme switching and preservation of an unsaved editor field. Reviewed both invoice-edit-dark.png and invoice-edit-light.png in `/tmp/ledgernest-theme-lifecycle-captures`.

Build passed with zero warnings/errors; 470 checks passed. These fix the two issues observed during editing tests. Full cross-platform, contrast/accessibility and legacy pixel comparisons remain open.

Account safeguards: deletion/demotion of the last administrator is rejected using current database state inside the write transaction. Duplicate usernames (trimmed, case-insensitive) and stale edits of deleted users are rejected. Updating the signed-in account invalidates its session. All 481 checks passed; captures: `/tmp/ledgernest-admin-guard-captures`. This does not complete role authorization, recovery or credential hardening.

Password input consistency: creation now preserves leading/trailing password spaces while continuing to normalize usernames. Regression checks verify exact authentication, rejection of the trimmed alternative and restart. Build passed with zero warnings/errors; 485 local checks passed. Existing stored credentials are unchanged.

Session identity follow-up: logout now clears username, role and password-change state; the sidebar follows the real signed-in account instead of hard-coded admin text. Failed account switches clear prior identity, and user edits/deletion notify session changes. Build passed with zero warnings/errors; 493 checks passed, including UI logout and sidebar refresh. Full route/service authorization, mandatory login, inactivity locking and recovery remain incomplete.

Workspace-access batch: database-backed windows now require login before displaying navigation or business pages. Required password changes retain the lock through Cancel, overlay dismissal and shortcuts. Logout removes workspace content immediately. All 502 local checks passed; startup login capture is in `/tmp/ledgernest-access-captures`. This establishes a desktop UI gate, not service-level authorization or verified pixel parity.

Credential-storage batch: 516 checks passed, including versioned PBKDF2 reference output, existing C# credential upgrades, malformed-hash rejection, and restart persistence. No visual parity claim is added by this security change.

Session-revalidation batch: 531 local checks passed, including external account changes/deletion and a live-window navigation check that returns an invalidated session to login. Existing self-service password-change and workspace checks continue to pass. No new visual parity percentage is asserted.

Safe database-restore batch: 553 local checks passed, including rejection of corrupt/incompatible backups without losing existing invoice/account data, valid restores and session clearing. This storage change adds no pixel-parity evidence.

JSON restore validation batch: 591 local checks passed. Missing tables, malformed roots/metadata, duplicate properties/IDs and broken references are rejected without replacing existing data; complete exports and explicit empty exports remain supported. This does not add visual-parity evidence.

JSON recovery-drill batch: 600 local checks passed, including rollback after an injected insert failure, setup-error handling, retry and reopening. This adds recovery evidence, not visual-parity evidence.

Destination-lock restore batch: 609 local checks passed, including failure without data loss under a competing writer, staging cleanup and successful retry. No additional visual-parity claim is made.

Restore-outcome batch: 615 local checks passed, including committed-copy/reload-failure reporting and reopening. Signed-out restore UI no longer repopulates Settings. No additional pixel-parity evidence is claimed.

JSON restore-outcome batch: 621 local checks passed, including failure after commit, restart guidance, locked partial workspace and successful reopening. No new pixel-parity evidence is claimed.
