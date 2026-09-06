using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LedgerNest.Infrastructure;

public sealed class LedgerNestDbContext(DbContextOptions<LedgerNestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<AppUser> Users => Set<AppUser>();

    // Upgrade only the C# schema; the Flutter database has different column names.
    public void EnsureCurrentSchema()
    {
        Database.EnsureCreated();
        Database.OpenConnection();
        try
        {
            using var transaction = Database.BeginTransaction();
            using var command = Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "PRAGMA table_info(invoices)";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader())
                while (reader.Read()) columns.Add(reader.GetString(1));
            if (!columns.Contains("InvoiceNumber"))
                throw new InvalidOperationException("This database is not a LedgerNest C# database.");
            if (!columns.Contains("Type"))
                Database.ExecuteSqlRaw("ALTER TABLE invoices ADD COLUMN Type TEXT NOT NULL DEFAULT 'Invoice'");
            EnsureColumns("customers", [("BusinessName", "TEXT NOT NULL DEFAULT ''")]);
            EnsureColumns("products", [
                ("Type", "TEXT NOT NULL DEFAULT 'Product'"),
                ("AliasName", "TEXT NOT NULL DEFAULT ''"),
                ("DefaultDiscount", "TEXT NOT NULL DEFAULT '0'"),
                ("PriceIncludesTax", "INTEGER NOT NULL DEFAULT 0"),
                ("UnlimitedStock", "INTEGER NOT NULL DEFAULT 0"),
                ("Unit", "TEXT NOT NULL DEFAULT 'None'"),
                ("CustomUnit", "TEXT NOT NULL DEFAULT ''"),
                ("StorageLocation", "TEXT NOT NULL DEFAULT ''"),
                ("ContainerNumber", "TEXT NOT NULL DEFAULT ''"),
                ("BatchNumber", "TEXT NOT NULL DEFAULT ''"),
                ("ExpiryDate", "TEXT NOT NULL DEFAULT ''"),
                ("ManufactureDate", "TEXT NOT NULL DEFAULT ''"),
                ("SupplierName", "TEXT NOT NULL DEFAULT ''"),
                ("Notes", "TEXT NOT NULL DEFAULT ''")]);
            void EnsureColumns(string table, (string Name, string Definition)[] additions)
            {
                // Identifiers and definitions come exclusively from the schema constants above.
                command.CommandText = $"PRAGMA table_info({table})";
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) existing.Add(reader.GetString(1));
                foreach (var column in additions)
                    if (!existing.Contains(column.Name))
                    {
                        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column.Name} {column.Definition}";
                        command.ExecuteNonQuery();
                    }
            }
            transaction.Commit();
        }
        finally
        {
            Database.CloseConnection();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Invoice>().ToTable("invoices");
        modelBuilder.Entity<InvoiceItem>().ToTable("invoice_items");
        modelBuilder.Entity<Payment>().ToTable("invoice_payments");
        modelBuilder.Entity<CompanyInfo>().ToTable("company_info");
        modelBuilder.Entity<AppSetting>().ToTable("settings").HasKey(x => x.Key);
        modelBuilder.Entity<AppUser>().ToTable("users").HasIndex(x => x.Username).IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.SubTotal).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>()
            .Property(x => x.TaxTotal).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>()
            .Property(x => x.DiscountTotal).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>()
            .Property(x => x.GrandTotal).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>()
            .Property(x => x.PaidAmount).HasPrecision(18, 2);

        modelBuilder.Entity<Product>().Property(x => x.SalePrice).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.PurchasePrice).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.TaxRate).HasPrecision(8, 2);
        modelBuilder.Entity<Product>().Property(x => x.StockQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.ProductPrice).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.PurchasePrice).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.TaxRate).HasPrecision(8, 2);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.Discount).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.ExtraCost).HasPrecision(18, 2);
    }
}
