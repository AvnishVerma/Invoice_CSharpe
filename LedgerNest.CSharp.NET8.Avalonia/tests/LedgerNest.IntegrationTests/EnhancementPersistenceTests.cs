using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.IntegrationTests;

public sealed class EnhancementPersistenceTests
{
    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(path); }
        catch (IOException) { }
    }
    private static (string Path, IDbContextFactory<LedgerNestDbContext> Factory) SqliteFactory()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ledgernest-enhancement-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={path}").Options;
        return (path, new TestFactory(options));
    }

    [Fact]
    public async Task NumberSeriesIsUniqueUnderConcurrency()
    {
        var fixture = SqliteFactory();
        try
        {
            await using (var db = await fixture.Factory.CreateDbContextAsync()) db.EnsureCurrentSchema();
            var service = new NumberSeriesService(fixture.Factory);
            var numbers = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => service.ReserveAsync("Product", "PRD", 1, 1, 6)));
            Assert.Equal(30, numbers.Distinct().Count());
            Assert.Contains("PRD000001", numbers);
            Assert.Contains("PRD000030", numbers);
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    [Fact]
    public async Task ReferencedUnitCannotBeDeletedAndDuplicateSellingUnitIsRejected()
    {
        var fixture = SqliteFactory();
        try
        {
            await using var db = await fixture.Factory.CreateDbContextAsync();
            db.EnsureCurrentSchema();
            var unit = new UnitOfMeasure { Code = "PCS", Name = "Piece", CreatedBy = "test" };
            var product = new Product { Name = "Product A", ProductCode = "PRD1", BaseUnitId = null };
            db.AddRange(unit, product); await db.SaveChangesAsync();
            product.BaseUnitId = unit.Id;
            db.ProductSellingUnits.Add(new ProductSellingUnit { ProductId = product.Id, UnitId = unit.Id, ConversionFactor = 1, SellingPrice = 10, IsDefault = true });
            await db.SaveChangesAsync();
            var units = new UnitMasterService(fixture.Factory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => units.DeleteAsync(unit.Id));
            var pricing = new ProductPricingService(fixture.Factory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => pricing.SaveSellingUnitAsync(new ProductSellingUnit { ProductId = product.Id, UnitId = unit.Id, ConversionFactor = 2, SellingPrice = 20 }));
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    [Fact]
    public async Task InventoryPostUpdatesStockAndLedgerAtomically()
    {
        var fixture = SqliteFactory();
        try
        {
            await using (var db = await fixture.Factory.CreateDbContextAsync())
            {
                db.EnsureCurrentSchema(); db.Products.Add(new Product { Name = "Stock", ProductCode = "PRD1", StockQuantity = 100 }); await db.SaveChangesAsync();
            }
            await using var lookup = await fixture.Factory.CreateDbContextAsync();
            var productId = await lookup.Products.Select(item => item.Id).SingleAsync();
            var inventory = new InventoryService(fixture.Factory);
            foreach (var change in new[] { 50m, -20m, 5m, -2m }) await inventory.PostAsync(new InventoryTransaction { ProductId = productId, BaseQuantityChange = change, TransactionType = "Test" });
            lookup.ChangeTracker.Clear();
            Assert.Equal(133m, (await lookup.Products.FindAsync(productId))!.StockQuantity);
            Assert.Equal(4, await lookup.InventoryTransactions.CountAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => inventory.PostAsync(new InventoryTransaction { ProductId = productId, BaseQuantityChange = -200, TransactionType = "Test" }));
            lookup.ChangeTracker.Clear();
            Assert.Equal(133m, (await lookup.Products.FindAsync(productId))!.StockQuantity);
            Assert.Equal(4, await lookup.InventoryTransactions.CountAsync());
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    [Fact]
    public async Task EffectivePriceUsesLatestEligibleEntryWithoutChangingHistory()
    {
        var fixture = SqliteFactory();
        try
        {
            int productId; int unitId;
            await using (var db = await fixture.Factory.CreateDbContextAsync())
            {
                db.EnsureCurrentSchema();
                var unit = new UnitOfMeasure { Code = "BOX", Name = "Box" };
                var product = new Product { Name = "Product A", ProductCode = "PRD1", SalePrice = 75 };
                db.AddRange(unit, product); await db.SaveChangesAsync();
                var sellingUnit = new ProductSellingUnit { ProductId = product.Id, UnitId = unit.Id, ConversionFactor = 10, SellingPrice = 95, IsDefault = true, IsActive = true };
                db.ProductSellingUnits.Add(sellingUnit); await db.SaveChangesAsync();
                productId = product.Id; unitId = sellingUnit.Id;
            }
            var pricing = new ProductPricingService(fixture.Factory);
            Assert.Equal(95m, await pricing.GetPriceAsync(productId, unitId, DateTime.UtcNow));
            await using (var db = await fixture.Factory.CreateDbContextAsync())
            {
                var sellingUnit = await db.ProductSellingUnits.FindAsync(unitId);
                sellingUnit!.SellingPrice = 0;
                await db.SaveChangesAsync();
            }
            Assert.Equal(75m, await pricing.GetPriceAsync(productId, unitId, DateTime.UtcNow));
            await pricing.SavePriceAsync(new ProductPrice { ProductId = productId, SellingUnitId = unitId, SellingPrice = 90, EffectiveDate = DateTime.UtcNow.AddDays(-2) }, "admin");
            await pricing.SavePriceAsync(new ProductPrice { ProductId = productId, SellingUnitId = unitId, SellingPrice = 88, EffectiveDate = DateTime.UtcNow.AddDays(1) }, "admin");
            Assert.Equal(90m, await pricing.GetPriceAsync(productId, unitId, DateTime.UtcNow));
            Assert.Equal(88m, await pricing.GetPriceAsync(productId, unitId, DateTime.UtcNow.AddDays(2)));
            await using var verify = await fixture.Factory.CreateDbContextAsync();
            Assert.Equal(2, await verify.ProductPrices.CountAsync());
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    [Fact]
    public async Task QuotationCancellationPreservesReasonAndAuditIdentity()
    {
        var fixture = SqliteFactory();
        try
        {
            int quotationId;
            await using (var db = await fixture.Factory.CreateDbContextAsync())
            {
                db.EnsureCurrentSchema();
                var quotation = new Invoice { InvoiceNumber = "QUO-1", Type = "Quotation", Status = "Open" };
                db.Invoices.Add(quotation); await db.SaveChangesAsync(); quotationId = quotation.Id;
            }
            var service = new QuotationService(fixture.Factory);
            await service.CancelAsync(quotationId, "Customer changed scope", "admin");
            await using var verify = await fixture.Factory.CreateDbContextAsync();
            var saved = await verify.Invoices.SingleAsync();
            Assert.Equal("Cancelled", saved.Status);
            Assert.Equal("Customer changed scope", saved.CancellationReason);
            Assert.Equal("admin", saved.CancelledBy);
            Assert.NotNull(saved.CancelledAt);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(quotationId, "Again", "admin"));
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    [Fact]
    public async Task PartialRefundRestoresBaseStockAndRejectsOverRefund()
    {
        var fixture = SqliteFactory();
        try
        {
            int invoiceId; int itemId;
            await using (var db = await fixture.Factory.CreateDbContextAsync())
            {
                db.EnsureCurrentSchema();
                var product = new Product { ProductCode = "PRD-R", Name = "Refundable", Type = "Product", StockQuantity = 4 };
                var invoice = new Invoice { InvoiceNumber = "INV-R", Type = "Invoice", Status = "Paid" };
                db.AddRange(product, invoice); await db.SaveChangesAsync();
                var item = new InvoiceItem { InvoiceId = invoice.Id, ProductId = product.Id, Description = "Box", Quantity = 2, UnitPrice = 100, TaxRate = 18, UnitConversionFactor = 10, BaseQuantity = 20 };
                db.InvoiceItems.Add(item); await db.SaveChangesAsync(); invoiceId = invoice.Id; itemId = item.Id;
            }
            var service = new InvoiceRefundService(fixture.Factory, new NumberSeriesService(fixture.Factory));
            var refund = await service.RefundAsync(invoiceId, [new RefundLineRequest(itemId, 1)], "Returned box", "admin");
            Assert.Equal(118m, refund.GrandTotal);
            await using var verify = await fixture.Factory.CreateDbContextAsync();
            Assert.Equal(14m, (await verify.Products.SingleAsync()).StockQuantity);
            Assert.Equal(10m, (await verify.InventoryTransactions.SingleAsync()).BaseQuantityChange);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefundAsync(invoiceId, [new RefundLineRequest(itemId, 2)], "Too many", "admin"));
        }
        finally { DeleteDatabase(fixture.Path); }
    }

    private sealed class TestFactory(DbContextOptions<LedgerNestDbContext> options) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(options);
    }
}
