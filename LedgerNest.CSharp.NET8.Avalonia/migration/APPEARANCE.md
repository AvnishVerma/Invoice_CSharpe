# Desktop language, theme and navigation

The shell applies saved language and theme preferences when its view model is attached and when either setting changes. System mode uses the platform theme; switching themes updates shared palettes and dynamic resources without rebuilding the current editor.

`UiLocalization` uses the seven catalogs copied from the Flutter reference under `Assets/Localization`. Presentation labels use `Ui.LocalText` or the attached `UiLocalization.Text` property. Internal navigation keys, commands and entered customer/product values remain unchanged. Missing desktop-specific phrases and unsupported ICU-format messages fall back to English. These catalogs retain the reference application's translation coverage; this is not a claim that every desktop message is translated.

Button captions are localized inside a TextBlock rather than binding Button.Content. This preserves custom icon/grid content and inherited foreground colors. Settings navigation uses a horizontal, scrollable top bar. Selected/hover tab colors use theme resources. Accordion headers use the Fluent theme's `ExpanderMinHeight` resource at 36 pixels, with 24-pixel chevron containers.

Run `LedgerNest.UiChecks` with an output directory and `--appearance-only` for live language switching, unchanged customer values, preserved editor and icon controls, theme restoration from SQLite, light/dark tab colors, top navigation placement, accordion height and populated management action screenshots. The invoice-editor, compact-ui, invoice-settings and product-settings modes provide related regression coverage.
