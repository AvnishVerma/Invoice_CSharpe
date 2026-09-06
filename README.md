# LedgerNest.CSharp

Initial C#/.NET 8 + Avalonia UI + SQLite migration scaffold for the uploaded LedgerNest Flutter application.

## Target stack
- .NET 8
- Avalonia UI 11
- MVVM
- Microsoft.Extensions.DependencyInjection / Configuration
- Microsoft.EntityFrameworkCore.Sqlite
- CommunityToolkit.Mvvm

## Projects
- `src/LedgerNest.Domain` - entities and domain contracts
- `src/LedgerNest.Application` - use cases/services/DTOs
- `src/LedgerNest.Infrastructure` - EF Core SQLite persistence
- `src/LedgerNest.Desktop` - Avalonia desktop application

## Migration approach
The Flutter/Dart application is not translated mechanically. Its business concepts are mapped into C# domain entities, repositories and ViewModels. Existing SQLite schema/data should be migrated after validating the Dart schema and business rules.

## Run
```bash
dotnet restore
dotnet build
dotnet run --project src/LedgerNest.Desktop
```
