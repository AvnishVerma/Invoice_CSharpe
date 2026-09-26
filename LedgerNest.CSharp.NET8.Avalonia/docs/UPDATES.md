# Application updates and notifications

LedgerNest checks a publisher-controlled JSON manifest over HTTPS. This keeps update discovery separate from installation: the app tells the user about a verified release and opens the publisher's download page only after the user chooses **Download update**.

## Configure the update channel

An administrator opens **Settings → Software Info**, enters the HTTPS manifest URL, and selects **Save update channel**. The URL is stored in the local SQLite settings database under `updates.manifest_url`.

Use an HTTPS endpoint that your organization controls. Do not use a URL that accepts arbitrary user-provided manifests.

## Manifest format

```json
{
  "version": "4.5.0",
  "downloadUrl": "https://downloads.example.com/ledgernest/4.5.0/LedgerNest-Setup.exe",
  "releaseNotes": "Improved invoice settings and fixed dark-mode controls.",
  "publishedAt": "2026-09-26T10:00:00Z",
  "mandatory": false
}
```

`version` must be a three-part .NET version, such as `4.5.0`. URLs must use HTTPS. The check rejects invalid versions, unsafe download URLs, oversized manifests, and failed HTTP responses.

## Release process

1. Build and sign the Windows installer or MSIX package.
2. Publish the installer to the HTTPS download location.
3. Publish the manifest after the installer is available.
4. Start the app or use **Check for updates**. An available release creates an in-app notification and toast.

The app deliberately does not replace its own running executable. Installing an update remains a visible user action, which preserves Windows installer signing and permission prompts.
