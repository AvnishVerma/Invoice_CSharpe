# Invoiso.CSharp

Initial C#/.NET 8 + Avalonia UI + SQLite migration scaffold for the uploaded Invoiso Flutter application.

## Target stack
- .NET 8
- Avalonia UI 11
- MVVM
- Microsoft.Extensions.DependencyInjection / Configuration
- Microsoft.EntityFrameworkCore.Sqlite
- CommunityToolkit.Mvvm

## Projects
- `src/Invoiso.Domain` - entities and domain contracts
- `src/Invoiso.Application` - use cases/services/DTOs
- `src/Invoiso.Infrastructure` - EF Core SQLite persistence
- `src/Invoiso.Desktop` - Avalonia desktop application

## Migration approach
The Flutter/Dart application is not translated mechanically. Its business concepts are mapped into C# domain entities, repositories and ViewModels. Existing SQLite schema/data should be migrated after validating the Dart schema and business rules.

## Run
```bash
dotnet restore
dotnet build
dotnet run --project src/Invoiso.Desktop
```
