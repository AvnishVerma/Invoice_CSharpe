using LedgerNest.Application;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.Settings;

public sealed class PdfFontParityTests
{
    [Theory]
    [InlineData("Small", "Medium", .9f)]
    [InlineData("Medium", "Large", 1f)]
    [InlineData("Large", "Medium", 1.2f)]
    [InlineData("X-Large", "Medium", 1.4f)]
    [InlineData("Inherit", "Large", 1.2f)]
    [InlineData("Invalid", "X-Large", 1f)]
    [InlineData(null, "Large", 1f)]
    [Trait("Category", "Unit")]
    public void FontPresets_MatchUpstreamScalesAndSafeDefaults(string? preset, string inherited, float expected)
        => Assert.Equal(expected, PdfFontSizeRules.Resolve(preset, inherited));

    [Fact]
    [Trait("Category", "Integration")]
    public void FontSettings_PersistAndOldDatabaseGetsUnchangedDefaults()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var fields = model.Settings["PDF Settings"].SelectMany(section => section.Fields).ToArray();
        Assert.Equal("Medium", fields.Single(field => field.Label == "PDF Font Size").Value);
        Assert.All(fields.Where(field => field.Label.EndsWith("Font Size") && field.Label != "PDF Font Size"), field => Assert.Equal("Inherit", field.Value));
        fields.Single(field => field.Label == "PDF Font Size").Value = "Large";
        fields.Single(field => field.Label == "Table Items Font Size").Value = "Small";
        Assert.True(model.SaveSettings("PDF Settings"), model.Status);
        var restored = fixture.CreateModel().Settings["PDF Settings"].SelectMany(section => section.Fields).ToArray();
        Assert.Equal("Large", restored.Single(field => field.Label == "PDF Font Size").Value);
        Assert.Equal("Small", restored.Single(field => field.Label == "Table Items Font Size").Value);
    }

    [Theory]
    [InlineData("Classic")]
    [InlineData("Grid Classic")]
    [InlineData("Modern")]
    [InlineData("Minimal")]
    [InlineData("Executive")]
    [InlineData("Compact")]
    [Trait("Category", "Unit")]
    public void Fonts_AreAppliedToHtmlSectionsAndPdfPagination(string template)
    {
        var options = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true)
        { FontScale = 1.2f, CompanyFontScale = 1.4f, DocumentTitleFontScale = 1.4f, TableHeaderFontScale = 1.4f, TableItemsFontScale = 1.4f, TotalsFontScale = 1.4f };
        var invoice = new Invoice { InvoiceNumber = "INV-FONTS", GrandTotal = 500 };
        var items = Enumerable.Range(1, 80).Select(index => new InvoiceItem { Description = "Product " + index, Quantity = 1, UnitPrice = 10 }).ToArray();
        var business = new DocumentPdf.Business("Company", "", "", "", "", "", "");
        var html = DocumentHtml.Create(invoice, items, business, "A4", false, template, "#002E78", options);
        Assert.Contains(".sheet{font-size:14.4px}", html);
        Assert.Contains(".title{font-size:39.2px}", html);
        Assert.Contains(".business-name{font-size:22.4px}", html);
        Assert.Contains("th{font-size:15.4px}", html);
        Assert.Contains("td{font-size:16.8px}", html);
        var pdf = DocumentPdf.Create(invoice, items, business, "A4", false, template, options: options);
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdf, new Docnet.Core.Models.PageDimensions(1));
        Assert.True(reader.GetPageCount() > 1);
        using var last = reader.GetPageReader(reader.GetPageCount() - 1);
        Assert.Contains("Balance due", last.GetText());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ThermalFonts_RemainIndependentOfNonthermalSettings()
    {
        var baseline = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true);
        var larger = baseline with { FontScale = 1.4f, CompanyFontScale = 1.4f, DocumentTitleFontScale = 1.4f, TableHeaderFontScale = 1.4f, TableItemsFontScale = 1.4f, TotalsFontScale = 1.4f };
        var invoice = new Invoice { InvoiceNumber = "INV-1" };
        var business = new DocumentPdf.Business("Company", "", "", "", "", "", "");
        Assert.Equal(DocumentHtml.Create(invoice, [], business, "Thermal 80mm", false, "Thermal", "#002E78", baseline),
            DocumentHtml.Create(invoice, [], business, "Thermal 80mm", false, "Thermal", "#002E78", larger));
    }
}
