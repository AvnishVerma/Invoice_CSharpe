# Shared tooltips

App.axaml enables UiTooltips for buttons, form inputs, selections, toggles, tabs and menus. The attached presentation behavior supplies missing hints from accessible names, ActionButtonView captions, headers, visible action text, input placeholders or adjacent labels. It never reads input values. Icon-only invoice result actions have explicit tooltips and accessible names.

Explicit ToolTip.Tip values and existing ActionButtonView.Tooltip bindings take precedence. Generated hints are refreshed on pointer entry and label changes, using UiLocalization.Translate. Nested input template controls defer to their owning input. Disabled controls retain their hints.

For a new icon-only action, provide AutomationProperties.Name with a meaningful action, or an explicit ToolTip.Tip for more detailed help. Do not use internal control names or user data as tooltip text.

TooltipCoverageTests checks shared action fallbacks, standard buttons, icon actions, fields, password privacy, dropdowns, checkboxes, numeric inputs, disabled custom help and dynamic captions.

Validation: solution build passed with zero errors and 12 existing dependency warnings. All 5 focused tooltip/invoice/calendar checks and all 1,096 application UI checks passed.
