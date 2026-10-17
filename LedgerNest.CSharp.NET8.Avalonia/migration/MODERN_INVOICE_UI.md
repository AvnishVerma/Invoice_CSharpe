# Modern touch-friendly invoice editor

The existing editor is redesigned directly in AXAML, using the same ViewModels, fields, commands and calculation/persistence workflow. No service, schema or business-logic changes.

- Shared application header and brand/theme resources retained.
- Clear customer, items, invoice details/options and total cards with consistent spacing and surfaces.
- Editor-only 48-DIP action buttons and icon targets, 50-DIP choice fields, and larger expander headers. Global application sizing is unchanged.
- Search is placed above items. Suggestions have larger touch rows. Existing shortcuts, barcode handling, custom-item and customer workflows remain bound to their original commands.
- Compact single-line item rows show name, unit/type, quantity, price, tax, discount, total and delete together. Numeric inputs are 36 DIP high, scoped to invoice items; existing two-way bindings and limits are preserved. Shared column headings avoid repeated labels. A horizontal scrollbar retains the single-line layout on narrow windows; full names remain available in tooltips.
- Wide view preserves the draggable divider and pins totals/action footer. Details and adjustments scroll together rather than competing for a fixed region.
- Invoice details start collapsed for quick item entry; their existing fields are available through the touch-sized header. Narrow screens move date/number into a summary strip to avoid title overlap.
- Narrow view stacks panels and keeps every document action accessible, with a full-width primary action. Existing customer-field responsive layout is reused; date/time fields stack when the details card is narrow.
- Existing localization helper and Font Awesome icon assets are reused. Keyboard shortcuts and accessibility names are retained or added.

Verification uses the existing invoice reference, navigation, customer/options and editing tests plus dedicated touch-layout tests at 1366, 1000, 800 and 640 pixels in both themes. Captures are emitted under the test output artifacts/invoice-touch folder.

Validation: solution build succeeded with zero warnings/errors (isolated artifacts/modern-invoice output, because the running desktop app locks its usual output). All 50 focused invoice tests passed, including four touch-layout cases and reference/edit/customer/options/navigation checks. The full headless UiChecks run passed 1,096 checks. Desktop and narrow captures were reviewed in light and dark themes. Native manual touch interaction was not performed.

Compact-row follow-up: replaced the multi-row item cards with a single aligned row and 36-DIP numeric fields. Build passed with zero warnings/errors; all eight focused invoice layout/reference tests passed. The updated checks verify a seven-column row, identical input baselines, reduced field sizes and the narrow-window horizontal-scroll fallback. Desktop capture reviewed.
Full compact-row validation also passed all 1,096 headless application checks.
