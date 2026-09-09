using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Infrastructure;

public static class DatabaseBackupRestore
{
    public static void Restore(byte[] bytes, string destinationPath)
    {
        if (bytes.Length == 0) throw new InvalidDataException("Database backup is empty.");
        var staging = Directory.CreateTempSubdirectory("ledgernest-restore-");
        try
        {
            var path = Path.Combine(staging.FullName, "candidate.db");
            File.WriteAllBytes(path, bytes);
            var sourceOptions = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, Mode = SqliteOpenMode.ReadWrite };
            using var source = new SqliteConnection(sourceOptions.ToString());
            source.Open();
            using (var command = source.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check";
                if (!string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
                    throw new InvalidDataException("Database integrity check failed.");
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('customers','products','invoices','invoice_items','invoice_payments','company_info','settings','users')";
                if (Convert.ToInt64(command.ExecuteScalar()) != 8)
                    throw new InvalidDataException("Backup is not a supported LedgerNest C# database.");
            }
            using (var candidate = new LedgerNestDbContext(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite(source).Options))
            {
                // Apply existing C# compatibility upgrades only to the isolated candidate.
                candidate.EnsureCurrentSchema();
                ValidateRows(candidate.Customers);
                ValidateRows(candidate.Products);
                ValidateRows(candidate.Invoices);
                ValidateRows(candidate.InvoiceItems);
                ValidateRows(candidate.Payments);
                ValidateRows(candidate.CompanyInfos);
                ValidateRows(candidate.Settings);
                ValidateRows(candidate.Users);
                if (candidate.Invoices.Any(i => i.CustomerId != null && !candidate.Customers.Any(c => c.Id == i.CustomerId))
                    || candidate.InvoiceItems.Any(i => i.ProductId != null && !candidate.Products.Any(p => p.Id == i.ProductId))
                    || candidate.InvoiceItems.Any(i => !candidate.Invoices.Any(invoice => invoice.Id == i.InvoiceId))
                    || candidate.Payments.Any(p => !candidate.Invoices.Any(invoice => invoice.Id == p.InvoiceId)))
                    throw new InvalidDataException("Backup contains broken record references.");
            }
            using (var command = source.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_key_check";
                using var reader = command.ExecuteReader();
                if (reader.Read()) throw new InvalidDataException("Backup contains broken record references.");
            }
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = destinationPath, Pooling = false, Mode = SqliteOpenMode.ReadWrite }.ToString());
            destination.Open();
            source.BackupDatabase(destination);
        }
        finally
        {
            staging.Delete(true);
        }
    }

    private static void ValidateRows<T>(DbSet<T> rows) where T : class
    {
        foreach (var row in rows.AsNoTracking().AsEnumerable()) { _ = row; }
    }
}
