# Reported UI issues — 2026-09-10

Source: the 11 rows pasted by the user from the Google Sheet. The Google Sheet itself requires authentication and has not been edited. `REPORTED_ISSUES_STATUS.csv` is an importable status update.

## 1. On Login screen Invalid password alerts are not showing

Inline login error is rendered inside the login card.

Verification: Visible invalid-password text and screenshot.

## 2. User Name sholud not be case sensitive.

Authentication and password verification use ordinal case-insensitive usernames; passwords remain case-sensitive.

Verification: Mixed-case ADMIN login through Enter.

## 3. Show First Time Setup screen if user (Admin) First Time login in application

First administrator login opens setup after any mandatory password change; completion persists.

Verification: First-time dialog and persisted completion check.

## 4. Login through enter key not working

Enter submits the login form.

Verification: Headless keyboard Enter authentication.

## 5. Multiple dashboard button is not working

Enabled dashboard layout selector and removed heading hit-test interception.

Verification: Enabled selector, layout selection, Simple Feed capture.

## 6. Add option to create a default customer that is selected automatically when new invoice is created

Customer creation/edit and existing-customer selection offer a default checkbox; new documents load the saved customer; clear option included.

Verification: Default survives model restart, populates new invoice, and can be cleared.

## 7. on selection on an item, same item getting add two times

Suggestion handler guards re-entry and clears selection before adding.

Verification: One selection produces exactly one invoice line.

## 8. Brand Logo is not getting save

Logo image contents are stored with settings and rendered when the company screen reopens; old file paths still load.

Verification: Embedded image persistence and rendered image after reopening. Native file picker needs manual smoke check.

## 9. Toggle buttons are highlighted after the toggle set to On

Removed outer selected highlight while retaining the on/off track and thumb appearance.

Verification: Checked toggle presenter remains transparent; screenshot reviewed.

## 10. on three dot button click dropdown should be opne instead of POP ups window.

Toolbar and row action buttons use anchored MenuFlyout dropdowns; destructive actions still confirm.

Verification: Both toolbar/row flyouts checked; row action executes; screenshot reviewed.

## 11. on invoice completion payment screen opens multiple times.

Saving payment closes the form; completion screen ignores repeat save shortcuts until a new document is started.

Verification: Payment form closes, one payment recorded, repeat Ctrl+S does not save again.

Validation uses Avalonia headless UI and SQLite fixtures. Screenshots are generated under `/tmp/ledgernest-reported-issues`. Native desktop/file-picker smoke testing and user acceptance are still required; this is not a claim of complete legacy parity or production readiness.

Final validation: build passed with zero warnings/errors; all 670 checks passed with local runtime major roll-forward. Visually reviewed the login-error, first-time setup, Simple Feed, dropdown and company logo/toggle captures.
