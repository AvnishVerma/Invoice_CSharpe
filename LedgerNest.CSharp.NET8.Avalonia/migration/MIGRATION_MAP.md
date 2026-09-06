# Flutter → C# migration map

The uploaded Flutter project contains a large number of Dart files. The conversion should preserve business behavior while changing the application architecture.

| Flutter/Dart concept | C# target |
|---|---|
| Flutter widgets/screens | Avalonia AXAML + Views |
| Riverpod providers | MVVM ViewModels + DI services |
| Repository classes | C# repository interfaces/implementations |
| SQLite helper/database | EF Core SQLite DbContext |
| Dart models | C# domain entities/DTOs |
| PDF generation | .NET PDF library/service |
| CSV export | C# CSV service |
| SharedPreferences/settings | JSON/SQLite settings service |
| File backup/restore | .NET file service |
| UPI QR | QR generation service |
| Printer integration | .NET printing abstraction |
| Theme/preferences | Avalonia resources/settings |
| App navigation | Avalonia navigation/view routing |

## Suggested implementation order

1. Database/schema compatibility
2. Domain models
3. Customers
4. Products/inventory
5. Invoice creation/editing
6. Payments
7. Quotations
8. Dashboard/reports
9. PDF/print
10. Backup/restore
11. Authentication/users/roles
12. Settings/localization

The generated scaffold is intentionally conservative: it establishes the .NET/Avalonia/SQLite foundation without pretending that every Flutter-specific behavior has already been converted.
