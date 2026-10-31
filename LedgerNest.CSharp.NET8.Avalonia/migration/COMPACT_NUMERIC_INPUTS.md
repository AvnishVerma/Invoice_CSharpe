# Compact numeric inputs

The shared NumericUpDown style in App.axaml uses a 36-DIP default height, 24-DIP spin buttons, the existing 15-point text-input font, rounded outline, theme brushes and centered content. Explicit page-specific minimums remain in effect, including larger Settings touch targets and compact invoice rows.

The generic TextBox height style had overridden the numeric editor's automatic height. The numeric inner-editor override now follows the generic styles, so both the outer control and its text editor shrink correctly. Its existing border-free focus theme remains in place to prevent a second outline.

This is AXAML styling only. Numeric values, bounds, increments, formatting, bindings and spinner behavior remain owned by the existing Avalonia control and ViewModels. No database or business changes.

CompactNumericInputTests render Barcodes, Inventory and Price Manager in light/dark themes and check dimensions, spin-button width, focus, text editing and ViewModel binding. Existing NumericInputFocusTests, InvoiceTouchLayoutTests and DialogLayoutTests cover related regressions.

Validation: solution build completed with zero errors and 12 existing NU1902/NU1903 dependency warnings. All 14 focused numeric-input, focus, invoice-layout and dialog-layout tests passed after the final styling corrections. The full 1,096 UI checks passed earlier during this task, before the final inner-editor height and enabled-button selector corrections. Representative actual-view captures were reviewed in light and dark themes.
