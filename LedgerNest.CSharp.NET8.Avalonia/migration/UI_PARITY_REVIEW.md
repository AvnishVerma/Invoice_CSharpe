# UI migration review

The Avalonia migration now includes the main navigation, invoice editor, customer/product/user forms, document lists, settings sections, reporting surfaces, and authentication/onboarding forms. Invoice calculations use decimal arithmetic ported from the legacy calculator.

Validation: the headless runner passes 167 checks, including navigation, form validation, invoice calculations, SQLite reload behavior, settings persistence, CSV import/export, JSON backup/restore, payment status updates, responsive invoice panes, and rendering at desktop and narrow widths. Build succeeds with zero warnings and errors.

Run from the repository root:

```sh
dotnet build LedgerNest.CSharp.NET8.Avalonia/tests/LedgerNest.UiChecks/LedgerNest.UiChecks.csproj
dotnet run --project LedgerNest.CSharp.NET8.Avalonia/tests/LedgerNest.UiChecks -- /tmp/ledgernest-ui-captures /tmp/invoiso-legacy-captures
```

The optional second argument compares matching reference PNGs and writes comparison.json. Legacy captures use the Flutter application with its light theme, Roboto fonts, seeded SQLite database and a 1440×900 viewport. Screenshot comparison reports differences; it does not assert parity.

Pixel parity remains incomplete. Remaining review includes dashboard variants, settings spacing, template previews, dialog details, icons and responsive layouts. Native Flutter and Avalonia text/control rasterization also differs.

Customer, product, invoice, payment, company information, invoice settings, and PDF settings now persist through SQLite and reload on a new ViewModel instance. Authentication, database-file backup, PDF export and report services are not fully migrated; some controls are intentionally disabled. Dark theme, localization and alternate legacy layouts require additional work. The original Flutter source and unrelated legacy archive are preserved.

## LedgerNest branding and component split

The requested redesign introduces LedgerNest, the tagline “Invoices, organized.”, a vector receipt mark, and teal/navy brand colors. Brand identity is defined in `src/LedgerNest.Desktop/Branding.cs`; application button colors are styled in `App.axaml`. Namespaces, project files, assemblies, and the local application data folder now use the LedgerNest name.

The window now delegates its shell, overlays, and record forms to `ShellView.cs`, `OverlayView.cs`, and `RecordDialogs.cs`. `BrandLogo` is a reusable visual component. `InvoiceWorkspace` owns the responsive invoice panels: the left pane contains customer information and items; the right pane contains invoice details, options, and totals. The divider supports dragging and keyboard arrows. Below 1000 pixels of available workspace width, panels stack vertically.

The intentional branding and panel changes supersede exact legacy screenshot parity on these surfaces. Reference comparisons remain useful for unrelated forms and behavior.
