# Dialog layout corrections

Refund and cancellation bodies already own their commands; OverlayDialogViewModel now omits the redundant default Close footer for these two views, and OverlayDialogView collapses the entire empty footer row. Commands and financial logic are unchanged.

The shared overlay uses consistent header padding, typography, accessory spacing and close-button dimensions, including product editors. Customer details use the shared header instead of an empty header followed by a second title. Read-only customer/product fields use shared input dimensions and themed colors; editing counters are omitted from customer display rows.

Refund lines reserve a separate labelled quantity column with space for the value/spinners and wrap the description independently. Refund and cancellation action groups align to the right; cancellation has a visible destructive outline. Missing background resource references were replaced by the existing AppCardBrush.

Headless tests render both dialogs in light/dark themes, assert no duplicate footer, verify shared title sizing and quantity width, and exercise the production refund-line loading/total notifications. Screenshots are saved under the test output artifacts/dialog-layouts folder.

Validation: full solution build passed with zero warnings/errors; six import cases and two themed dialog cases passed; complete application UI checks passed 1,096 assertions. Final refund/cancellation captures were visually reviewed.
