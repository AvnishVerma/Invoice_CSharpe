# Create Invoice layout

The invoice editor keeps customer name and phone visible, with business and contact details expandable. Product search sits directly above the line items and matches names, aliases, SKU and HSN/SAC. Ctrl+F focuses search, Escape dismisses suggestions, and Ctrl+S saves.

Document details, optional discounts, charges, notes and tax controls occupy a narrower side panel. The full totals breakdown wraps across the fixed footer above the document action buttons and Save. On narrower windows, the same controls move into a scrolling vertical layout without recreating the draft. The footer always shows the current totals and primary save action; secondary actions open the last saved document and are hidden only when the available width is too small. Invoice fields use compact input heights, approximately 20% smaller, without changing other screens.

This change does not alter persistence, invoice numbering or monetary calculations. Existing optional fields and settings continue to apply.

Validation: build the UI-check project, then run its executable with `--invoice-editor-only`. The focused checks cover search, adding an item, quantity and discount calculations, responsive draft preservation and saving. Screenshots cover empty, populated, medium and narrow layouts.
