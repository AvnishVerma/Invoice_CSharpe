using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    // Performs the export document pdf action for this screen or workflow.
    public byte[] ExportDocumentPdf(UiRecord document)
    {
        var context = BuildDocumentExportContext(document);
        var bytes = DocumentPdf.Create(context.Invoice, context.Items, context.Business, context.PageSize, context.Landscape, context.Template, context.ThemeColor, context.Options);
        Status = $"Exported {document.Name} PDF.";
        return bytes;
    }

    // Creates self-contained invoice HTML for Chromium direct printing.
    public string ExportDocumentHtml(UiRecord document)
    {
        var context = BuildDocumentExportContext(document);
        return DocumentHtml.Create(context.Invoice, context.Items, context.Business, context.PageSize, context.Landscape, context.Template, context.ThemeColor, context.Options);
    }

    // Resolves the saved invoice, line items, and configured layout shared by PDF and HTML output.
    private DocumentExportContext BuildDocumentExportContext(UiRecord document)
    {
        Invoice invoice;
        if (dbFactory != null && document.SourceId > 0)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            invoice = db.Invoices.AsNoTracking().FirstOrDefault(i => i.Id == document.SourceId)
                ?? new Invoice
                {
                    InvoiceNumber = document.Name, Type = document["Type"], CustomerName = document["Customer"],
                    InvoiceDate = DateTime.TryParse(document["Date"], out var missingDate) ? missingDate : DateTime.Today,
                    Status = document["Status"], GrandTotal = ParseDecimal(document["Total"]),
                    PaidAmount = ParseDecimal(document["Paid"]), TaxTotal = ParseDecimal(document["Tax"])
                };
        }
        else invoice = new Invoice
        {
            InvoiceNumber = document.Name, Type = document["Type"], CustomerName = document["Customer"],
            InvoiceDate = DateTime.TryParse(document["Date"], out var date) ? date : DateTime.Today,
            Status = document["Status"], GrandTotal = ParseDecimal(document["Total"]),
            PaidAmount = ParseDecimal(document["Paid"]), TaxTotal = ParseDecimal(document["Tax"])
        };
        string Setting(string category, string label) => Settings[category].SelectMany(s => s.Fields).FirstOrDefault(f => f.Label == label)?.Value ?? "";
        var showLogo = Settings["Company Info"].SelectMany(s => s.Fields).First(f => f.Label == "Show on PDF").IsChecked;
        var business = new DocumentPdf.Business(Setting("Company Info", "Company Name"), Setting("Company Info", "Address"),
            Setting("Company Info", "Phone"), Setting("Company Info", "Email"), Setting("Company Info", "GSTIN"),
            showLogo ? Setting("Company Info", "Company logo") : "", Setting("Invoice Settings", "Thank You Note"));
        bool Checked(string category, string label) => Settings[category].SelectMany(s => s.Fields).FirstOrDefault(f => f.Label == label)?.IsChecked ?? false;
        var pdfOptions = new DocumentPdf.PdfExportOptions(
            Setting("Invoice Settings", "Date Format"),
            Setting("Invoice Settings", "Time Format"),
            Checked("Invoice Settings", "Show time in PDF"),
            Setting("Invoice Settings", "Quantity Column"),
            Checked("Invoice Settings", "Show Description"),
            Checked("Invoice Settings", "Description on new line"),
            Checked("Invoice Settings", "Show Customer Business Name"),
            Checked("Invoice Settings", "Show Customer Address"),
            Checked("Invoice Settings", "Show Customer Phone"),
            Checked("Invoice Settings", "Show Customer Email"),
            Checked("Invoice Settings", "Show Customer GSTIN"),
            Checked("Invoice Settings", "Show Sl. No."),
            Checked("Invoice Settings", "Item Name"),
            Checked("Invoice Settings", "Show Quantity"),
            Checked("Invoice Settings", "Price"),
            Checked("Invoice Settings", "Tax"),
            Checked("Invoice Settings", "Show Discount"),
            Checked("Invoice Settings", "Total"),
            Checked("PDF Settings", "Show Total Quantity"));
        return new DocumentExportContext(
            invoice,
            InvoiceItemsFor(document),
            business,
            Setting("PDF Settings", "Page Size"),
            Setting("PDF Settings", "Orientation") == "Landscape",
            Setting("PDF Settings", "Template"),
            Setting("PDF Settings", "Theme Color"),
            pdfOptions);
    }

    // Performs the preview items for action for this screen or workflow.
    public InvoiceItem[] PreviewItemsFor(UiRecord document) => InvoiceItemsFor(document);

    // Performs the invoice items for action for this screen or workflow.
    private InvoiceItem[] InvoiceItemsFor(UiRecord document)
    {
        if (dbFactory == null || document.SourceId <= 0) return [];
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return db.InvoiceItems.AsNoTracking().Where(i => i.InvoiceId == document.SourceId).OrderBy(i => i.Id).ToArray();
    }

    // Holds the resolved document and presentation settings used by each output renderer.
    private sealed record DocumentExportContext(
        Invoice Invoice,
        InvoiceItem[] Items,
        DocumentPdf.Business Business,
        string PageSize,
        bool Landscape,
        string Template,
        string ThemeColor,
        DocumentPdf.PdfExportOptions Options);


}
