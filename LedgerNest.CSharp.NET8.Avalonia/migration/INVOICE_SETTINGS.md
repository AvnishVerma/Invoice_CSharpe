# Invoice settings behavior

The General, Branding, and Tax & GST controls follow `invoiso-main/lib/screens/settings/invoice_settings_screen_v2.dart`. Invoice defaults apply in `StartDocument`; editing a saved invoice continues to load its historical snapshot.

- Starting-number changes are rejected while any document exists, including trash. Prefixes and leading-zero preferences format output only; the stored numbering sequence stays unchanged.
- Logo sizes use the Flutter values 40/60/90/120; signature heights use 35/50/70. Existing numeric C# settings remain valid and appear as Custom when they do not match a named size.
- Signature and watermark uploads accept PNG/JPEG up to 2 MB. Watermarks are omitted on thermal output. Branding is applied at export time, as in the reference 
- application.
- Round-off uses decimal midpoint rounding away from zero. The extra rows and amount in words are presentation only; saved totals, payments, and balances retain their existing values. INR uses Indian number grouping; other currencies use international grouping.
- New invoices retain their creation time. Editing changes the date without replacing the original time; old midnight timestamps are not reconstructed.
- `LineHsnCodes` is an optional addition to the existing invoice snapshot JSON, populated for newly saved product lines. No SQLite schema migration is required. Existing snapshots without it continue to load and do not invent historical HSN codes. This change does not claim compatibility with a Flutter database.

Focused verification: `dotnet run --project tests/LedgerNest.UiChecks -- <output-directory> --invoice-settings-only`.

## Item, customer, column and custom field settings

The remaining sections follow the same Flutter settings screen. Fractional quantities and duplicate catalog products are validated when adding lines and again when saving. Independent custom items are not treated as duplicate catalog products. The Product/Service tag is available in the editor and PDF; PDF tags follow the reference's Both business-type setting.

Customer name, item name, price/rate and total remain mandatory output. Optional customer details require both their toggle and a value; GST identifiers also require Show GST Fields. Customer email and product descriptions are omitted on thermal receipts. The HSN column uses the same setting as Show GST Fields.

`LinePresentations` and `CustomFields` are optional invoice snapshot JSON properties. Product aliases, types and metadata are captured when saving; custom fields store a stable ID, label and value per invoice. Editing and cloning preserve historical presentation data. Later definition changes affect new invoices only. Definitions (including an explicitly empty list) are persisted in the existing settings table as `invoice.custom_field_definitions` JSON. New installations seed the thirteen reference transport/delivery labels.

Grid Classic prints enabled product metadata as table columns and nonempty invoice custom values in a three-column block. Other templates omit these features. Previous balance is calculated from earlier nondeleted invoices for the same saved customer and currency, excluding the current invoice, quotations and receipts. It is an export-only display and does not modify totals or payment records.

The C# Products table gains `ManufacturerName` (non-null TEXT with an empty default) through the existing guarded schema-upgrade path. The legacy source has manufacture-date metadata and accepts `manufacture_name` in its CSV schema; this change supports that import alias plus `manufacturer_name`. It does not assert compatibility with the Flutter SQLite database. Existing C# product IDs, values and other columns are retained.
