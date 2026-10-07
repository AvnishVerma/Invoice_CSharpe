using System.Globalization;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Invoices;

public sealed class InvoiceEditParityTests
{
    [Theory]
    [InlineData("29.996", true)]
    [InlineData("29.99", false)]
    [InlineData("30", true)]
    [InlineData("100", true)]
    [Trait("Category", "Unit")]
    public void PaidTotalRule_UsesDecimalTolerance(string total, bool allowed)
    {
        var invoice = new Invoice { PaidAmount = 30, Status = "Partial" };
        var amount = decimal.Parse(total, CultureInfo.InvariantCulture);
        if (allowed) InvoiceEditRules.Validate(invoice, amount, 30);
        else Assert.Throws<InvalidOperationException>(() => InvoiceEditRules.Validate(invoice, amount, 30));
    }

    [Theory]
    [InlineData("Voided")]
    [InlineData("Declined")]
    [InlineData("Cancelled")]
    [InlineData("Converted")]
    [Trait("Category", "Unit")]
    public void EditRule_RejectsUnavailableStatuses(string status) => Assert.Throws<InvalidOperationException>(() => InvoiceEditRules.Validate(new Invoice { Status = status }, 100, 0));

    [Theory]
    [InlineData("2026-02-30", "14:30")]
    [InlineData("2026-10-05", "24:00")]
    [InlineData("2026-10-05", "-01:00")]
    [InlineData("2026-10-05", "")]
    [InlineData("invalid", "14:30")]
    [Trait("Category", "Unit")]
    public void DateRule_RejectsInvalidInput(string date, string time) => Assert.Throws<InvalidOperationException>(() => InvoiceEditRules.ResolveDate(date, time));

    [Fact]
    [Trait("Category", "Unit")]
    public void DateRule_PreservesFractionalSeconds_AndPaymentRuleRejectsInconsistentHistory()
    {
        Assert.Equal(new DateTime(2026, 10, 5, 14, 30, 45, DateTimeKind.Utc).AddTicks(1234567), InvoiceEditRules.ResolveDate("2026-10-05", "14:30:45.1234567", DateTimeKind.Utc));
        Assert.Throws<InvalidOperationException>(() => InvoiceEditRules.Validate(new Invoice { PaidAmount = 10 }, 100, 20));
        Assert.Equal("Paid", InvoiceEditRules.PaymentStatus(29.996m, 30));
    }

    private static UiRecord CreatePaidInvoice(TestDatabaseFixture fixture)
    {
        var model = fixture.CreateModel();
        model.StartDocument("Invoice"); model.InvoiceOptions[3].Value = "No Tax";
        model.InvoiceDetails[1].Value = "2026-09-23"; model.OrderTime.Value = "16:24:31.1234567";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100, Quantity = 1 });
        Assert.True(model.SaveInvoice(), model.Status);
        var payment = FormCatalog.Payment(); payment[0].Value = "30";
        Assert.True(model.ApplyPayment(model.Invoices.Single(), payment), model.Status);
        return model.Invoices.Single();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PaidEdit_PreservesPaymentsTimeIdentityNumberAndRepeatedSaves()
    {
        using var fixture = new TestDatabaseFixture();
        var original = CreatePaidInvoice(fixture);
        var model = fixture.CreateModel();
        Assert.True(model.LoadDocumentForEditing(model.Invoices.Single()));
        Assert.False(model.CanVoidCurrentInvoice);
        model.InvoiceDetails[1].Value = "2026-10-05"; model.Lines[0].Price = 60;
        Assert.True(model.SaveInvoice(), model.Status);
        Assert.True(model.SaveInvoice(), model.Status);
        using var db = fixture.CreateDbContext();
        var invoice = Assert.Single(db.Invoices);
        Assert.Equal(original.SourceId, invoice.Id); Assert.Equal(original.Name, invoice.InvoiceNumber);
        Assert.Equal(new DateTime(2026, 10, 5, 16, 24, 31).AddTicks(1234567), invoice.InvoiceDate);
        Assert.Equal(30m, invoice.PaidAmount); Assert.Equal(60m, invoice.GrandTotal); Assert.Equal("Partial", invoice.Status);
        Assert.Equal(30m, Assert.Single(db.Payments).Amount);
        Assert.Equal("30.00", model.Invoices.Single()["Paid"]); Assert.Equal("30.00", model.Invoices.Single()["Outstanding"]);
        Assert.Equal("00000002", model.PeekNextDocumentNumber("Invoice"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PaidEdit_RejectsBelowPaidWithoutChangingItemsOrPayments()
    {
        using var fixture = new TestDatabaseFixture(); CreatePaidInvoice(fixture);
        var model = fixture.CreateModel(); Assert.True(model.LoadDocumentForEditing(model.Invoices.Single()));
        model.Lines[0].Price = 29.99m;
        Assert.False(model.SaveInvoice()); Assert.Contains("already paid", model.Status);
        using var db = fixture.CreateDbContext();
        Assert.Equal(100m, db.Invoices.Single().GrandTotal); Assert.Equal(100m, db.InvoiceItems.Single().UnitPrice); Assert.Equal(30m, db.Payments.Single().Amount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PaidEdit_RejectsPaymentArrivingAfterEditorLoad()
    {
        using var fixture = new TestDatabaseFixture(); CreatePaidInvoice(fixture);
        var editor = fixture.CreateModel(); Assert.True(editor.LoadDocumentForEditing(editor.Invoices.Single()));
        var payer = fixture.CreateModel(); var payment = FormCatalog.Payment(); payment[0].Value = "10";
        Assert.True(payer.ApplyPayment(payer.Invoices.Single(), payment));
        editor.Lines[0].Price = 90;
        Assert.False(editor.SaveInvoice()); Assert.Contains("changed", editor.Status);
        using var db = fixture.CreateDbContext(); Assert.Equal(40m, db.Invoices.Single().PaidAmount); Assert.Equal(100m, db.Invoices.Single().GrandTotal); Assert.Equal(2, db.Payments.Count());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Persistence_RejectsConcurrentFinancialChangesAtSaveBoundary()
    {
        using var fixture = new TestDatabaseFixture(); CreatePaidInvoice(fixture);
        using var stale = fixture.CreateDbContext(); var old = stale.Invoices.Single();
        using (var current = fixture.CreateDbContext())
        { current.Invoices.Single().PaidAmount = 40; current.SaveChanges(); }
        old.GrandTotal = 90;
        Assert.Throws<DbUpdateConcurrencyException>(() => stale.SaveChanges());
        using var intact = fixture.CreateDbContext(); Assert.Equal(100m, intact.Invoices.Single().GrandTotal); Assert.Equal(40m, intact.Invoices.Single().PaidAmount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SaveFailure_RollsBackInvoiceItemsStockAndNumberAllocation()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) db.Database.ExecuteSqlRaw("CREATE TRIGGER fail_invoice BEFORE INSERT ON invoices BEGIN SELECT RAISE(ABORT, 'Injected invoice failure'); END;");
        model.Lines.Add(new InvoiceLineViewModel { Name = "Rejected", Price = 100 });
        Assert.False(model.SaveInvoice()); Assert.Contains("Unable to save", model.Status);
        using var intact = fixture.CreateDbContext(); Assert.Empty(intact.Invoices); Assert.Empty(intact.InvoiceItems); Assert.Empty(intact.InventoryTransactions);
        Assert.Equal("00000001", model.PeekNextDocumentNumber("Invoice"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void BankSelection_PersistsHistoricalAccountEvenAfterLiveAccountIsUnavailable()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); model.AddBankAccount();
        var bank = model.BankAccounts.Single(); bank[0].Value = "Historical transfer"; bank[1].Value = "Historical bank"; bank[2].Value = "123456789";
        var id = bank[5].Value;
        model.SelectedInvoiceBank = model.InvoiceBankChoices.Single(choice => choice.Account?.Id == id);
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100 }); Assert.True(model.SaveInvoice(), model.Status);
        var editor = fixture.CreateModel(); Assert.True(editor.LoadDocumentForEditing(editor.Invoices.Single()));
        Assert.Equal(id, editor.SelectedInvoiceBank!.Account!.Id); Assert.Equal("Historical bank", editor.SelectedInvoiceBank.Account.BankName);
        Assert.Contains(editor.InvoiceBankChoices, choice => choice.Account?.Id == id);
        editor.OrderTime.Value = "09:15"; Assert.True(editor.SaveInvoice(), editor.Status);
        using var db = fixture.CreateDbContext(); Assert.Equal("123456789", db.Invoices.Single().Snapshot!.BankAccount!.AccountNumber);
        Assert.Equal(new TimeSpan(9, 15, 0), db.Invoices.Single().InvoiceDate.TimeOfDay);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SelectedBank_HistoricalSnapshotDrivesPdfEvenAfterBankSettingsChange()
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext()) { db.CompanyInfos.Add(new CompanyInfo { Name = "Business" }); db.SaveChanges(); }
        var model = fixture.CreateModel(); model.AddBankAccount();
        var bank = model.BankAccounts.Single(); bank[1].Value = "Historical bank"; bank[2].Value = "123456789";
        model.Settings["Company Info"].SelectMany(section => section.Fields).Single(field => field.Label == "Show bank details on invoice").IsChecked = true;
        Assert.True(model.SaveSettings("Company Info"), model.Status);
        model.SelectedInvoiceBank = model.InvoiceBankChoices[1];
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100 }); Assert.True(model.SaveInvoice());
        bank[1].Value = "UPDATED BANK"; bank[2].Value = "999999999";
        Assert.True(model.SaveSettings("Company Info"));
        var reopened = fixture.CreateModel(); Assert.True(reopened.LoadDocumentForEditing(reopened.Invoices.Single()));
        Assert.Contains(reopened.InvoiceBankChoices, choice => choice.Account == reopened.SelectedInvoiceBank!.Account);
        var bytes = reopened.ExportDocumentPdf(reopened.Invoices.Single());
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(bytes, new Docnet.Core.Models.PageDimensions(1));
        var text = string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index => { using var page = reader.GetPageReader(index); return page.GetText(); }));
        Assert.Contains("Historical bank", text); Assert.Contains("123456789", text); Assert.DoesNotContain("UPDATED BANK", text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public void PaidEdit_StockDeltaAndInvoiceUpdateCommitTogether(bool failUpdate)
    {
        using var fixture = new TestDatabaseFixture();
        int productId;
        using (var db = fixture.CreateDbContext())
        {
            var product = new Product { Name = "Stocked product", SalePrice = 100, StockQuantity = 20 };
            db.Products.Add(product); db.SaveChanges(); productId = product.Id;
        }
        var model = fixture.CreateModel(); model.InvoiceOptions[3].Value = "No Tax";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Stocked product", ProductKey = "id:" + productId, Price = 100, Quantity = 2 });
        Assert.True(model.SaveInvoice(), model.Status);
        var payment = FormCatalog.Payment(); payment[0].Value = "30";
        Assert.True(model.ApplyPayment(model.Invoices.Single(), payment));
        var editor = fixture.CreateModel(); Assert.True(editor.LoadDocumentForEditing(editor.Invoices.Single()));
        if (failUpdate)
        {
            using var db = fixture.CreateDbContext();
            db.Database.ExecuteSqlRaw("CREATE TRIGGER fail_edit BEFORE UPDATE ON invoices BEGIN SELECT RAISE(ABORT, 'Injected edit failure'); END;");
        }
        editor.Lines[0].Quantity = 1;
        Assert.Equal(!failUpdate, editor.SaveInvoice());
        using var result = fixture.CreateDbContext();
        Assert.Equal(failUpdate ? 18m : 19m, result.Products.Single().StockQuantity);
        Assert.Equal(failUpdate ? 200m : 100m, result.Invoices.Single().GrandTotal);
        Assert.Equal(failUpdate ? -2m : -1m, result.InventoryTransactions.Single().BaseQuantityChange);
        Assert.Equal(30m, result.Invoices.Single().PaidAmount);
        Assert.Equal(30m, result.Payments.Single().Amount);
    }
}
