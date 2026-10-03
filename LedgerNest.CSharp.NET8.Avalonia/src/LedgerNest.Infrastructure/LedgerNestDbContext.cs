using LedgerNest.Domain;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LedgerNest.Infrastructure;

// Performs the ledger nest db context action for this screen or workflow.
public sealed class LedgerNestDbContext(DbContextOptions<LedgerNestDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CompanyInfo> CompanyInfos => Set<CompanyInfo>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<BackupHistoryEntry> BackupHistory => Set<BackupHistoryEntry>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();
    public DbSet<ProductSellingUnit> ProductSellingUnits => Set<ProductSellingUnit>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<NumberSeries> NumberSeries => Set<NumberSeries>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InvoiceRefund> InvoiceRefunds => Set<InvoiceRefund>();
    public DbSet<InvoiceRefundLine> InvoiceRefundLines => Set<InvoiceRefundLine>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();

    // Upgrade only the C# schema; the Flutter database has different column names.
    public void EnsureCurrentSchema()
    {
        Database.EnsureCreated();
        if (!Database.IsSqlite()) return;
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
            NormalizeLegacyDuplicateDocumentNumbers();
            EnsureColumns("invoices", [("DeletedAt", "TEXT NULL"), ("CustomerName", "TEXT NOT NULL DEFAULT ''"), ("Snapshot", "TEXT NULL")]);
            EnsureColumns("invoices", [("CancellationReason", "TEXT NULL"), ("CancelledBy", "TEXT NULL"), ("CancelledAt", "TEXT NULL")]);
            EnsureColumns("customers", [("BusinessName", "TEXT NOT NULL DEFAULT ''"), ("CustomerCode", "TEXT NULL")]);
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS backup_history (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    Size INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    IsDatabase INTEGER NOT NULL
                )
                """;
            command.ExecuteNonQuery();
            command.CommandText = "CREATE TABLE IF NOT EXISTS document_sequences (Type TEXT PRIMARY KEY NOT NULL, NextValue INTEGER NOT NULL)";
            command.ExecuteNonQuery();
            command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_invoices_Type_InvoiceNumber ON invoices(Type, InvoiceNumber)";
            command.ExecuteNonQuery();
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
                ("ManufacturerName", "TEXT NOT NULL DEFAULT ''"),
                ("SupplierName", "TEXT NOT NULL DEFAULT ''"),
                ("Notes", "TEXT NOT NULL DEFAULT ''")]);
            EnsureColumns("products", [("ProductCode", "TEXT NULL"), ("Barcode", "TEXT NULL"), ("BaseUnitId", "INTEGER NULL")]);
            EnsureColumns("invoice_items", [("SellingUnitId", "INTEGER NULL"), ("SellingUnitCode", "TEXT NOT NULL DEFAULT ''"), ("UnitConversionFactor", "TEXT NOT NULL DEFAULT '1'"), ("BaseQuantity", "TEXT NOT NULL DEFAULT '0'")]);
            EnsureEnhancementTables();
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

            void NormalizeLegacyDuplicateDocumentNumbers()
            {
                // Earlier C# databases had no Type column. Adding it assigns all
                // rows to Invoice, which can expose colliding numbers that were
                // previously independent invoice, quotation, and receipt values.
                command.CommandText = "SELECT Id, Type, InvoiceNumber FROM invoices ORDER BY Id";
                var documents = new List<(int Id, string Type, string Number)>();
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) documents.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

                foreach (var group in documents.GroupBy(document => (document.Type, document.Number)))
                {
                    var duplicates = group.Skip(1).ToArray();
                    if (duplicates.Length == 0) continue;

                    var next = documents
                        .Where(document => document.Type == group.Key.Type)
                        .Select(document => new string(document.Number.Where(char.IsAsciiDigit).ToArray()))
                        .Select(number => long.TryParse(number, out var value) ? value : 0)
                        .DefaultIfEmpty(0)
                        .Max();

                    foreach (var duplicate in duplicates)
                    {
                        var replacement = (++next).ToString("D8");
                        command.CommandText = "UPDATE invoices SET InvoiceNumber = $number WHERE Id = $id";
                        command.Parameters.Clear();
                        var number = command.CreateParameter(); number.ParameterName = "$number"; number.Value = replacement;
                        var id = command.CreateParameter(); id.ParameterName = "$id"; id.Value = duplicate.Id;
                        command.Parameters.Add(number); command.Parameters.Add(id);
                        command.ExecuteNonQuery();
                    }
                }
                command.Parameters.Clear();
            }

            void EnsureEnhancementTables()
            {
                var statements = new[]
                {
                    "CREATE TABLE IF NOT EXISTS units (Id INTEGER PRIMARY KEY AUTOINCREMENT, Code TEXT NOT NULL, Name TEXT NOT NULL, Description TEXT NOT NULL DEFAULT '', IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, ModifiedBy TEXT NOT NULL DEFAULT '', ModifiedAt TEXT NULL)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_units_Code ON units(Code)",
                    "CREATE TABLE IF NOT EXISTS product_selling_units (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL, UnitId INTEGER NOT NULL, ConversionFactor TEXT NOT NULL DEFAULT '1', SellingPrice TEXT NOT NULL DEFAULT '0', IsDefault INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(ProductId) REFERENCES products(Id) ON DELETE RESTRICT, FOREIGN KEY(UnitId) REFERENCES units(Id) ON DELETE RESTRICT)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_product_selling_units_ProductId_UnitId ON product_selling_units(ProductId, UnitId)",
                    "CREATE TABLE IF NOT EXISTS product_prices (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL, SellingUnitId INTEGER NOT NULL, PriceList TEXT NOT NULL DEFAULT 'Default', SellingPrice TEXT NOT NULL DEFAULT '0', EffectiveDate TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, ModifiedBy TEXT NOT NULL DEFAULT '', ModifiedAt TEXT NULL)",
                    "CREATE INDEX IF NOT EXISTS IX_product_prices_lookup ON product_prices(ProductId, SellingUnitId, PriceList, IsActive, EffectiveDate)",
                    "CREATE TABLE IF NOT EXISTS number_series (EntityType TEXT PRIMARY KEY NOT NULL, Prefix TEXT NOT NULL DEFAULT '', NextValue INTEGER NOT NULL DEFAULT 1, Increment INTEGER NOT NULL DEFAULT 1, NumberLength INTEGER NOT NULL DEFAULT 6)",
                    "CREATE TABLE IF NOT EXISTS inventory_transactions (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL, TransactionDate TEXT NOT NULL, TransactionType TEXT NOT NULL, BaseQuantityChange TEXT NOT NULL, SourceType TEXT NOT NULL DEFAULT '', SourceId INTEGER NULL, Reference TEXT NOT NULL DEFAULT '', Notes TEXT NOT NULL DEFAULT '', CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, FOREIGN KEY(ProductId) REFERENCES products(Id) ON DELETE RESTRICT)",
                    "CREATE INDEX IF NOT EXISTS IX_inventory_transactions_ProductId_TransactionDate ON inventory_transactions(ProductId, TransactionDate)",
                    "CREATE TABLE IF NOT EXISTS invoice_refunds (Id INTEGER PRIMARY KEY AUTOINCREMENT, InvoiceId INTEGER NOT NULL, RefundNumber TEXT NOT NULL, RefundDate TEXT NOT NULL, Reason TEXT NOT NULL DEFAULT '', SubTotal TEXT NOT NULL, TaxTotal TEXT NOT NULL, GrandTotal TEXT NOT NULL, CreatedBy TEXT NOT NULL DEFAULT '', CreatedAt TEXT NOT NULL, FOREIGN KEY(InvoiceId) REFERENCES invoices(Id) ON DELETE RESTRICT)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_invoice_refunds_RefundNumber ON invoice_refunds(RefundNumber)",
                    "CREATE TABLE IF NOT EXISTS invoice_refund_lines (Id INTEGER PRIMARY KEY AUTOINCREMENT, InvoiceRefundId INTEGER NOT NULL, InvoiceItemId INTEGER NOT NULL, Quantity TEXT NOT NULL, UnitPrice TEXT NOT NULL, TaxRate TEXT NOT NULL, LineTotal TEXT NOT NULL, FOREIGN KEY(InvoiceRefundId) REFERENCES invoice_refunds(Id) ON DELETE CASCADE, FOREIGN KEY(InvoiceItemId) REFERENCES invoice_items(Id) ON DELETE RESTRICT)",
                    "CREATE TABLE IF NOT EXISTS role_permissions (Id INTEGER PRIMARY KEY AUTOINCREMENT, Role TEXT NOT NULL, Resource TEXT NOT NULL, Action TEXT NOT NULL, IsAllowed INTEGER NOT NULL DEFAULT 0)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_role_permissions_Role_Resource_Action ON role_permissions(Role, Resource, Action)",
                    "CREATE TABLE IF NOT EXISTS roles (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, IsSystem INTEGER NOT NULL DEFAULT 0)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_roles_Name ON roles(Name)",
                    "CREATE TABLE IF NOT EXISTS user_roles (UserId INTEGER NOT NULL, RoleId INTEGER NOT NULL, PRIMARY KEY(UserId, RoleId), FOREIGN KEY(UserId) REFERENCES users(Id) ON DELETE CASCADE, FOREIGN KEY(RoleId) REFERENCES roles(Id) ON DELETE CASCADE)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_products_ProductCode ON products(ProductCode) WHERE ProductCode IS NOT NULL AND ProductCode <> ''",
                    "CREATE INDEX IF NOT EXISTS IX_products_Search ON products(Name, HsnCode, Code, Barcode)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_customers_CustomerCode ON customers(CustomerCode) WHERE CustomerCode IS NOT NULL AND CustomerCode <> ''"
                };
                foreach (var statement in statements) { command.CommandText = statement; command.ExecuteNonQuery(); }
            }
            transaction.Commit();
        }
        finally
        {
            Database.CloseConnection();
        }
    }

    // Performs the on model creating action for this screen or workflow.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().ToTable("customers").HasIndex(x => x.CustomerCode).IsUnique();
        modelBuilder.Entity<Product>().ToTable("products").HasIndex(x => x.ProductCode).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => new { x.Name, x.HsnCode, x.Code, x.Barcode });
        modelBuilder.Entity<Invoice>().ToTable("invoices").HasIndex(i => new { i.Type, i.InvoiceNumber }).IsUnique();
        modelBuilder.Entity<Invoice>().Property(i => i.Snapshot).HasConversion(
            snapshot => JsonSerializer.Serialize(snapshot, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<InvoiceSnapshot>(json, (JsonSerializerOptions?)null));
        modelBuilder.Entity<InvoiceItem>().ToTable("invoice_items");
        modelBuilder.Entity<Payment>().ToTable("invoice_payments");
        modelBuilder.Entity<CompanyInfo>().ToTable("company_info");
        modelBuilder.Entity<AppSetting>().ToTable("settings").HasKey(x => x.Key);
        modelBuilder.Entity<BackupHistoryEntry>().ToTable("backup_history");
        modelBuilder.Entity<DocumentSequence>().ToTable("document_sequences").HasKey(x => x.Type);
        modelBuilder.Entity<UnitOfMeasure>().ToTable("units").HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<ProductSellingUnit>().ToTable("product_selling_units").HasIndex(x => new { x.ProductId, x.UnitId }).IsUnique();
        modelBuilder.Entity<ProductPrice>().ToTable("product_prices").HasIndex(x => new { x.ProductId, x.SellingUnitId, x.PriceList, x.IsActive, x.EffectiveDate });
        modelBuilder.Entity<NumberSeries>().ToTable("number_series").HasKey(x => x.EntityType);
        modelBuilder.Entity<NumberSeries>().Property(x => x.NextValue).IsConcurrencyToken();
        modelBuilder.Entity<InventoryTransaction>().ToTable("inventory_transactions").HasIndex(x => new { x.ProductId, x.TransactionDate });
        modelBuilder.Entity<InvoiceRefund>().ToTable("invoice_refunds").HasIndex(x => x.RefundNumber).IsUnique();
        modelBuilder.Entity<InvoiceRefundLine>().ToTable("invoice_refund_lines");
        modelBuilder.Entity<RolePermission>().ToTable("role_permissions").HasIndex(x => new { x.Role, x.Resource, x.Action }).IsUnique();
        modelBuilder.Entity<AppRole>().ToTable("roles").HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<AppUserRole>().ToTable("user_roles").HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<AppUserRole>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AppUserRole>().HasOne<AppRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        // An increment is guarded by the previously-read value, so simultaneous
        // desktop clients retry instead of reserving the same document number.
        modelBuilder.Entity<DocumentSequence>().Property(x => x.NextValue).IsConcurrencyToken();
        modelBuilder.Entity<AppUser>().ToTable("users").HasIndex(x => x.Username).IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProductSellingUnit>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductSellingUnit>().HasOne<UnitOfMeasure>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductPrice>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductPrice>().HasOne<ProductSellingUnit>().WithMany().HasForeignKey(x => x.SellingUnitId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryTransaction>().HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceRefund>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceRefundId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InvoiceRefund>().HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceRefundLine>().HasOne<InvoiceItem>().WithMany().HasForeignKey(x => x.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);

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
        modelBuilder.Entity<InvoiceItem>().Property(x => x.UnitConversionFactor).HasPrecision(18, 6);
        modelBuilder.Entity<InvoiceItem>().Property(x => x.BaseQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<ProductSellingUnit>().Property(x => x.ConversionFactor).HasPrecision(18, 6);
        modelBuilder.Entity<ProductSellingUnit>().Property(x => x.SellingPrice).HasPrecision(18, 2);
        modelBuilder.Entity<ProductPrice>().Property(x => x.SellingPrice).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryTransaction>().Property(x => x.BaseQuantityChange).HasPrecision(18, 3);
        modelBuilder.Entity<InvoiceRefund>().Property(x => x.SubTotal).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceRefund>().Property(x => x.TaxTotal).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceRefund>().Property(x => x.GrandTotal).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceRefundLine>().Property(x => x.Quantity).HasPrecision(18, 3);
        modelBuilder.Entity<InvoiceRefundLine>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<InvoiceRefundLine>().Property(x => x.TaxRate).HasPrecision(8, 2);
        modelBuilder.Entity<InvoiceRefundLine>().Property(x => x.LineTotal).HasPrecision(18, 2);
    }
}
