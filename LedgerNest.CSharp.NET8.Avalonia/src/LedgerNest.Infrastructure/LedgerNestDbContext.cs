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
