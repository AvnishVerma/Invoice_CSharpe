# Company Information reference layout

The active Avalonia `CompanySettingsView` retains the shared page header and Settings shell. Its desktop logo column is 240 px wide, with a separate form ScrollViewer and an anchored Save action. AXAML container queries stack the logo panel over the form below 720 px and collapse company field pairs below 480 px. `CompanyFieldView` supplies transparent floating labels, Font Awesome icons, validation messages, and the existing address expansion workflow.

The page continues to use the existing Company Info fields, settings keys, payment collections, and `MainWindowViewModel.SaveSettings`. No database migration or invoice/PDF generation change is introduced. The previously empty Company Logo Position selector incorrectly referenced the Show on PDF boolean; the screen now exposes that boolean as the logo visibility toggle. Actual invoice logo positioning remains in Invoice Settings / Branding.

Logo replacement and removal use the existing persisted base64 value. Upload validation permits PNG/JPG, at most 2 MB and 1080x1080 pixels. The presentation Save command prevents reentry and retains edits after validation/database failure. UPI and bank account configuration remain below the primary form.

The Flutter reference has individual company-field PDF visibility preferences that do not currently exist in Avalonia. Those preferences were not added as part of this UI-only redesign because doing so requires changing the PDF behavior excluded by the request. Logo, UPI QR, and bank-detail visibility remain supported. Flutter source is unchanged.

Validation: CompanyInformationTests cover persisted fields/business type/payment preferences, logo replacement/removal/validation, database save failure and retry, and desktop/narrow Light/Dark rendering. Existing SettingsPersistenceTests, BankAccountParityTests, and the compact UiChecks suite cover dependent workflows.
