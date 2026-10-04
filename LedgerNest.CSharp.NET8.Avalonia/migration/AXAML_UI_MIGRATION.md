# AXAML UI migration

The migration is ongoing. Do not treat the whole application as AXAML-only until the remaining builders are removed and the final audit passes.

## Ownership

- AXAML owns controls, templates, layout, icons, tooltips, dimensions, colors, and visual states.
- Presentation models own form metadata, values, derived visibility/enabled state, validation state, and commands.
- Code-behind initializes named AXAML views, attaches/detaches model subscriptions, handles calendar flyout events, and invokes native storage/printing services.
- ScottPlot's chart control is declared in AXAML. Its series data and plotting calls remain in code-behind because ScottPlot exposes these through its plotting API; these calls create no Avalonia controls.

## Converted areas

Shared shell/header/action/field components, authentication/onboarding/confirmation overlays, Company Information, Backup, Software Information, License, PDF Settings, Product Details, Accessibility, generic settings, invoice-setting input/toggle/image widgets, Reports, printer selection, payment/user/custom-item dialogs, Publisher Console, and product record editing use AXAML visuals.

Product editing uses form metadata and reusable input templates. Its checkbox, calendar, optional-field, custom-unit, unlimited-stock, type-selection, advanced-section, and footer visuals are declarative. The purple save/cancel treatment preserves the existing product-editor design; action dimensions use the common action styles.

## Remaining migration

Management tables/action menus; invoice editor builders; invoice settings navigation/sections; responsive invoice workspace adapters; shared legacy layout helpers; and legacy settings-header extraction still require migration or final removal. The final audit must include indirect `Ui.*` helpers and target-typed constructors, not only `new Button` searches.

## Validation

`tests/LedgerNest.UiChecks` exercises bound controls and critical workflows using isolated data. Unit, integration, and ViewModel suites remain in the solution. Run focused checks after each batch and the complete suites before declaring the overall migration finished. Headless screenshots verify layout; they do not substitute for a native desktop smoke test of platform dialogs.
