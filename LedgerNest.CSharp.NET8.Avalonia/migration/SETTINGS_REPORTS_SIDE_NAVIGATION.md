# Settings and Reports side navigation

The existing SettingsPageView and ReportsPageView now render their category navigation on the left in AXAML. No routing, models, persistence, authorization or report calculations changed.

SideNavigationStyles.axaml shares the theme-aware rail surface/divider and width: 200 DIP normally, 156 DIP at view widths of 700 DIP or less. Navigation scrolls vertically and labels wrap where needed.

Settings keeps its existing header above both columns, content to the right, and shared Save footer below the content. Reports keeps its header/Refresh action and places the existing currency/period controls below report navigation in the same scrollable rail.

SideNavigationLayoutTests cover both views at 1200/light and 640/dark, selection/content routing, refresh, vertical placement, scrolling policies, retained report filters and touch targets. SettingsSavePlacementTests verify the original shared Save commands and placement. The declarative architecture assertion now checks side-by-side settings columns.

Validation: solution build completed with zero errors and 12 existing NU1902/NU1903 dependency warnings for System.Security.Cryptography.Pkcs 9.0.11. All seven focused navigation/save/layout checks and all 1,096 headless application checks passed. Wide/light and narrow/dark captures were reviewed, including the real Company Information and Revenue Reports screens. Native manual interaction was not performed.
