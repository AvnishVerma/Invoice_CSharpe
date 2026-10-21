# Modern touch-friendly invoice editor

The existing editor is redesigned directly in AXAML, using the same ViewModels, fields, commands and calculation/persistence workflow. No service, schema or business-logic changes.

- Shared application header and brand/theme resources retained.
- Clear customer, items, invoice details/options and total cards with consistent spacing and surfaces.
- Editor-only 48-DIP action buttons and icon targets, 50-DIP choice fields, and larger expander headers. Global application sizing is unchanged.
- Search is placed above items. Suggestions have larger touch rows. Existing shortcuts, barcode handling, custom-item and customer workflows remain bound to their original commands.
- Compact single-line item rows show name, unit/type, quantity, price, tax, discount, total and delete together. Numeric inputs are 36 DIP high, scoped to invoice items; existing two-way bindings and limits are preserved. Shared column headings avoid repeated labels. A horizontal scrollbar retains the single-line layout on narrow windows; full names remain available in tooltips.
- Wide view preserves the draggable divider and pins totals/action footer. The selected information tab scrolls independently from the item list.
- Invoice details open expanded in their dedicated information tab. Narrow screens move date/number into a summary strip to avoid title overlap.
- Narrow view stacks panels and keeps every document action accessible, with a full-width primary action. Existing customer-field responsive layout is reused; date/time fields stack when the details card is narrow.
- Existing localization helper and Font Awesome icon assets are reused. Keyboard shortcuts and accessibility names are retained or added.

Verification uses the existing invoice reference, navigation, customer/options and editing tests plus dedicated touch-layout tests at 1366, 1000, 800 and 640 pixels in both themes. Captures are emitted under the test output artifacts/invoice-touch folder.

Validation: solution build succeeded with zero warnings/errors (isolated artifacts/modern-invoice output, because the running desktop app locks its usual output). All 50 focused invoice tests passed, including four touch-layout cases and reference/edit/customer/options/navigation checks. The full headless UiChecks run passed 1,096 checks. Desktop and narrow captures were reviewed in light and dark themes. Native manual touch interaction was not performed.

Compact-row follow-up: replaced the multi-row item cards with a single aligned row and 36-DIP numeric fields. Build passed with zero warnings/errors; all eight focused invoice layout/reference tests passed. The updated checks verify a seven-column row, identical input baselines, reduced field sizes and the narrow-window horizontal-scroll fallback. Desktop capture reviewed.
Full compact-row validation also passed all 1,096 headless application checks.


Tabbed invoice information: the left panel now contains only items/search. The right panel uses five AXAML tabs: Customer, Invoice Details, Charges, Adjustments, and Tax Details, with totals below. Tab headers wrap at narrow widths and retain 48-DIP targets. Customer and invoice components are reused unchanged; options use scoped AXAML section classes to share their existing fields and commands without duplicate business state. Adjustments includes discount, notes and custom fields; Charges retains the existing signed charge/deduction rows. Invoice details open expanded when their tab is selected.


Tab lifecycle: options views track both visual attachment and DataContext replacement. The shared options model counts active view attachments so removing one tab view cannot disconnect charge-row updates required by another. This is presentation subscription handling only; monetary calculations and persistence are unchanged.

Tabbed-layout validation: final solution build passed with zero warnings/errors; all 50 focused invoice tests passed, including tab selection, section visibility and charge-row refresh after switching tabs at four widths. Full UiChecks passed all 1,096 checks. Final desktop/light and narrow/dark headless captures were reviewed.

Uniform tab children: InvoiceTabStyles.axaml now owns the shared 16-DIP card padding/radius, outline/surface, 36-DIP icon badges, 16-point headings, 48-DIP heading targets and 12-DIP content spacing. Customer, invoice details, charges, adjustments and tax reuse these classes. Former charge/discount-specific tinted nested cards were removed; existing fields, expanders, commands and visibility remain. Touch-layout checks assert the shared format in every tab.
Uniform-tab validation: solution build succeeded, the corrected test-project rebuild passed with zero warnings/errors, and all 14 focused invoice UI tests passed. The updated invoice-details capture was reviewed. The interrupted earlier test session is not counted as a pass.
