# Shared floating labels

Root cause: the application-wide `field-caption` background had been changed to Transparent, leaving the outline visible through label text. Five reusable field views also defined different offsets (-7/-8/-14), caption padding and muted typography. Product fields used a fixed light-theme border color. Overlay icons and input padding were maintained separately.

The fix centralizes caption geometry, typography and outline masking in App.axaml. Each existing field component selects its actual themed surface (generic/card, company/canvas, invoice/input, product/input); these are component surface differences, not per-field offsets. All field grids reserve eight DIPs above the outline. Focus-only visibility is retained from the existing requested behavior for both populated and empty fields. Validation errors remain independently visible; disabled controls cannot receive focus. No search box receives an unsolicited floating label.

Reviewed all AXAML TextBox declarations, global styles, five floating-label implementations, Ui.Field/Fields, numeric inner-editor styling, and company/invoice/product/date/choice/access/settings/dialog workflows. Plain search/filter, UPI/bank-row and dialog text areas intentionally keep their placeholder-based layouts. Existing business, persistence and validation behavior is unchanged.

Templated inputs require an explicit surface binding in their existing DataTemplates: a view-scoped TextBox selector alone did not reach all generated template instances. Captions constrain long labels with a shared width converter and ellipsis; full labels remain in input accessibility names.

The Fluent focused template also overrides the rendered border background. The shared floating-input focus style now binds the actual template border to the input surface, and hides the watermark TextBlock while the caption is visible. Search/filter controls are not marked floating-input.

Verification: isolated full-solution build passed with zero warnings/errors; 19 focused tests passed; complete UiChecks passed 1,096 checks. Captures include Invoice Prefix and Name in light/dark themes under `tests/LedgerNest.Desktop.Tests/artifacts/floating-labels/artifacts/floating-labels/`. Tests cover actual rendered border/caption colors, icon padding, caret selection/binding, empty/populated focus transitions, errors, disabled/read-only states and narrow long-label bounds. Existing numeric-only focus outlines remained intact.

No programmatically constructed visual controls were added. Existing plain custom-field inputs in InvoiceSettingsSections.cs retain global TextBox typography/focus behavior; this task does not migrate that legacy composition or add unsolicited floating labels.
