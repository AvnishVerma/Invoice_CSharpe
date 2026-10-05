using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;

namespace LedgerNest.Desktop.Tests.Invoices;

[Trait("Category", "Unit")]
public sealed class PdfFieldParityTests
{
    [Theory]
    [InlineData("Classic")]
    [InlineData("Modern")]
    [InlineData("Minimal")]
    [InlineData("Executive")]
    [InlineData("Grid Classic")]
    [InlineData("Compact")]
    [InlineData("Thermal")]
    public void CustomFieldsAndMetadata_RenderAcrossSupportedTemplates(string template)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-FIELDS", GrandTotal = 100,
            Snapshot = new(1, new("Customer", "", "", "", "", ""), null, "Invoice", "", false, false, "INR", "Qty", "Per Item", 0, "None", 0, "", [])
            {
                CustomFields = [new("reference", "Purchase Order", "PO-HISTORICAL"), new("empty", "EMPTY-FIELD-MARKER", "")],
                LinePresentations = [new("", "Product", new() { ["Batch"] = "BATCH-HISTORICAL" })]
            }
        };
        var item = new InvoiceItem { Description = "Product", Quantity = 1, UnitPrice = 100 };
        var options = new DocumentPdf.PdfExportOptions("dd/MM/yyyy", "24 hour", false, "Qty", true, false, true, true, true, true, true, true, true, true, true, true, true, true, true)
        { MetadataColumns = ["Batch"] };
        var business = new DocumentPdf.Business("Business", "", "", "", "", "", "");
        var pdf = DocumentPdf.Create(invoice, [item], business, "A4", false, template, options: options);
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdf, new Docnet.Core.Models.PageDimensions(1));
        var text = string.Join("\n", Enumerable.Range(0, reader.GetPageCount()).Select(index =>
        { using var page = reader.GetPageReader(index); return page.GetText(); }));
        Assert.Contains("PO-HISTORICAL", text);
        Assert.DoesNotContain("EMPTY-FIELD-MARKER", text);
        var unwrappedText = string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
        if (template == "Thermal") Assert.DoesNotContain("BATCH-HISTORICAL", unwrappedText);
        else Assert.Contains("BATCH-HISTORICAL", unwrappedText);
        var html = DocumentHtml.Create(invoice, [item], business, "A4", false, template, "#002E78", options);
        Assert.Contains("PO-HISTORICAL", html);
        Assert.DoesNotContain("EMPTY-FIELD-MARKER", html);
        if (template == "Thermal") Assert.DoesNotContain("BATCH-HISTORICAL", html);
        else Assert.Contains("BATCH-HISTORICAL", html);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CustomFieldSnapshot_PersistsEvenWhenDefinitionsAreRenamedLater()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Settings["Invoice Settings"].SelectMany(section => section.Fields).Single(field => field.Label == "Enable Custom Fields").IsChecked = true;
        model.StartDocument("Invoice");
        model.InvoiceCustomFields[0].Field.Value = "HISTORICAL-VALUE";
        model.Lines.Add(new InvoiceLineViewModel { Name = "Product", Price = 100 });
        Assert.True(model.SaveInvoice(), model.Status);
        var oldLabel = model.InvoiceCustomFields[0].Field.Label;
        model.InvoiceCustomFieldDefinitions[0].Label = "Renamed definition";
        Assert.True(model.SaveSettings("Invoice Settings"), model.Status);
        using var db = fixture.CreateDbContext();
        var saved = db.Invoices.Single().Snapshot!.CustomFields!.Single(field => field.Value == "HISTORICAL-VALUE");
        Assert.Equal(oldLabel, saved.Label);
        Assert.NotEqual("Renamed definition", saved.Label);
    }
}
