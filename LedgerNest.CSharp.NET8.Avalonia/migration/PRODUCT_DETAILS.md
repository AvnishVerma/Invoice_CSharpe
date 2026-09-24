# Product Details settings

The Product Details page follows the reference's fixed blue header, 240-pixel Save rail, scrollable field rows, and nested metadata switches. At narrow widths the Save button moves below the scrollable form. Name and Price are always enabled. Header bands share `Branding.HeaderColor` (`#002E78`) through `Ui.AppBar` and the `PageHeaderBrush` XAML resource.

Settings retain the existing `Product Details` section and label keys. The legacy `Advanced Information` key controls the visible “Product Metadata” master switch; renaming its UI does not lose existing preferences. Manufacturer Name is now selectable. Defaults enable all optional fields, matching Flutter's `ProductColumnsConfig`; existing persisted choices take precedence. No database migration is introduced.

Product add/edit forms, product-list column availability, product-search suggestions, and invoice-item controls use `ProductFieldVisible`. The metadata master hides its children without resetting them. Hidden existing values remain in the edit model and survive saving. Disabling Stock defaults newly created products to unlimited stock; it does not rewrite the inventory policy of existing products. Hiding a commercial field does not erase an existing product's price, tax, discount, or unit. Invoice PDF column settings remain separately controlled under Invoice Settings.

Products' Configure button opens this page directly. Save persists preferences using the existing settings store. Product-list column selection can further hide optional columns; Name and Price cannot be hidden.

Focused checks:

```powershell
dotnet build tests/LedgerNest.UiChecks --no-restore -p:OutputPath=bin/ProductSettingsCheck/
dotnet tests/LedgerNest.UiChecks/bin/ProductSettingsCheck/LedgerNest.UiChecks.dll artifacts/product-settings --product-settings-only
```

These cover SQLite preference persistence, required fields, unlimited-stock defaults, hidden-value preservation, metadata dependencies, invoice extra-cost behavior, rendered forms, list columns, header colors, and desktop/narrow layouts.
