using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Invoices;

[Trait("Category", "Integration")]
public sealed class InvoiceVoidParityTests
{
    private static int SeedSale(TestDatabaseFixture fixture, decimal paid = 0, bool paymentRow = false)
    {
        fixture.CreateModel();
        using var db = fixture.CreateDbContext();
        var product = new Product { Name = "Fractional stock", StockQuantity = 7.5m };
        var invoice = new Invoice { InvoiceNumber = "INV-VOID", GrandTotal = 100, PaidAmount = paid, Status = "Unpaid" };
        db.Products.Add(product);
        db.Invoices.Add(invoice);
        db.SaveChanges();
        db.InventoryTransactions.Add(new InventoryTransaction { ProductId = product.Id, SourceType = "Invoice", Reference = invoice.InvoiceNumber, BaseQuantityChange = -2.5m });
        if (paymentRow) db.Payments.Add(new Payment { InvoiceId = invoice.Id, Amount = 0 });
        db.SaveChanges();
        return invoice.Id;
    }

    [Fact]
    public async Task Void_RestoresRecordedFractionalStockExactlyOnce_AndRetainsAudit()
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture);
        var service = new InvoiceLifecycleService(fixture);
        await service.VoidAsync(id, "admin", "Order withdrawn");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VoidAsync(id, "admin", "Again"));
        using var db = fixture.CreateDbContext();
        Assert.Equal(10m, db.Products.Single().StockQuantity);
        Assert.Equal(2.5m, db.InventoryTransactions.Single(item => item.SourceType == "InvoiceVoid").BaseQuantityChange);
        Assert.Equal(2, db.InventoryTransactions.Count());
        var invoice = db.Invoices.Single();
        Assert.Equal("Voided", invoice.Status);
        Assert.Equal("Order withdrawn", invoice.CancellationReason);
        Assert.Equal("admin", invoice.CancelledBy);
        Assert.NotNull(invoice.CancelledAt);
        Assert.Equal(0, invoice.BalanceAmount);
        Assert.Empty(db.Invoices.Where(InvoiceStatusRules.FinancialInvoices));
        var reloaded = fixture.CreateModel();
        Assert.Empty(reloaded.ActiveInvoices);
        Assert.Equal(0, reloaded.BuildRevenueReport().InvoiceCount);
        Assert.Equal(0, reloaded.BuildRevenueReport().Outstanding);
        Assert.Contains("Voided", reloaded.ExportCsv("Invoice"));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(0, true)]
    public async Task Void_RejectsPaymentAggregateOrAnyPaymentHistory(decimal paid, bool row)
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture, paid, row);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new InvoiceLifecycleService(fixture).VoidAsync(id, "admin", "Void"));
        using var db = fixture.CreateDbContext();
        Assert.Equal("Unpaid", db.Invoices.Single().Status);
        Assert.Equal(7.5m, db.Products.Single().StockQuantity);
        Assert.Single(db.InventoryTransactions);
    }

    [Fact]
    public async Task Void_DatabaseFailureRollsBackStatusStockAndReversal()
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture);
        using (var db = fixture.CreateDbContext())
            db.Database.ExecuteSqlRaw("CREATE TRIGGER reject_void BEFORE UPDATE OF Status ON invoices WHEN NEW.Status = 'Voided' BEGIN SELECT RAISE(ABORT, 'Injected void failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => new InvoiceLifecycleService(fixture).VoidAsync(id, "admin", "Void"));
        using var verify = fixture.CreateDbContext();
        Assert.Equal("Unpaid", verify.Invoices.Single().Status);
        Assert.Equal(7.5m, verify.Products.Single().StockQuantity);
        Assert.Single(verify.InventoryTransactions);
    }

    [Theory]
    [InlineData("View")]
    [InlineData("Update")]
    public async Task Void_ServiceRequiresBothViewAndUpdate(string action)
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture);
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Restricted");
        TestData.CreateUser(model, "restricted", "Restricted");
        TestData.SetPermissions(fixture, "Restricted", "Invoice", action);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new InvoiceLifecycleService(fixture).VoidAsync(id, "restricted", "Void"));
        using var db = fixture.CreateDbContext();
        Assert.Equal("Unpaid", db.Invoices.Single().Status);
    }

    [Fact]
    public async Task Void_PaymentAttemptCannotReactivateDocument()
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture);
        var model = fixture.CreateModel();
        var staleRecord = model.Invoices.Single();
        await new InvoiceLifecycleService(fixture).VoidAsync(id, "admin", "Void");
        var fields = FormCatalog.Payment();
        fields[0].Value = "10";
        Assert.False(model.ApplyPayment(staleRecord, fields));
        using var db = fixture.CreateDbContext();
        Assert.Empty(db.Payments);
        Assert.Equal("Voided", db.Invoices.Single().Status);
    }

    [Fact]
    public async Task Void_LegacyDocumentWithoutMovementsDoesNotInventStock()
    {
        using var fixture = new TestDatabaseFixture();
        var id = SeedSale(fixture);
        using (var db = fixture.CreateDbContext())
        {
            db.InventoryTransactions.RemoveRange(db.InventoryTransactions);
            db.SaveChanges();
        }
        await new InvoiceLifecycleService(fixture).VoidAsync(id, "admin", "Void");
        using var verify = fixture.CreateDbContext();
        Assert.Equal(7.5m, verify.Products.Single().StockQuantity);
        Assert.Empty(verify.InventoryTransactions);
    }
}

[Trait("Category", "Unit")]
public sealed class InvoicePaymentOutputTests
{
    [Fact]
    public void VoidedPaymentHistory_IsReadOnlyEvenWhenBalanceOrButtonStateIsStale()
    {
        Assert.False(new LedgerNest.Desktop.Views.PaymentDialogModel { IsVoided = true }.CanRecordPayment);
        Assert.False(new LedgerNest.Desktop.Views.PaymentDialogModel { HasUpdatePermission = false }.CanRecordPayment);
    }
    [Theory]
    [InlineData("Classic")]
    [InlineData("Modern")]
    [InlineData("Minimal")]
    [InlineData("Executive")]
    [InlineData("Grid Classic")]
    [InlineData("Thermal")]
    public void Pdf_VoidedStatusRemainsVisibleButPaymentQrIsHidden(string template)
    {
        var options = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true)
        { ShowPaymentQr = true, UpiAccounts = [new("PAYMENT-QR-MARKER", "billing@upi")] };
        var business = new DocumentPdf.Business("Business", "", "", "", "", "", "");
        var invoice = new Invoice { InvoiceNumber = "INV-1", Status = "Unpaid", GrandTotal = 100 };
        string PdfText(byte[] bytes)
        {
            using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(1));
            return string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index =>
            { using var page = reader.GetPageReader(index); return page.GetText(); }));
        }
        var payable = PdfText(DocumentPdf.Create(invoice, [], business, "A4", false, template, options: options));
        Assert.Contains("PAYMENT-QR-MARKER", payable);
        invoice.Status = "Voided";
        var voided = PdfText(DocumentPdf.Create(invoice, [], business, "A4", false, template, options: options));
        Assert.Contains("Voided", voided);
        Assert.DoesNotContain("PAYMENT-QR-MARKER", voided);
    }

    [Theory]
    [InlineData("Unpaid", "Invoice", 100, 0, 100, true)]
    [InlineData("Partial", "Invoice", 100, 30, 70, true)]
    [InlineData("Paid", "Invoice", 100, 100, 0, false)]
    [InlineData("Paid", "Invoice", 100, 99.996, 0, false)]
    [InlineData("Voided", "Invoice", 100, 0, 0, false)]
    [InlineData("DECLINED", "Invoice", 100, 0, 0, false)]
    [InlineData("Open", "Quotation", 100, 0, 100, false)]
    public void PaymentOutput_UsesOutstandingAndDocumentEligibility(string status, string type, decimal total, decimal paid, decimal outstanding, bool qr)
    {
        var invoice = new Invoice { Status = status, Type = type, GrandTotal = total, PaidAmount = paid };
        Assert.Equal(outstanding, invoice.BalanceAmount);
        Assert.Equal(qr, InvoiceStatusRules.CanRequestPayment(invoice));
        var uri = DocumentPdf.UpiPaymentUri(new("Pay", "billing@upi"), new("Business", "", "", "", "", "", ""), invoice, "INV-1");
        Assert.Contains("&am=" + outstanding.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "&", uri);
    }
}
