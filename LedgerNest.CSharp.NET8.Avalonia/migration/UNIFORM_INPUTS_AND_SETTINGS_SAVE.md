# Uniform inputs and Settings Save

Single-line TextBox controls use shared `TextInputHeight` (50 DIP), `TextInputFontSize` (15), `TextInputPadding`, and `TextInputIconPadding` resources in App.axaml. Existing component overrides now use those resources. Widths remain responsive; multiline editors keep their larger/wrapping layouts. Numeric inner editors fill their outer control and retain a single outline. Buttons and other application control dimensions are not globally changed.

SettingsPageView owns a persistent bottom-right Save action. Existing Save buttons remain command/state sources and are hidden declaratively only within the shared shell. The footer binds their original Command and enabled state; no save/persistence behavior is replaced. Standalone views retain their existing controls. Users, Backup, License activation and other categories without a Save operation do not receive a redundant Save button.

For an AXAML page's existing plain Save button, add `settings-save-source` to Classes. ActionButtonView recognizes standard Save captions automatically, including legacy Invoice Settings composition. The footer uses the shared ActionButtonView with the existing primary styling, Font Awesome save icon and 48-DIP height. Navigation changes replace the command source, preventing stale Save operations.

Tests cover shared input height/font and existing focus, validation and numeric behavior; footer tests inspect original command identity, single visible Save action and bottom-right placement at wide/narrow widths. Full UI checks retain persistence and PDF dirty-state verification.

Validation completed: full solution build passed with zero warnings/errors; 22 input, focus, company and persistence tests passed; both wide/narrow Save-placement tests passed; complete UI suite passed 1,096 checks. Company Information capture confirms the single bottom-right Save action.
