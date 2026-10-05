using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.Settings;

public sealed class BankAccountParityTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void BankAccount_IbanPersists_AndLegacyAccountsLoadWithEmptyIban()
    {
        using var fixture = new TestDatabaseFixture();
        using (var db = fixture.CreateDbContext())
        {
            db.Settings.Add(new AppSetting { Key = "company.bank_accounts", Value = "[{\"Label\":\"Legacy\",\"Bank Name\":\"Bank\",\"Account Number\":\"123\",\"IFSC Code\":\"LEGACY\"}]" });
            db.CompanyInfos.Add(new CompanyInfo { Name = "Business" });
            db.SaveChanges();
        }
        var model = fixture.CreateModel();
        Assert.Equal("", model.BankAccounts[0][4].Value);
        model.BankAccounts[0][4].Value = "GB82WEST12345698765432";
        Assert.True(model.SaveSettings("Company Info"), model.Status);
        Assert.Equal("GB82WEST12345698765432", fixture.CreateModel().BankAccounts[0][4].Value);
    }

    [Theory]
    [InlineData("Classic")]
    [InlineData("Modern")]
    [InlineData("Minimal")]
    [InlineData("Executive")]
    [InlineData("Grid Classic")]
    [InlineData("Compact")]
    [InlineData("Thermal")]
    [Trait("Category", "Unit")]
    public void BankAccount_IbanPrintsAcrossTemplates(string template)
    {
        var invoice = new Invoice { InvoiceNumber = "INV-IBAN", GrandTotal = 100 };
        var options = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true)
        { ShowBankDetails = true, BankAccounts = [new("Transfer", "Bank", "", "", "GB82WEST12345698765432")] };
        var pdf = DocumentPdf.Create(invoice, [], new DocumentPdf.Business("Business", "", "", "", "", "", ""), "A4", false, template, options: options);
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdf, new Docnet.Core.Models.PageDimensions(1));
        var text = string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index => { using var page = reader.GetPageReader(index); return page.GetText(); }));
        Assert.Contains("IBAN: GB82WEST12345698765432", text);
    }
}
