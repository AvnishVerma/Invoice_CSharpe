# Imported customer and product IDs

CSV imports use the existing transaction-safe AutoCodeGenerator and its persisted prefix, next number, padding and Auto Generate settings. New rows with no ID generate a code; import updates also fill missing IDs on legacy records when auto-generation is enabled. Existing non-empty IDs are retained. Ordinary edits retain their previous behavior.

Optional headers: `customer_id`/`customer_code` for customers and `product_id`/`product_code` for products. Spaces/case normalize through the existing CSV header handling. Supplied IDs on new rows are preserved and validated for uniqueness. With auto-generation disabled, new rows require a supplied ID. Allocation, record persistence and sequence advancement remain in the same existing database transaction; failed rows do not consume the sequence. No schema changes.

Regression tests cover sequential generation, prefix/padding/next number, reimport without ID changes, persisted IDs after reload, legacy blank IDs, manual IDs, disabled generation, alias headers and duplicate rejection.

Validation: full solution build passed with zero warnings/errors; six import cases and two themed dialog cases passed; complete application UI checks passed 1,096 assertions. Final refund/cancellation captures were visually reviewed.
