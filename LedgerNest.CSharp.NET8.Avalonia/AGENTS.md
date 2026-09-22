# Repository Guidelines

## Project Structure & Architecture

`LedgerNest.CSharp.sln` is the active .NET solution. Keep its layered dependency direction intact: `src/LedgerNest.Domain` contains entities and contracts and has no project references; `src/LedgerNest.Application` contains services and ViewModels; `src/LedgerNest.Infrastructure` owns EF Core/SQLite persistence; and `src/LedgerNest.Desktop` contains Avalonia views, startup, PDF, and printing code. Assets live under `src/LedgerNest.Desktop/Assets`.

`tests/LedgerNest.UiChecks` provides headless UI, calculation, persistence, report, and printing checks. `invoiso-main/` is the legacy Flutter reference—consult its behavior and tests before porting a feature. Migration decisions and compatibility notes belong in `migration/`.

## Build, Test, and Run

Run these from the repository root:

```powershell
dotnet restore LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.sln --no-restore
dotnet run --project tests/LedgerNest.UiChecks
dotnet run --project src/LedgerNest.Desktop
```

Restore before a clean build; the second command verifies compilation without changing dependency resolution. The UI-check project is the primary automated verification command. The desktop app needs a graphical session. Use the SDK pinned in `global.json`; do not change target frameworks simply to match a locally installed SDK.

## Coding Style & Conventions

Use C# file-scoped namespaces, four-space indentation, nullable reference types, and implicit usings. Name public types and members in `PascalCase`, local variables and parameters in `camelCase`, and interfaces with an `I` prefix (for example, `IPrintService`). Keep Avalonia compiled-binding types aligned with their ViewModels. Use CommunityToolkit.Mvvm patterns for presentation state; keep calculations and persistence out of code-behind. Monetary calculations must use `decimal` and preserve tax, discount, and rounding behavior from the Flutter reference.

## Testing Guidelines

Add focused checks to `tests/LedgerNest.UiChecks/Program.cs` for changed behavior, especially totals, SQLite persistence, and ViewModel interactions. Follow the existing self-contained check style and use isolated temporary databases. Run the UI checks before opening a pull request; if changing `invoiso-main/`, run `flutter test` there when Flutter is available.

## Commits & Pull Requests

Use concise, imperative commit subjects such as `Add PDF export functionality` or `Refactor UI actions`. Keep each commit focused. Pull requests should summarize the user-visible change, note migration or database-compatibility implications, list commands run, and include screenshots for visual Avalonia changes. Do not commit `bin/`, `obj/`, IDE files, or unrelated legacy changes.

## Data and Migration Safety

Validate the legacy SQLite schema and business rules before adding EF migrations or claiming compatibility with existing data. Do not overwrite reference material in `invoiso-main/` or `Lagacy Code/`.
