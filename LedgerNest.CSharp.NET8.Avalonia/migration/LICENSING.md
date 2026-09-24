# LedgerNest licensing

## Implemented policy

LedgerNest accepts publisher-signed license files. A license is issued for one Device ID and contains its customer, unique license ID, product, version, issue/activation times, expiry and entitlements. Paid licenses may expire or be perpetual. Signed trials last at most 30 days; a trial starts at its signed issue time, not each import. There is no automatically resetting local trial.

Without an active `business.write` entitlement, invoice/quotation/receipt saves, customer/product saves and deletes, payments, CSV imports and document trash/deletion operations are rejected at the ViewModel operation boundary. Existing data, PDF/CSV exports, account administration, settings and backup/recovery stay available. Expiry is checked on each protected operation and refreshed in the UI every minute and when the window becomes active.

There is no activation server configured or implemented in this first version. The publisher issues files manually; the customer imports them under **Settings → License**, or pastes their contents. Renewals replace the current activation only after successful verification and storage. A malformed or wrong-device import preserves the previous license.

## Publisher setup

Run from the solution root using its pinned SDK:

```powershell
dotnet run --project tools/LedgerNest.LicenseTool -- keygen --private .local/licensing/issuer.private.pem --public src/LedgerNest.Desktop/Licensing/public-key.pem
```

The tool securely prompts for a password of at least 16 characters and saves a password-encrypted private key. Keep this key and password on the publisher's machine, with a secure backup; neither belongs in a customer installer. Private key paths and generated customer licenses are ignored by Git. The public key can be committed and must be embedded in customer builds. The tool never overwrites existing output files.

```powershell
dotnet publish src/LedgerNest.Desktop -c Release -o artifacts/desktop
```

Release builds fail if the public-key file is absent. An alternative key path can be supplied using `-p:LedgerNestLicensePublicKeyFile=C:/secure/public-key.pem`. Debug builds without a public key stay read-only; there is no Debug bypass or runtime setting that disables verification. No production keypair is included in this repository. Keep using the same publisher key for renewals; key rotation requires a separately planned client update.

After the customer copies their 64-character **Device ID** from Settings → License, issue a license (replace the example ID with the customer's actual ID):

```powershell
dotnet run --project tools/LedgerNest.LicenseTool -- issue --private .local/licensing/issuer.private.pem --customer "Acme Ltd" --device DEVICE_ID_FROM_APP --days 365 --out .local/licensing/acme.ledgerlicense
```

Use `--trial --days 30` for a trial, or `--perpetual` instead of `--days` for a perpetual paid license. Each issued file grants `business.write` for exactly one Device ID. Issue a separate license per device and maintain a publisher-side customer/license register. There is no online concurrent-device counter or remote revocation in an offline file.

```powershell
dotnet run --project tools/LedgerNest.LicenseTool -- verify --public src/LedgerNest.Desktop/Licensing/public-key.pem --device DEVICE_ID_FROM_APP --file .local/licensing/acme.ledgerlicense
```

Automation can supply `LEDGERNEST_SIGNING_PASSWORD` through a secret environment variable; do not put passwords on command lines or in source files. The utility prints no keys or passwords. The publisher tool is a separate executable and is not referenced or packaged by the desktop project.

## Storage and security boundaries

`LicenseVerifier` in Application verifies RSA-PSS/SHA-256 signatures over the exact decoded JSON payload bytes with .NET's [RSA verification API](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa.verifydata?view=net-10.0). The issuer generates 3072-bit RSA keys and encrypted PKCS#8 private PEM files. The client accepts only a SubjectPublicKeyInfo public key, never an embedded private key. Schema/product checks, device binding, signed time limits and entitlements run after signature verification.

Activation is stored atomically under `%LOCALAPPDATA%/LedgerNest/Licensing/activation.json`, separately from invoice databases. Backup restores do not transfer or reset activation. Windows Device IDs hash the OS MachineGuid with a product-specific prefix; Linux uses `/etc/machine-id`. Platforms without an OS machine ID use a persisted installation identity. Raw OS IDs are not displayed or transmitted. Reinstalling the OS or changing an installation identity requires reissuing the license.

The store records the latest observed UTC time and detects ordinary backwards clock changes beyond five minutes, including across restarts. This is best-effort offline detection, not tamper-proof trusted time. A person controlling the computer can patch the application, edit/delete local state or manipulate its clock. Online leases and a trusted server are required for stronger expiry enforcement, revocation, automatic trials, payment integration and centralized device-seat limits. No network request or customer-data transmission is made by this implementation.

The licensing change introduces no invoice database schema migration. The interfaces live in Domain; verification/service logic in Application; filesystem and OS identity access in Infrastructure; composition, UI and existing operation guards in Desktop. Ordinary headless checks inject an explicit test entitlement; licensing checks use real RSA signatures and the production verifier/service.

## Verification

```powershell
dotnet build tests/LedgerNest.UiChecks --no-restore -p:OutputPath=bin/LicensingCheck/
dotnet tests/LedgerNest.UiChecks/bin/LicensingCheck/LedgerNest.UiChecks.dll artifacts/licensing --licensing-only
dotnet build tools/LedgerNest.LicenseTool --no-restore
```

The focused checks cover signature/payload tampering, wrong keys/devices/products, schema checks, trial and perpetual licenses, UTC expiry boundaries, clock rollback/reimports, failed writes, persistence, renewals, business-operation guards, retained exports, and activation screen states.
