# Shared Settings UI Framework

The Avalonia desktop application uses one settings shell for every settings category. Keep settings navigation and shared touch styling in `src/LedgerNest.Desktop/Views/SettingsPageView.axaml`; keep tab selection and content lifecycle in `SettingsPageView.axaml.cs`; keep the category-to-view mapping in `MainWindow.SettingsView()` and `SettingsContent(string)` in `Views/SettingsView.cs`.

## Existing architecture

- `SettingsPageModel` owns the tabs, selected category, current page content, and extracted page header.
- `SettingsTabModel` represents a category and its selected state.
- `SettingsPageView` renders the common header, horizontally scrollable category navigation, and shared content surface.
- `SettingsContent(string)` routes each category to its existing view. Do not duplicate the settings shell inside individual categories.
- Existing `Ui.Card`, `Ui.Fields`, `Ui.Field`, `Ui.Button`, `Ui.Scroll`, and `Ui.AppBar` helpers should be reused to keep card, field, action, and header behavior consistent.

## Touch and responsive behavior

Settings navigation has a 48-DIP minimum target and scrolls horizontally when all categories do not fit. Controls within the settings content surface receive settings-scoped minimum heights: 50 DIP for text, selection, date, and numeric inputs, and 48 DIP for buttons, checkboxes, and switches. These styles are intentionally scoped to settings so invoice editing and other workflows are not changed unintentionally.

## Adding a settings category

1. Add a `SettingsTabModel` to the tab list in `MainWindow.SettingsView()`.
2. Add its route to `SettingsContent(string)` in `Views/SettingsView.cs`.
3. Build the page using existing `Ui` helpers and preserve the current model's validation, save, and persistence behavior.
4. Use `Ui.AppBar(title, actions...)` for the page header so the shared shell can extract and render it consistently.
5. Run the UI checks from `LedgerNest.CSharp.NET8.Avalonia`: `dotnet run --project tests/LedgerNest.UiChecks`.

Do not add a second tab strip, navigation shell, or settings-specific data persistence layer inside a category view.
