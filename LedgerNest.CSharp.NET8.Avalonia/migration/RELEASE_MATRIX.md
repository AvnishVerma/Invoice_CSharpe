# Release acceptance matrix

Initial inventory, not a complete legacy audit. Baseline: b790122, 397 reported checks. No percentage can be derived from this table until the complete feature inventory and release scope are approved. Paths below are relative to the solution directory.

Statuses describe the listed scenario only. P0 means release-blocking; P1 still requires a scope decision before deferral. Test method names refer to tests/LedgerNest.UiChecks/Program.cs.

| ID | Priority | Legacy evidence | Current implementation / acceptance scenario | Status and evidence |
| --- | --- | --- | --- | --- |
| CALC-01 | P0 | lib/domain/invoice_totals_calculator.dart | Application calculation entry points agree on inclusive tax, per-unit discounts, extra costs and rounding | Verified for per-item service API: CheckServiceTotals reproduced failure, then passed after delegation to InvoiceTotalsCalculator; global-mode API design remains separate |
| CALC-02 | P0 | lib/domain/invoice_totals_calculator.dart | Global/per-item/no tax and invoice discounts match reference amounts | Partial coverage: CheckTotals; exhaustive matrix pending |
| DOC-01 | P0 | lib/database/invoice_service.dart | Invoice, quotation and receipt types survive restart and backups | Covered: CheckDocumentTypes |
| DOC-02 | P0 | lib/database/invoice_service.dart | Independent sequence previews, persisted starting number and stale editors | Covered: CheckDocumentNumbering; concurrent processes and retry injection pending |
| DOC-03 | P0 | lib/models/invoice.dart; lib/screens/create_invoice_screen_v2.dart | Load/edit/save complete historical invoice without losing dates, notes, currency, tax configuration or charges | In progress: snapshot-based unpaid editing implemented with stale/payment guards; paid corrections, stock reconciliation, complete item metadata and audit policy remain open |
| PAY-01 | P0 | lib/domain/invoice_calculator.dart; lib/models/invoice.dart | Creation, partial payment and settlement reconcile immediately and after restart | Covered: CheckReceivablesLifecycle; refunds, concurrent payments and corrections pending |
| CAT-01 | P0 | lib/database/customer_service.dart; lib/database/product_service.dart | Exposed catalog fields survive create/edit/reload/JSON restore | Covered: CheckFormRoundTrips; locale and malformed-import cases pending |
| DEL-01 | P0 | lib/database/invoice_service.dart | Trash/restore and permanent deletion preserve or remove linked records correctly | Covered: CheckDocumentTrash |
| DEL-02 | P0 | lib/database/customer_service.dart; lib/database/product_service.dart; lib/database/user_service.dart | Catalog/user deletion persists without erasing historical names or product sales | Covered: CheckRecordDeletion; last-administrator deletion/demotion guards covered by CheckAdministratorGuards |
| DATA-01 | P0 | lib/database/database_helper.dart | Every supported C# schema upgrades transactionally; Flutter conversion is separate | Partial ad hoc column-upgrade tests; versioned migrations and Flutter conversion missing |
| DATA-02 | P0 | lib/database/database_helper.dart | Restore after corruption/interruption preserves last committed data | Happy-path JSON/DB restore covered; failure injection and clean-install recovery pending |
| AUTH-01 | P0 | lib/screens/auth/login_screen.dart; lib/database/user_service.dart | Owner setup, mandatory password change, logout and permissions enforced in services | Partial sign-in/password tests; full enforcement missing |
| AUTH-02 | P0 | lib/screens/auth/ | Recovery proof, expiry, replay prevention and credential migration | Missing; current fast salted hash requires reviewed replacement |
| RPT-01 | P0 | lib/database/report_service.dart | Actual tax and receivables agree with invoices after restart/trash/restore | Covered: CheckTaxReport, CheckReceivablesLifecycle |
| RPT-02 | P0 | lib/database/report_service.dart | Date filters, revenue/profit basis and exports reconcile on mixed scenarios | Partial reports; detail/filter and reconciliation work pending |
| PDF-01 | P0 | lib/services/pdf/; test/pdf_all_templates_dispatcher_test.dart | Multipage templates, Unicode, logos, totals, preview and printing | Basic PDF only; templates, pagination and bulk behavior missing |
| SET-01 | P1 | lib/screens/onboarding/onboarding_screen.dart | Settings survive setup and actually affect every supported workflow | Persistence covered in CheckFormRoundTrips; behavioral audit pending |
| UI-01 | P1 | lib/screens/ | Every form/action has approved baseline and visual comparison across supported displays/themes | Headless captures exist; legacy baselines and real-desktop review missing |
| LOC-01 | P1 | lib/l10n/ | Supported locales translate strings and parse dates/decimals consistently | Preference only; translations missing |
| OPS-01 | P0 | Production plan, Phases 0 and 5 | Automated reproducible build/check artifacts and clean-machine packaging | CI workflow added; hosted execution and installation tests pending |

## Environment and release decisions still open

- Supported production operating systems, minimum hardware, DPI scales and printers.
- Required languages/currencies, maximum dataset and performance/recovery budgets.
- Recovery authority, administrator lifecycle and financial edit/audit retention rules.
- Complete legacy feature inventory and approved treatment of intentional branding/layout differences.

The new CI workflow initially exercises Linux headless checks only; it does not establish support for a production OS or real printer. It retains logs and screenshots for 14 days. Approved visual references and release evidence need separate durable storage.

## Next batches

1. Reproduce CALC-01 failures, consolidate the service on the existing decimal calculator, run regression checks.
2. Expand the inventory to every legacy action and setting; obtain reference captures and environment decisions.
3. Complete DOC-03 snapshot design with migration fixtures before enabling editing.
4. Move transactional operations into Application services; add storage failure/concurrency checks.
5. Implement the owner/session/permission model and recovery controls.


## First batch validation

- Before the fix, CheckServiceTotals failed on per-unit discount/extra-cost handling.
- InvoiceService now delegates to InvoiceTotalsCalculator instead of maintaining separate arithmetic. Its existing public result shape is preserved.
- Solution and headless-check builds passed with zero warnings/errors; 401 checks passed after the fix.
- Local checks used the installed runtime with `--roll-forward Major`; CI explicitly provisions .NET 8. Hosted CI has not yet executed.
- Workflow YAML parsed successfully and diff whitespace checks passed. This is structural validation, not proof of hosted execution.
- Local captures: `/tmp/ledgernest-production-baseline`. CI will retain logs/captures on runs once the workflow is pushed.
- Phase 0 and Phase 1 remain in progress. No production-readiness claim follows from this batch.


## Snapshot storage batch

New invoices capture customer contact/business/address details, due date, title/custom number, hide-number/interstate flags, currency/quantity label, tax and discount configuration, notes and individual additional charges. Existing line snapshots retain the monetary inputs. JSON and database backups preserve the new snapshot. Older C# databases gain a nullable column; null explicitly means the original editor inputs are unavailable.

CheckInvoiceSnapshots verifies field preservation, independence from subsequent editor changes, recalculation of stored totals, both backup formats and upgrade without invented historical values. This is a snapshot format version, not the planned replacement of ad hoc schema upgrades with versioned migrations. Invoice editing, complete item metadata, company/payment-account snapshots, PDF consumption and unknown-format handling remain open.

Snapshot batch validation: build passed with zero warnings/errors; 434 local checks passed. Hosted CI has not run for this batch.


## Unpaid editing batch

The document Edit action now loads version-1 snapshots and line inputs. Save updates the original invoice and items in a transaction, preserving its number and historical cost/description data for existing editor lines. Snapshot currency and quantity labels are preserved independently of current settings. Repeated saves remain updates; the success screen targets the actual saved record.

Editing rejects missing/unsupported snapshots, trash, changed records and documents with payments (including payments posted after loading). Changing document type during editing is not yet supported. Paid-document corrections and audit history require the planned policy and implementation. Legacy updateInvoice also reconciles stock; that behavior is not implemented by this batch, so DOC-03 is not complete.

Regression checks cover reload, repeated saves, retained identity, line replacement, stale editors and payments arriving during editing, plus UI Edit/Save/New actions. Review also found an existing shell error on replacing a live window's DataContext and low-contrast dark-themed fields. Tests use a fresh window and explicit light theme; these UI findings remain open, not fixed by the fixture change.

Unpaid-edit batch validation: build passed with zero warnings/errors; all 460 checks passed. Reviewed light-theme edit capture at `/tmp/ledgernest-edit-captures/invoice-edit.png`. Hosted CI and complete legacy/production readiness remain unverified.


## Theme and shell lifecycle follow-up

Resolved the live-window DataContext reparenting failure and removed the old shell model listener. Invoice editor event cleanup now captures the editor's original model. Shared mutable palette brushes update field surfaces and text in place when the application theme changes, preserving active controls and unsaved input. Wordmark and outlined-action contrast were also corrected.

Validation: build passed with zero warnings/errors; 470 checks passed. The test now replaces the existing window's model and verifies old-model event isolation, then switches the same edited draft through dark/light themes. Both captures were visually reviewed under `/tmp/ledgernest-theme-lifecycle-captures`. Broader accessibility, native OS theme transitions, window lifecycle cases and complete visual parity remain unverified.

Account-guard batch: 481 checks passed, including duplicate names, stale-view administrator counts, deleted-user edits, and session clearing. Production-plan administrator protection intentionally supersedes unrestricted legacy account deletion. Service extraction and full authorization remain pending.

Hosted baseline CI is now confirmed successful for c693536: https://github.com/AvnishVerma/Invoice_CSharpe/actions/runs/34196349072 . This supersedes the earlier pending-hosted-baseline notes, but does not prove subsequent commits passed. The account guard batch was pushed as 0ca4038. Follow-up password creation preserves exact whitespace; 485 local checks passed. Credential hashing and complete authorization remain open.
