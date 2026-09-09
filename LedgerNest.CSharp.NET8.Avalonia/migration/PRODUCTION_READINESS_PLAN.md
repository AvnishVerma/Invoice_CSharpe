# LedgerNest production-readiness plan

Status: proposed execution plan. No phase is complete merely because this plan exists.
Baseline: commit b790122; latest reported validation is a successful build and 397 headless checks. Those checks were not rerun for this documentation change. They are a regression baseline, not a coverage or readiness percentage.

## Release objective and scope

Deliver a desktop invoicing application that preserves financial records, enforces access controls, produces correct documents, and can be installed, upgraded and recovered predictably.

Working scope: local desktop use with SQLite. Select the supported operating systems, currencies, languages, printer types, deployment model and largest expected dataset in Phase 0. Shared/network databases, cloud synchronization and simultaneous users must not be implied by this local-storage scope; requests for those capabilities need explicit design and testing.

The existing goal of complete legacy functionality and UI matching remains open. Production readiness and legacy parity are separate gates: matching an unsafe legacy behavior is not sufficient for production. Any intentionally changed or deferred feature must be recorded and approved before a narrower release is described as ready. Preserve the agreed LedgerNest branding and split invoice panes when comparing UI.

Keep .NET 8/Avalonia 11 as the current repository target. Check runtime and dependency support against the intended release date; if an upgrade is needed, plan and validate it as a separate change, not as a response to the locally installed SDK.

## Phase 0 — Establish an auditable release baseline

Priority: P0. This phase defines the scope for every later gate.

- Build a feature matrix from the legacy routes, database services, forms, settings, reports and tests. Each row needs a legacy source reference, current implementation, acceptance scenario, evidence, priority and status: unverified, missing, implemented or verified.
- Include normal operation, invalid input, cancellation, restart, permission denial, backup/restore and failure cases. Audit every visible action, keyboard shortcut and setting; a rendered or enabled control does not prove functionality.
- Agree the supported OS/display/printer/language/currency matrix and representative small, typical and large datasets. Use synthetic or appropriately sanitized fixtures.
- Capture legacy screenshots and representative PDFs with reproducible data, dates, fonts, viewport, theme and scale settings. Retain references in durable test storage rather than relying on /tmp.
- Add CI for restore, solution build and the existing headless runner. Retain check results and captures as artifacts. Record toolchain versions so failures can be reproduced.
- Inventory dependencies and licenses; identify unsupported packages and known vulnerabilities before selecting the release toolchain.

Exit gate: the feature inventory and supported environment matrix are reviewed, the baseline runs in CI, and every known gap has a tracked acceptance scenario. Missing visual references remain an explicit blocker to a visual parity claim.

## Phase 1 — Make financial operations and storage dependable

Priority: P0. Depends on the baseline; characterization tests precede refactoring.

### One calculation path and clear ownership

- Reconcile Application/InvoiceService.cs with Application/InvoiceTotalsCalculator.cs. They currently differ in rounding and discount treatment. Route all saving, previews, reports and PDF totals through one authoritative calculation policy.
- Lock down decimal arithmetic, rounding boundaries, inclusive/exclusive tax, global/per-item/no tax, per-unit discounts, invoice discounts, extra charges, zero totals, overpayments and currency precision using examples from the legacy calculators and tests.
- Move invoice/payment operations and business rules from Desktop/MainWindowViewModel.cs into Application services with Domain contracts. Infrastructure implements database, backup and file operations; Desktop handles presentation and composition. Extract one workflow at a time, preserving behavior with tests.
- Enforce validation in the service layer so shortcuts, imports and future callers cannot bypass it.

### Durable records and migrations

- Persist complete invoice snapshots: customer/business identity and addresses, item descriptions and costs, document type/title, dates, due date, currency, tax mode/rate, discounts, additional charges, notes and payment details. Customer name alone is not a complete historical snapshot.
- Introduce versioned, transactional migrations with recorded schema versions and fixture tests for each supported C# schema. Move schema setup out of routine per-operation checks.
- Treat legacy Flutter conversion as a distinct import/migration path. Verify original keys, columns, relationships and monetary semantics; do not open a Flutter database as though it were a C# database.
- Define foreign-key, uniqueness and check constraints. Allocate document numbers atomically; validate simultaneous writers, duplicate submissions and retry behavior. Decide and test whether deleted numbers may ever be reused.
- Make document edits, payments, stock adjustments and permanent deletion transactional. Add conflict detection where stale editors could overwrite newer data.
- Test process termination, locked databases, disk-full errors, read-only storage and interrupted migrations. Failures must preserve committed data and show a useful recovery action.

### Recovery

- Validate backup versions and contents before replacing live data. Create and verify a recovery copy, stop competing writes, restore atomically, then run integrity and reconciliation checks.
- Handle missing/corrupt/truncated backups, unsupported schemas, cancelled restores and unavailable asset files. Document the difference between JSON business-data backups and database backups containing credentials.
- Rehearse recovery on a separate clean installation, including logos/assets and access to restored accounts. Define backup frequency and acceptable recovery time/data loss before the pilot.

Exit gate: no unresolved financial discrepancies in the agreed test matrix; supported migrations and recovery drills pass; concurrent saves cannot silently duplicate or lose records; failure-injection tests show no partial business operations. Critical financial and storage code receives independent review.

## Phase 2 — Enforce authentication and authorization

Priority: P0. Depends on the Application service boundary and schema strategy from Phase 1; design can start earlier.

- Define a permission matrix for owners/admins and users, including settings, user management, financial edits, deletion, export and restore. Enforce it in services as well as UI routes.
- Replace reusable default credentials with a deliberate first-run owner setup. Make required password changes unavoidable through Escape, overlay dismissal, navigation, shortcuts or app restart.
- Replace the current fast salted SHA-256 password scheme with a vetted, versioned password-derivation implementation. Support migration of existing hashes after successful authentication; choose parameters through a documented security review and performance test.
- Design local-account recovery with explicit proof of authority, expiry, one-time use, retry limits and audit events. Do not add an unrestricted reset shortcut to complete the legacy screen.
- Define logout, inactivity locking, session invalidation after password/role changes, and behavior when the signed-in account is deleted. Protect against deleting or demoting the last recovery-capable administrator.
- Restrict sensitive file access and sanitize logs. Never log passwords, hashes, recovery secrets, or entire customer/payment payloads. Define protection and handling for database backups containing credentials.
- Record security-relevant actions without placing secrets in the audit trail. Audit history must survive normal business-record deletion according to the agreed retention policy.

Exit gate: negative authorization tests pass for direct service calls, UI and shortcuts; account recovery is demonstrated; no reusable default credential remains; credential migration works; no unresolved critical/high security finding remains.

## Phase 3 — Complete essential business workflows

Priority: P0 for release-essential behavior, P1 for approved optional functionality. Depends on Phases 1–2.

- Implement document load/edit/save, clone, quotation conversion, receipts, due dates and all persisted editor options. Specify edit restrictions once payments exist, and retain history when financial records change.
- Complete payment correction/refund/receipt workflows required by the feature matrix. Reconcile invoice balances against payment records after imports, edits and restores.
- Verify stock behavior for products versus services, unlimited stock, cancellations, edits and returns. Catalog deletion must preserve historical sales and invoice snapshots.
- Make each supported setting affect the intended behavior, including numbering display, currency, dates, tax defaults, units, logo and payment details. Complete onboarding and startup behavior.
- Finish report date/customer filters, tax detail, revenue/profit semantics, sorting, pagination and export scope. Reconcile screens, CSVs and PDFs with the same underlying data.
- Implement full multipage document/PDF output: repeated headers, totals, Unicode/currency text, selected templates, logos, pagination, bulk export, preview and printing. Test cancellation, missing assets and unavailable printers.
- Inventory imports, exports, custom fields and other legacy actions. Implement and test each in-scope path. Any deferred feature needs an explicit release-scope decision and clear product behavior.

Exit gate: every release-essential workflow passes end-to-end from creation through payment, correction and export, with restart checks. No dead control or silently ignored setting remains in the released scope. Report and document totals reconcile with stored records.

## Phase 4 — Verify UI, accessibility and usability

Priority: P1; incorrect or inaccessible critical actions are release blockers. Reference preparation starts in Phase 0; final validation follows workflow completion.

- Compare every in-scope legacy form, dialog and workflow against approved references. Record branding and split-pane differences explicitly.
- Use reproducible visual comparisons with per-screen tolerances and human review. Set tolerances after inspecting stable baselines; do not invent an overall pixel score. Different Flutter/Avalonia text rasterization needs an agreed treatment.
- Cover supported OSes, DPI scales, window sizes, light/dark themes and languages. Check clipping, overflow, contrast, long names, large values and empty/loading/error states.
- Verify focus order, keyboard-only operation, shortcuts, accessible names, validation focus and destructive-action confirmation. Add real desktop checks alongside headless tests.
- Move strings into localization resources, test every supported language, and ensure decimal/date parsing and displayed currency agree with saved data.
- Avoid blocking the UI thread during reports, file operations and backups. Provide progress, cancellation and actionable error messages.

Exit gate: all reference screens have reviewed results; no unresolved visual/accessibility defect prevents an essential task; supported locale and display scenarios pass. Exact legacy parity remains unclaimed until the approved comparison criteria are satisfied.

## Phase 5 — Package and operate a release candidate

Priority: P0 for installation, upgrade and recovery; P1 for operational refinements. Depends on the functional and security gates.

- Produce reproducible, versioned release artifacts for each supported OS. Test runtime availability, installation without an SDK, upgrade, uninstall and data retention. Add signing through the appropriate release process.
- Define updater behavior and verify artifact integrity. Establish a rollback procedure compatible with database schema changes; do not assume an older binary can read an upgraded database.
- Add structured local diagnostics and crash handling with redaction, rotation and a reviewed support-export flow. Any remote telemetry requires a deliberate privacy and consent decision.
- Publish installation, backup, restore, troubleshooting and known-limitations documentation. Assign release and incident owners.
- Benchmark agreed datasets on minimum supported hardware. Set numeric budgets for startup, search, save, report generation, export and restore before performance acceptance testing.
- Run dependency/license checks and review the release manifest, configuration and bundled assets.

Exit gate: clean-machine installation and upgrade/rollback rehearsals pass; diagnostics help reproduce faults without exposing secrets; performance meets agreed budgets; release artifacts and runbooks are traceable to a tested commit.

## Phase 6 — Controlled pilot and production decision

Priority: P0. Requires Phases 0–5 release gates.

- Start with copied or synthetic records, then an explicitly authorized, limited pilot with verified backups and a recovery contact.
- Exercise daily work and period-end reconciliation using representative invoices, payments, reports, printing and recovery. A stakeholder familiar with the legacy app signs off business results.
- Track failures, discrepancies, support requests and recovery times. Fix findings and rerun affected checks before expanding use.
- Agree pilot duration and representative transaction volume in Phase 0. Time elapsed alone is not evidence of readiness.

Release gate: all release-essential acceptance scenarios pass; no open blocker/critical/high defect remains; reconciliation has no unexplained differences; recovery and rollback work; business, technical and security reviewers sign off the exact candidate. Record lower-severity exceptions with an owner and fix date.

## Execution order and evidence

Sequence: baseline → financial/storage foundation → access controls → workflow completion → final UI verification → packaged candidate → pilot.

UI reference collection, packaging discovery and security design can proceed during foundation work. Production release cannot bypass their gates. The full legacy-parity target remains tracked even if a narrower pilot scope is approved.

For each implementation slice:

1. Record the concrete legacy behavior and acceptance scenarios.
2. Add focused regression/characterization checks where appropriate.
3. Implement the smallest coherent change within the project dependency boundaries.
4. Review financial, data, security and UI implications relevant to that change.
5. Run affected checks and the required regression suite; inspect visual artifacts when UI changes.
6. Commit code, tests and evidence together; update the feature matrix and remaining risks.

Store CI results, visual references, migration fixtures and release evidence durably. Test counts may grow or shrink as checks are improved; do not use the count as a release gate or a parity percentage. Report verified scenarios against the complete approved matrix and show unverified scenarios separately.

## First implementation batch

| Order | Deliverable | Acceptance evidence |
| --- | --- | --- |
| 1 | Feature/release matrix and CI baseline | Reproducible build/check artifacts; each known gap tracked |
| 2 | Calculation characterization and consolidation | Both current entry points reconciled; legacy rounding/tax/discount cases pass |
| 3 | Complete invoice snapshots and versioned migration | Historical documents unchanged by catalog edits; old-schema fixtures upgrade safely |
| 4 | Transactional Application services | Save/payment retry, concurrency and failure tests pass |
| 5 | Recovery rehearsal and owner/account model | Verified backup restoration; enforced owner setup and role matrix |

Estimate calendar dates only after Phase 0 establishes scope, staffing, available reference environments and integration requirements. Assign an accountable implementer and a distinct reviewer to each batch. No production date or numerical readiness score is promised by this plan.


## Execution record — first batch

Started the initial [release acceptance matrix](RELEASE_MATRIX.md) and added `.github/workflows/ledgernest-checks.yml` for .NET 8 build/headless checks with retained artifacts. Reproduced the duplicate service-calculation defect, then delegated InvoiceService to the existing decimal calculator. Both builds passed and all 401 checks passed locally. Hosted CI execution, complete feature enumeration and environment decisions remain open; neither Phase 0 nor Phase 1 is complete.

Credential-storage progress: versioned PBKDF2-HMAC-SHA256 now replaces new fast hashes, with successful-verification migration of existing C# credentials. See [password storage design and limitations](PASSWORD_STORAGE.md). Phase 2 remains open: service authorization, first-run owner setup, recovery and release-hardware performance/security review are not complete.

Session progress: desktop sessions now retain the verified account snapshot and revalidate its identity, role, credentials and password-change state on window activation, page rendering and keyboard shortcuts. Deleted or changed accounts return to login; failed database session checks also clear access. Self-service password changes refresh the snapshot. This is checkpoint-based invalidation, not continuous monitoring or transaction-level authorization; service enforcement and inactivity locking remain open.

Database restore progress: file restores now validate an isolated candidate before copying through the SQLite backup API; corrupt files, unsupported tables, malformed snapshots and broken references are rejected before live data changes. See [database restore design and remaining drills](DATABASE_RESTORE.md). Full disaster-recovery testing and restore authorization remain open.

JSON restore progress: incomplete and malformed backups are rejected before the replacement transaction, including missing tables, duplicate IDs and broken references. Explicit complete empty backups remain supported. Financial consistency validation, schema versioning, authorization and failure drills are still pending.

Recovery-drill progress: JSON restore now has injected insert-failure rollback and database-setup failure coverage, including successful retry and reopening. Database setup is covered by the restore error handler. Disk-full, power-loss, concurrent-write and platform recovery drills remain pending.

Recovery-drill progress: database-file restore now has a competing destination-writer test, including preserved live records, staging cleanup and successful retry after lock release. Restore connections use a two-second lock timeout. General concurrent-operation safety, disk exhaustion and power-loss drills remain open.

Restore outcome progress: committed database copies are distinguished from later reload/cleanup problems. Reload failure provides restart guidance and retains signed-out access; cleanup IO/access failures preserve the copy outcome. Injected post-copy reload failure is covered; platform cleanup-failure injection remains open.

JSON restore outcome progress: failures during reload after commit now report the completed restore with restart guidance and lock partial workspace state. An injected post-commit failure verifies persistence and reopening. Broader production-readiness gates remain incomplete.
