# Compact application controls

The desktop application uses Fluent's compact density, smaller shared button padding and minimum heights, and compact shared form fields by default. Custom controls in management pages, reports, settings, authentication and dialogs use reduced padding, spacing and dimensions (approximately 20% where layout permits). Material icons use 80% of their previous size. Regular text sizes remain unchanged for readability.

The invoice footer retains its complete totals breakdown and document actions. Printed/PDF document dimensions and business calculations are unaffected.

Validation commands, after building `tests/LedgerNest.UiChecks`:

- `--compact-ui-only`: page, settings, report and dialog rendering, desktop/narrow layouts and dark theme (118 assertions).
- `--invoice-editor-only`: invoice calculations, searching, adding, responsive layout, footer and saving (34 assertions).
- `--product-settings-only`: product preferences and editor behavior (58 assertions).
- `--invoice-settings-only`: invoice preference behavior and presentation (130 assertions).

Pass the screenshot output directory before the mode flag. The compact smoke check intentionally excludes database-backup tests; it is not a substitute for the complete business test suite.
