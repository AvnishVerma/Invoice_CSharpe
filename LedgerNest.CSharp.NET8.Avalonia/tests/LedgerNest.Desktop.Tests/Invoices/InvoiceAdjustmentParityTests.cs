using System.Globalization;
using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Invoices;

public sealed class InvoiceAdjustmentParityTests
{
    [Theory]
    [InlineData("-12.50", true)]
    [InlineData("12.50", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    [Trait("Category", "Unit")]
    public void AdjustmentValidation_AllowsSignedAmountsOnlyOnAdjustmentFields(string value, bool valid)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var fields = InvoiceOptionsViewModel.CreateCost("Adjustment"); fields[1].Value = value;
            Assert.Equal(valid, fields[1].Validate());
            if (value.StartsWith('-')) Assert.False(new FormField("Product price", value, "number").Validate());
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculation_PreservesSignedAdjustmentAndExistingNonnegativeTotalPolicy()
    {
        var totals = InvoiceTotalsCalculator.Calculate([new(100, 1)], InvoiceTaxMode.None, additionalCosts: -12.50m);
        Assert.Equal(-12.50m, totals.AdditionalCosts); Assert.Equal(87.50m, totals.Total);
        Assert.Equal(0m, InvoiceTotalsCalculator.Calculate([new(100, 1)], InvoiceTaxMode.None, additionalCosts: -150).Total);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SignedAdjustment_PersistsReopensClonesAndRejectsMalformedInput()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.InvoiceOptions[3].Value = "No Tax";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100 });
        model.AdditionalCosts.Add(InvoiceOptionsViewModel.CreateCost("Freight paid by buyer", -12.50m));
        Assert.True(model.SaveInvoice(), model.Status);
        using (var db = fixture.CreateDbContext())
        {
            Assert.Equal(87.50m, db.Invoices.Single().GrandTotal);
            Assert.Equal(-12.50m, db.Invoices.Single().Snapshot!.AdditionalCosts.Single().Amount);
        }
        var reopened = fixture.CreateModel(); Assert.True(reopened.LoadDocumentForEditing(reopened.Invoices.Single()));
        Assert.True(reopened.AdditionalCosts[0][1].Validate()); Assert.True(reopened.SaveInvoice(), reopened.Status);
        Assert.True(reopened.CloneDocumentForEditing(reopened.Invoices.Single()));
        Assert.Equal(-12.50m, reopened.AdditionalCosts[0][1].Number); Assert.True(reopened.AdditionalCosts[0][1].Validate());
        reopened.AdditionalCosts[0][1].Value = "invalid";
        Assert.False(reopened.SaveInvoice()); Assert.Contains("adjustments", reopened.Status);
        using var intact = fixture.CreateDbContext(); Assert.Single(intact.Invoices); Assert.Equal(87.50m, intact.Invoices.Single().GrandTotal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void DeductionBelowPaidAmount_IsRejectedWithoutChangingSnapshot()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel(); model.InvoiceOptions[3].Value = "No Tax";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Service", Price = 100 }); Assert.True(model.SaveInvoice());
        var payment = FormCatalog.Payment(); payment[0].Value = "30"; Assert.True(model.ApplyPayment(model.Invoices.Single(), payment));
        Assert.True(model.LoadDocumentForEditing(model.Invoices.Single()));
        model.AdditionalCosts.Add(InvoiceOptionsViewModel.CreateCost("Excess deduction", -80));
        Assert.False(model.SaveInvoice()); Assert.Contains("already paid", model.Status);
        using var db = fixture.CreateDbContext(); Assert.Equal(100m, db.Invoices.Single().GrandTotal); Assert.Empty(db.Invoices.Single().Snapshot!.AdditionalCosts);
    }

    [Theory]
    [InlineData("Classic")]
    [InlineData("Modern")]
    [InlineData("Minimal")]
    [InlineData("Executive")]
    [InlineData("Grid Classic")]
    [InlineData("Compact")]
    [InlineData("Thermal")]
    [Trait("Category", "Integration")]
    public void SignedAdjustment_RendersInPdfAndHtml(string template)
    {
        var invoice = new LedgerNest.Domain.Invoice { InvoiceNumber = "INV-ADJUST", GrandTotal = 87.50m,
            Snapshot = new(1, new("Customer", "", "", "", "", ""), null, "Invoice", "", false, false, "INR", "Qty", "No Tax", 0, "None", 0, "", [new("Freight deduction", -12.50m)]) };
        LedgerNest.Domain.InvoiceItem[] items = [new() { Description = "Service", UnitPrice = 100, Quantity = 1 }];
        var options = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true);
        var business = new DocumentPdf.Business("Business", "", "", "", "", "", "");
        var pdf = DocumentPdf.Create(invoice, items, business, "A4", false, template, options: options);
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdf, new Docnet.Core.Models.PageDimensions(1));
        var text = string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index => { using var page = reader.GetPageReader(index); return page.GetText(); }));
        Assert.Contains("Freight deduction", text); Assert.Contains("-12.50", text);
        var html = DocumentHtml.Create(invoice, items, business, "A4", false, template, "#002E78", options);
        Assert.Contains("Freight deduction", html); Assert.Contains("-12.50", html);
    }
}
