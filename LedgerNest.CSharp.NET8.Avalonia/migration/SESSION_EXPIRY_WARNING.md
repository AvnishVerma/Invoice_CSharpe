# Timed local sessions and expiry warning

## Architecture findings and agreed scope

LedgerNest authenticates SQLite AppUser records with PasswordCredentials. It has no access token, refresh token, HTTP authentication interceptor, remote refresh/revoke endpoint, or existing expiry metadata. The user selected timed local sessions with account revalidation instead of external token integration. This implementation does not invent bearer tokens or claim remote token refresh.

Authenticated sessions last 30 minutes. WarningPeriod is five minutes. Both durations are centralized in Application/SessionExpiryMonitor.cs; no database schema or settings keys change. Expiration is in memory, so restarting the app requires its existing login flow.

## Responsibilities

- SessionExpiryMonitor: injected TimeProvider, actual UTC deadline calculations, countdown and progress, urgency states, one renewal in flight, cancellation/generation protection, successful-renewal feedback, expiry and exit coordination. The desktop host drives one dispatcher tick per second and ticks on activation/validation after sleep. Missed ticks never extend the deadline.
- MainWindowViewModel.Authentication: initializes/stops the lifetime at sign-in/out, revalidates the current account asynchronously using the existing SQLite factory, compares identity/role/credential revision/password-change state, and extends the deadline only after a successful check. ValidateSession, CanAccessWorkspace, expiry-aware role delegates and the existing permission gate enforce expiry, including the interval before a delayed UI tick. Existing SessionVersion guards invalidate outstanding workspace operations after sign-out.
- MainWindow and ShellView: own the single UI timer, stop it after logout/window close/model replacement, preserve login routing, disable underlying content during the warning, and block workspace shortcuts while the warning is shown.
- SessionExpiryDialogView.axaml: theme-resource surface, rounded elevated card, Font Awesome clock, gradient accent, large MM:SS countdown, progress, attention/urgent treatment, processing/success feedback, accessible actions and footer. All visual controls are AXAML.
- SessionExpiryDialogView.axaml.cs: presentation-only native fade/scale and backdrop transitions, gentle digit fade, progress interpolation, focus handling and animation cleanup. A single short-lived, injectable TimeProvider timer bounds each transition to 250 ms and posts completion to the UI thread; generation guards reject stale callbacks. Rendering-clock completion never gates login. Windows SPI_GETCLIENTAREAANIMATION determines reduced motion each opening; other platforms conservatively suppress motion. A presentation override supports deterministic UI checks.

## Security behavior

Continue Session rechecks the account without retaining a password. Missing/changed accounts and storage failures terminate the local session. No credentials are displayed or logged. The old deadline remains authoritative until renewal completes. Logout/expiry cancels renewal and increments its generation; a late completion cannot restore the old or a newer session. Exit/expiry bypass unsaved-document prompts, clear local authentication and the in-memory invoice draft, and route to login immediately. Cleanup requires no database access. The expired message can fade over the login screen, but navigation and access invalidation never wait for animation.

Only the host UI thread drives monitor state. Interlocked single-flight protection additionally rejects duplicate renewal starts. There are no authenticated HTTP API calls in this local authentication flow; HTTP token rotation, server revocation and interceptor tests are inapplicable to the agreed implementation.

## Accessibility and animation

Entrance is 250 ms with a 220 ms backdrop fade; exit is 250 ms. Countdown digit opacity settles over 180 ms and progress interpolates over 850 ms. Renewal success displays for at least 350 ms before the next one-second tick dismisses the dialog. Reduced motion suppresses all these transitions. The title and help text describe the dialog; Tab cycles within the card, Continue receives initial focus, Escape does not dismiss the security warning, and countdown announcements are disabled. Only status changes are polite live announcements. Existing focus styling and theme resources remain in use.

## Verification

SessionExpiryMonitorTests use an injected fake clock and controlled asynchronous responses for warning boundaries, countdown, urgency, sleep catch-up, renewal, duplicate starts, failure, automatic exit and stale responses. LocalSessionRenewalTests exercise the existing SQLite account and dirty-invoice workflows. SessionExpiryDialogTests render light/dark and narrow-window states, check keyboard focus and reduced motion, capture visual evidence, and verify the real shell returns to login at expiry.

Modified existing files: MainWindow.axaml (warning host and input isolation), MainWindow.axaml.cs (session timer and shortcuts), Views/ShellView.cs (timer lifecycle), MainWindowViewModel.Authentication.cs (local lifetime, revalidation and forced logout), MainWindowViewModel.Permissions.cs (deadline enforcement).

Added files: Application/SessionExpiryMonitor.cs; Desktop/Views/SessionExpiryDialogView.axaml and its presentation code-behind; Desktop.Tests/UI/SessionExpiryMonitorTests.cs; Desktop.Tests/UI/SessionExpiryDialogTests.cs; Desktop.Tests/Authentication/LocalSessionRenewalTests.cs; this document.

The animation lifecycle tests use a fake presentation TimeProvider, so the deadline/cancellation behavior is deterministic even when the headless rendering clock is paused. Native transition objects and light/dark rendering remain exercised. Manual operating-system screen-reader testing has not been performed.

## Build and test commands

The solution was built with the pinned SDK and isolated output (to avoid replacing assemblies held by the running desktop application):

```powershell
dotnet build LedgerNest.CSharp.sln --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:OutputPath=artifacts/session-expiry/
dotnet vstest tests/LedgerNest.Desktop.Tests/artifacts/session-expiry/LedgerNest.Desktop.Tests.dll /TestCaseFilter:"FullyQualifiedName~SessionExpiry|FullyQualifiedName~LocalSessionRenewal|FullyQualifiedName~AuthenticationTests|FullyQualifiedName~SessionIsolationTests"
dotnet tests/LedgerNest.UiChecks/artifacts/session-expiry/LedgerNest.UiChecks.dll artifacts/session-expiry-complete-checks
```

Final solution build: zero errors, 12 existing NU1902/NU1903 warnings for System.Security.Cryptography.Pkcs 9.0.11. All 32 focused checks passed. Captures were reviewed at desktop and narrow sizes in light/dark themes and saved under artifacts/session-expiry-preview/.

Full final application UI checks: all 1,096 passed against the completed compiled implementation. No database/schema changes or new dependencies were introduced.
