# Automated LedgerNest checks

Run from the directory containing `LedgerNest.CSharp.sln`:

```powershell
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln --no-restore
dotnet test LedgerNest.CSharp.sln --no-build --no-restore --logger trx --results-directory artifacts/test-results --settings tests/coverage.runsettings --collect:"XPlat Code Coverage"
pwsh -File tests/Report-TestResults.ps1 -ResultsDirectory artifacts/test-results
```

`dotnet test` discovers all five test projects. No production database, interactive login, printer, clipboard, or network access is needed. The existing command-line headless runner remains available with `dotnet run --project tests/LedgerNest.UiChecks -- artifacts/ui`.

## Projects and isolation

| Project | Responsibility |
| --- | --- |
| LedgerNest.UnitTests | Domain calculations and rules |
| LedgerNest.IntegrationTests | SQLite inventory, pricing, refunds, and persistence |
| LedgerNest.ViewModelTests | Application and master-data ViewModels |
| LedgerNest.Desktop.Tests | Named authentication, authorization, roles, users, settings, validation, rollback, concurrency, and actual AXAML tests |
| LedgerNest.UiChecks | Existing complete headless regression, exposed as one xUnit test with over 1,000 individual assertions |

The new SQLite fixtures use unique temporary directories and disable connection pooling; disposal removes each database. The regression wrapper confines its temporary files to one test-owned directory and clears its own process's SQLite pools before cleanup. Avalonia runs in its own headless session, with UI tests serialized on the dispatcher. HTTP update tests use a local fake message handler; printer settings use a fake print service. Signed-license tests generate isolated signing keys and use fixed UTC dates.

Categories: `Unit`, `Integration`, `UI`, `EndToEnd`, `Architecture`, and `Regression`. Examples:

```powershell
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=UI"
dotnet test --filter "Category=EndToEnd"
dotnet test --filter "Category=Architecture"
```

The three original xUnit projects predate these traits; run the complete solution to include them. Theory cases count separately in TRX. Legacy headless assertions are not reported as separate xUnit tests.

## Requirement coverage map

Numbers correspond to the supplied comprehensive-test request. Coverage is shared where one workflow validates several requirements; this table does not claim that an assertion passing proves an entire screen's appearance on every operating system.

| Requested scenarios | Automated evidence |
| --- | --- |
| 1–12, 65–66, 110–112 | RoleManagementTests; role creation, reload, selector data, duplicate/case/blank rejection, protected Admin, assigned-role fallback and deletion |
| 13, 147–150 | RoleFailureTests; real SQLite trigger failure verifies permission replacement rollback; ConcurrentRoleTests synchronizes competing saves; strict failure checks expose unhandled errors |
| 14–17, 22–23, 25–30 | UserManagementTests; SessionIsolationTests checks multi-role persistence and union; AdministratorGuards in UiChecks covers stale edits and last Admin |
| 18–19, 31 | Existing headless management search/filter/pagination workflows; action UI tests render real Customer management; user search/filter coverage remains in the regression runner rather than an independent new case |
| 32–39, 41–42, 58–64, 67 | AuthenticationTests and SessionIsolationTests; changed credentials invalidate sessions, logout removes all routes/actions, Admin → restricted A → restricted B → Admin |
| 43–57, all resources in menu section | PermissionTests tests every catalog Resource+Action; NavigationTests covers every main route with View on/off plus unknown route and Add-without-View |
| 68–78 | ActionAuthorizationUiTests checks all eight Add/Update/Delete combinations on an accessible Customer screen; View/Refresh remain available; service entry points reject forbidden writes |
| 79–95 | AxamlWorkflowTests verifies actual header typography/height with pixel rounding, permission rows and geometry at two widths, toggles and Accessibility; full UiChecks traverses main and Settings pages and exercises shared button/refresh geometry |
| 94, 96–101 | AxamlArchitectureTests parses shared AXAML and checks production C# constructors/mutations/factories; remaining UI builders deliberately fail the migration gate |
| 102–104, 115–120 | SettingsPersistenceTests; PdfSettingsTests checks reset, page-size compatibility, printer deduplication; legacy invoice and PDF settings regressions cover sequence/formatting and PDF output |
| 105–109 | BackupTests plus existing rejected database/JSON restore, corrupt schema, lock, stream, transaction rollback, and reload-failure checks in UiChecks |
| 113–114 | PermissionTests verifies grouped rows, unsupported cells, dependency, unsaved state, save and restart; actual AXAML toggle test verifies two-way editing |
| 121–123 | ProductWorkflowTests; legacy ProductSettingsBehavior and ProductEditorState check optional fields and unit/stock behavior |
| 124–126 | Actual Accessibility AXAML test verifies all six keyboard shortcuts and absence of Create Invoice Layout |
| 127–131 | LicenseVerificationTests and legacy CheckLicensing cover signed claims, wrong device, signature tampering, time boundaries, import/store failure and stable device identifiers |
| 132–135 | UpdateServiceTests uses fake responses for versions, malformed/oversized manifests, unsafe URLs, HTTP errors, transport failure, timeout and cancellation; existing Software Info AXAML/notification checks |
| 136–143 | RefreshPersistedData test preserves route/context and uniqueness; action UI tests execute real Refresh; BackupCommandTests checks loading-state duplicate prevention; role save and reload tests check permission refresh |
| 144–146 | CustomerWorkflowTests and InvoiceWorkflowTests verify rejected creates/updates/deletes preserve data and sequence; legacy deletion tests preserve invoice history |
| Business calculations and regression | InvoiceCalculationTests plus original domain tests and complete UiChecks: invoices, payments, void, trash/restore, quotations, refunds, tax/revenue reports, QR/PDF output and printing service contracts |

## Explicit gaps and constraints

- The application has no account lock/unlock state or session timeout API. The lock icon opens **Change Password**. Scenarios 20–21 and 40 cannot honestly be tested as account locking until that feature exists. Credential-change session invalidation is covered; a time-expired session cannot be fabricated as a production feature.
- No username character policy is defined beyond required/duplicate validation. Scenario 24 needs an agreed business rule before arbitrary characters can be called invalid.
- Native file-picker, clipboard, physical printer, and download/installation dialogs require platform smoke tests. Their data/services are automated without accessing a user's devices or online services.
- The parent Settings resource has its own content even when User/Permission children are denied; it must remain accessible with Settings View. Do not hide it merely because those two children are unauthorized. Main route checks cover the existing flat menu model.
- The strict architecture test fails while the earlier complete AXAML migration remains unfinished. Do not exclude those legacy builders or claim migration completion.
- The user explicitly authorized fixes for the confirmed role error-handling/concurrency and model validation defects. Their strict tests remain enabled. Any remaining failures are open application defects, and the all-tests-pass acceptance criterion is not met until they are resolved.

CI uses the repository's existing `ledgernest-checks.yml`, builds the full solution, runs `dotnet test`, and retains TRX and Cobertura artifacts even on failure. Review `summary.md` and `test-cases.csv` generated by the reporting script for exact executed counts and outcomes.
