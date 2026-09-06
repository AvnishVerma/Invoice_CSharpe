# UI migration review

The Avalonia migration now includes the main navigation, invoice editor, customer/product/user forms, document lists, settings sections, reporting surfaces, and authentication/onboarding forms. Invoice calculations use decimal arithmetic ported from the legacy calculator.

Validation: the headless runner passes 129 checks, including navigation, form validation, invoice calculations, session state, and rendering at desktop and narrow widths. Build succeeds with zero warnings and errors.

Run from the repository root:

```sh
dotnet build Invoiso.CSharp.NET8.Avalonia/tests/Invoiso.UiChecks/Invoiso.UiChecks.csproj
dotnet run --project Invoiso.CSharp.NET8.Avalonia/tests/Invoiso.UiChecks -- /tmp/invoiso-ui-captures /tmp/invoiso-legacy-captures
```

The optional second argument compares matching reference PNGs and writes comparison.json. Legacy captures use the Flutter application with its light theme, Roboto fonts, seeded SQLite database and a 1440×900 viewport. Screenshot comparison reports differences; it does not assert parity.

Pixel parity remains incomplete. Remaining review includes dashboard variants, settings spacing, template previews, dialog details, icons and responsive layouts. Native Flutter and Avalonia text/control rasterization also differs.

Data edits currently last for the session. Authentication, payments, backup, import/export and report services are not fully migrated; some controls are intentionally disabled. Dark theme, localization and alternate legacy layouts require additional work. The original Flutter source and unrelated legacy archive are preserved.
