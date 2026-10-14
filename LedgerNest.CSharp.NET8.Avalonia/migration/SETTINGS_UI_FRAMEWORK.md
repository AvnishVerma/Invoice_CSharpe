# Settings UI framework

`SettingsPageView` owns the header host, horizontally scrollable category navigation and active content host. Existing pages retain their shared `PageHeaderView` actions; `ScreenScaffoldView.ShowHeader` transfers their presentation into the shell above navigation. Do not add another category bar inside a page.

## Add a category

1. Register a `SettingsTabModel` title/icon in `MainWindow.SettingsView` and apply any existing authorization requirement.
2. Route its label in `SettingsContent` to the existing AXAML view and ViewModel. Unknown selections are ignored; missing initial selections fall back to the first registered category.
3. Use `ScreenScaffoldView` and `PageHeaderView`, then `SettingsSectionView` with Title, optional Description and Body. Bind existing fields through `FormFieldView`/`FormFieldsView`, choices through their templates, and segmented choices through `SegmentedChoiceView`.
4. Bind `SettingsActionsView.SaveCommand` to the existing save operation. Supply CancelCommand only where a real cancel workflow exists. Do not invent repository or persistence operations.

`SettingsStyles.axaml` is included once by the shell. It scopes 50-DIP inputs and 48-DIP action/toggle targets to `settings-content`, preserving editor/application sizing. Cards, typography and all theme brushes reuse application resources. Forms keep their existing scrolling and responsive container queries; avoid wrapping an already scrollable page in another scroll viewer.

`SettingsFeedbackView` accepts observable `SettingsFeedbackModel` flags for loading, empty, success, failure and unsaved states, plus workflow feedback. Pages should bind these flags to their actual operations; no save outcome is inferred from a button click.

Existing field errors, empty-state views, loading/status presentation, specialized numeric inputs, date/calendar templates, toggles and commands remain authoritative. No schema, persistence, logo, backup, PDF, licensing or update behavior changes. Existing pages without Cancel or unsaved-change tracking do not acquire fake operations.

C# components expose properties/state only; all newly added visual structure is AXAML. Legacy Ui.Field/Fields/Button/AppBar already load AXAML components. Ui.Card/Scroll still construct legacy controls, so the framework uses AXAML sections and scroll viewers directly rather than expanding those helpers.

Validation: the UiChecks settings checks cover registered-category fallback, selection callbacks, rejected unknown routes, shared validation state, command delegation and narrow-window touch dimensions.

## Validation results

The standard build initially encountered default desktop DLLs locked by running .NET hosts. Isolated solution builds use `-p:OutputPath=artifacts/floating-labels/` with no dependency/framework changes.

The initial invoice-tax failure came from a stale test fixture that did not resolve the unsaved-invoice confirmation before starting a fresh draft. Two further stale assertions assumed separate product metadata labels and preservation after the screenshot helper explicitly discarded the draft. The fixtures now resolve Discard/Keep Editing explicitly and inspect the existing combined metadata line. Production invoice tax/navigation behavior is unchanged.

Final complete UiChecks run: **1,096 checks passed**. Shared settings checks include category routing, validation state, commands, touch dimensions, responsive columns and feedback. Focused floating-label/numeric/company tests: **19 passed**. Final isolated solution build: **zero warnings/errors**.
