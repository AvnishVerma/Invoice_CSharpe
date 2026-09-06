using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Invoice>().ToTable("invoices");
        modelBuilder.Entity<InvoiceItem>().ToTable("invoice_items");
        modelBuilder.Entity<Payment>().ToTable("invoice_payments");
        modelBuilder.Entity<CompanyInfo>().ToTable("company_info");
        modelBuilder.Entity<AppSetting>().ToTable("settings").HasKey(x => x.Key);

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
    }
}
