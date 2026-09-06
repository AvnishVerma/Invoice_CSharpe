# Repository guide

## Project layout

The active C# solution is `LedgerNest.CSharp.NET8.Avalonia/LedgerNest.CSharp.sln`.
It is a migration scaffold targeting .NET 8, Avalonia 11, MVVM, and EF Core SQLite.
Paths below are relative to `LedgerNest.CSharp.NET8.Avalonia/`:

- `src/LedgerNest.Domain`: entities and repository contracts; no project dependencies.
- `src/LedgerNest.Application`: application services and ViewModels; references Domain.
- `src/LedgerNest.Infrastructure`: SQLite persistence and dependency registration; references Domain.
- `src/LedgerNest.Desktop`: Avalonia views, desktop startup, and composition; references Application and Infrastructure.
- `invoiso-main/`: original Flutter/Dart application and its tests, used to understand migration behavior.
- `migration/MIGRATION_MAP.md`: migration approach and suggested implementation order.
- `migration/SOURCE_INVENTORY.json`: source inventory.

## Development commands

Run from the repository root:

```bash
dotnet restore LedgerNest.CSharp.NET8.Avalonia/LedgerNest.CSharp.sln
dotnet build LedgerNest.CSharp.NET8.Avalonia/LedgerNest.CSharp.sln --no-restore
dotnet run --project LedgerNest.CSharp.NET8.Avalonia/src/LedgerNest.Desktop
```

The desktop app requires a graphical session and a compatible .NET runtime.
The project targets .NET 8; do not change the target framework merely to match an installed SDK.
Headless C# UI and calculation checks live in `tests/LedgerNest.UiChecks`. For behavior changes, add focused tests where appropriate and report which checks actually ran.
Flutter reference tests live in `invoiso-main/test/`; run `flutter test` from `invoiso-main/` when modifying that application and Flutter is available.

## Implementation guidance

- Preserve the project dependency boundaries above.
- Follow existing C# conventions: file-scoped namespaces, four-space indentation, nullable reference types, and implicit usings.
- Use MVVM and the existing CommunityToolkit.Mvvm patterns for presentation state. Keep business calculations out of view code-behind.
- Avalonia compiled bindings are enabled by default; keep binding types consistent with ViewModels.
- Use decimal arithmetic for monetary values and verify rounding, tax, and discount behavior against the relevant Dart implementation and tests.
- Consult the migration map and original business logic before porting features. The scaffold does not yet represent complete feature parity.
- Validate the original SQLite schema and business rules before introducing database migrations or claiming compatibility with existing data.
- Preserve unrelated local changes and legacy source material, including `Lagacy Code/` at the repository root.
- Do not hand-edit generated `bin/` or `obj/` files or include build output in source changes.
