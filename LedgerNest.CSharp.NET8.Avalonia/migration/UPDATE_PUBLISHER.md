# LedgerNest Update Publisher

`LedgerNest.UpdateServer` hosts the release manifest used by Desktop clients and an administrator console.

## Run locally

```powershell
$env:LEDGERNEST_PUBLISHER_KEY = "replace-with-a-long-random-secret"
dotnet run --project src/LedgerNest.UpdateServer --urls http://localhost:5080
```

Open `http://localhost:5080/admin`, enter the API key, publish a release, and verify `http://localhost:5080/api/manifest`.

Deploy the server behind HTTPS. Configure every Desktop installation with `https://your-host/api/manifest` under **Settings → Software Information**. Clients check at startup and silently every 30 minutes.

The API key must be supplied through `LEDGERNEST_PUBLISHER_KEY` in production; do not put a real key in `appsettings.json`.
