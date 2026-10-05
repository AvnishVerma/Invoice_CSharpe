using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Invoices;

[Trait("Category", "Integration")]
public sealed class InvoiceWorkflowTests
{
    [Fact]
    public void Invoice_CreateReloadVoid_UpdatesDatabaseAndRejectsDuplicateVoid()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Lines.Add(new InvoiceLineViewModel { Name = "Test service", Quantity = 2, Price = 100, TaxRate = 18 });
        Assert.True(model.SaveInvoice(), model.Status);
        var invoice = Assert.Single(model.Invoices);
        Assert.True(model.LoadDocumentForEditing(invoice), model.Status);
        Assert.True(model.CanVoidCurrentInvoice);
        Assert.True(model.VoidCurrentInvoice(), model.Status);
        Assert.False(model.CanVoidCurrentInvoice);
        Assert.False(model.VoidCurrentInvoice());
        using var db = fixture.CreateDbContext();
        Assert.Equal("Voided", db.Invoices.Find(invoice.SourceId)!.Status);
        Assert.Equal("Voided", Assert.Single(fixture.CreateModel().Invoices)["Status"]);
    }

    [Theory]
    [InlineData("", 1, 10)]
    [InlineData("Test", 0, 10)]
    [InlineData("Test", -1, 10)]
    [InlineData("Test", 1, -1)]
    public void Invoice_InvalidLine_NoPartialInvoiceOrSequenceAdvance(string name, decimal quantity, decimal price)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var next = model.PeekNextDocumentNumber("Invoice");
        model.Lines.Add(new InvoiceLineViewModel { Name = name, Quantity = quantity, Price = price });
        Assert.False(model.SaveInvoice());
        Assert.Empty(fixture.CreateModel().Invoices);
        Assert.Equal(next, model.PeekNextDocumentNumber("Invoice"));
    }

    [Fact]
    public async Task Invoice_RestrictedUser_ServiceEntryPointsRejectUnauthorizedCreateAndVoid()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Lines.Add(new InvoiceLineViewModel { Name = "Test", Price = 10 });
        Assert.True(model.SaveInvoice());
        var invoice = Assert.Single(model.Invoices);
        Assert.True(model.LoadDocumentForEditing(invoice));
        await TestData.CreateRole(model, "Viewer");
        TestData.CreateUser(model, "viewer", "Viewer");
        TestData.SetPermissions(fixture, "Viewer", "Invoice", "View");
        model.SignIn("viewer", TestData.Password);
        Assert.False(model.VoidCurrentInvoice());
        model.StartDocument("Invoice");
        Assert.False(model.SaveInvoice());
        Assert.Single(fixture.CreateModel().Invoices);
    }
}

[Trait("Category", "Unit")]
public sealed class InvoiceCalculationTests
{
    [Theory]
    [InlineData(InvoiceTaxMode.PerItem, 18, 236)]
    [InlineData(InvoiceTaxMode.Global, 5, 210)]
    [InlineData(InvoiceTaxMode.None, 18, 200)]
    public void Calculate_TaxMode_UsesDecimalExpectedTotals(InvoiceTaxMode mode, decimal rate, decimal expected)
        => Assert.Equal(expected, InvoiceTotalsCalculator.Calculate([new InvoiceLineInput(100, 2, TaxRatePercent: 18)], mode, rate).Total);

    [Theory]
    [InlineData(InvoiceDiscountKind.Amount, 10, 90)]
    [InlineData(InvoiceDiscountKind.Percent, 10, 90)]
    [InlineData(InvoiceDiscountKind.None, 10, 100)]
    [InlineData(InvoiceDiscountKind.Amount, 200, 0)]
    public void Calculate_DiscountAndZeroFloor_PreservesBusinessRules(InvoiceDiscountKind kind, decimal discount, decimal expected)
        => Assert.Equal(expected, InvoiceTotalsCalculator.Calculate([new InvoiceLineInput(100, 1)], InvoiceTaxMode.None, discountKind: kind, discountValue: discount).Total);

    [Fact]
    public void Calculate_FractionalQuantity_DoesNotPrematurelyRound()
        => Assert.Equal(0.333m, InvoiceTotalsCalculator.Calculate([new InvoiceLineInput(1, .333m)], InvoiceTaxMode.None).Total);
}
