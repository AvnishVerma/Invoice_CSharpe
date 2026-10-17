# Modern touch-friendly invoice editor

The existing editor is redesigned directly in AXAML, using the same ViewModels, fields, commands and calculation/persistence workflow. No service, schema or business-logic changes.

- Shared application header and brand/theme resources retained.
- Clear customer, items, invoice details/options and total cards with consistent spacing and surfaces.
- Editor-only 48-DIP action buttons and icon targets, 50-DIP choice fields, and larger expander headers. Global application sizing is unchanged.
- Search is placed above items. Suggestions have larger touch rows. Existing shortcuts, barcode handling, custom-item and customer workflows remain bound to their original commands.
- Item cards replace the cramped fixed-width table. Quantity, price, tax and discount keep their existing two-way bindings and limits. Numeric fields adapt from four to two columns without horizontal scrolling.
- Wide view preserves the draggable divider and pins totals/action footer. Details and adjustments scroll together rather than competing for a fixed region.
- Invoice details start collapsed for quick item entry; their existing fields are available through the touch-sized header. Narrow screens move date/number into a summary strip to avoid title overlap.
- Narrow view stacks panels and keeps every document action accessible, with a full-width primary action. Existing customer-field responsive layout is reused; date/time fields stack when the details card is narrow.
- Existing localization helper and Font Awesome icon assets are reused. Keyboard shortcuts and accessibility names are retained or added.

Verification uses the existing invoice reference, navigation, customer/options and editing tests plus dedicated touch-layout tests at 1366, 1000, 800 and 640 pixels in both themes. Captures are emitted under the test output artifacts/invoice-touch folder.

Validation: solution build succeeded with zero warnings/errors (isolated artifacts/modern-invoice output, because the running desktop app locks its usual output). All 50 focused invoice tests passed, including four touch-layout cases and reference/edit/customer/options/navigation checks. The full headless UiChecks run passed 1,096 checks. Desktop and narrow captures were reviewed in light and dark themes. Native manual touch interaction was not performed.
