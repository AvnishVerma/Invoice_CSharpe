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
    public string EditorCurrency => editingSnapshot?.Currency ?? InvoiceSetting("Currency").Value;
    // Performs the peek next document number action for this screen or workflow.
    public string PeekNextDocumentNumber(string type)
    {
        ValidateDocumentType(type);
        if (dbFactory == null)
            return NextDocumentNumber(type, Invoices.Where(i => i["Type"] == type).Select(i => i.Name), InvoiceStartingNumber());
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return NextDocumentNumber(db, type);
    }

    // Performs the validate document type action for this screen or workflow.
    private static void ValidateDocumentType(string type)
    {
        if (type is not ("Invoice" or "Quotation" or "Receipt"))
            throw new ArgumentException("Unknown document type.", nameof(type));
    }

    // Performs the invoice starting number action for this screen or workflow.
    private long InvoiceStartingNumber()
    {
        var field = Settings["Invoice Settings"].SelectMany(s => s.Fields).Single(f => f.Label == "Starting Number");
        return long.TryParse(field.Value, out var start) && start > 0 ? start : 1;
    }

    // Performs the next document number action for this screen or workflow.
    private string NextDocumentNumber(LedgerNestDbContext db, string type)
    {
        var key = SettingKey("Invoice Settings", "General", "Starting Number");
        var setting = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == key)?.Value;
        var start = long.TryParse(setting, out var parsed) && parsed > 0 ? parsed : InvoiceStartingNumber();
        return NextDocumentNumber(type, db.Invoices.AsNoTracking().Where(i => i.Type == type).Select(i => i.InvoiceNumber).ToArray(), start);
    }

    // Performs the next document number action for this screen or workflow.
    private static string NextDocumentNumber(string type, IEnumerable<string> existing, long start)
    {
        // Earlier C# records used INV-0001; keep their numeric suffix in the sequence.
        var maximum = existing.Select(number =>
        {
            var digits = new string(number.Where(char.IsAsciiDigit).ToArray());
            return long.TryParse(digits, out var value) ? value : 0;
        }).DefaultIfEmpty(0).Max();
        var next = maximum > 0 ? checked(maximum + 1) : type == "Invoice" ? start : 1;
        return next.ToString("D8", CultureInfo.InvariantCulture);
    }

    private UiRecord? editingDocument;
    private string? editingFingerprint;
    private InvoiceSnapshot? editingSnapshot;
    private readonly Dictionary<InvoiceLineViewModel, InvoiceItem> historicalLines = [];
    public bool IsEditingDocument => editingDocument != null;
    public string EditorDocumentNumber => editingDocument?.Name ?? PeekNextDocumentNumber(InvoiceDetails[0].Value);
    public UiRecord? LastSavedDocument { get; private set; }

    // Performs the fingerprint action for this screen or workflow.
    private static string Fingerprint(Invoice invoice) =>
        JsonSerializer.Serialize(new { Invoice = invoice, Items = invoice.Items.OrderBy(i => i.Id).ToArray() });

    // Performs the clone document for editing action for this screen or workflow.
    public bool CloneDocumentForEditing(UiRecord record)
    {
        if (dbFactory == null) { Status = "Cloning requires saved document storage."; return false; }
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var invoice = db.Invoices.Include(i => i.Items).SingleOrDefault(i => i.Id == record.SourceId);
        if (invoice == null || invoice.DeletedAt != null) { Status = "Document is unavailable or in trash."; return false; }
        if (invoice.Snapshot is not { Version: 1 } snapshot)
        { Status = "This document lacks a supported historical snapshot and cannot be safely cloned."; return false; }

        editingDocument = null;
        editingSnapshot = null;
        editingFingerprint = null;
        historicalLines.Clear();
        Lines.Clear();
        AdditionalCosts.Clear();
        string[] customer = [snapshot.Customer.Name, snapshot.Customer.BusinessName, snapshot.Customer.Phone, snapshot.Customer.Email, snapshot.Customer.GstNumber, snapshot.Customer.Address];
        for (var i = 0; i < customer.Length; i++) InvoiceCustomer[i].Value = customer[i];
        InvoiceDetails[0].Value = invoice.Type;
        InvoiceDetails[1].Value = DateTime.Today.ToString("yyyy-MM-dd");
        InvoiceDetails[2].Value = snapshot.DueDate?.ToString("yyyy-MM-dd") ?? "";
        InvoiceDetails[3].Value = snapshot.DocumentTitle;
        InvoiceDetails[4].Value = "";
        HideInvoiceNumber.IsChecked = snapshot.HideInvoiceNumber;
        InterState.IsChecked = snapshot.IsInterState;
        InvoiceOptions[0].Value = snapshot.DiscountKind;
        InvoiceOptions[1].Value = snapshot.DiscountValue.ToString(CultureInfo.CurrentCulture);
        InvoiceOptions[2].Value = snapshot.Notes;
        InvoiceOptions[3].Value = snapshot.TaxMode;
        InvoiceOptions[4].Value = snapshot.TaxRate.ToString(CultureInfo.CurrentCulture);
        foreach (var cost in snapshot.AdditionalCosts)
            AdditionalCosts.Add([new("Description", cost.Description), new("Amount", cost.Amount.ToString(CultureInfo.CurrentCulture), "number")]);
        foreach (var item in invoice.Items)
        {
            var line = new InvoiceLineViewModel { ProductKey = item.ProductId is int productId ? $"id:{productId}" : "", ProductType = snapshot.LinePresentations?.ElementAtOrDefault(Lines.Count)?.ProductType ?? "Product", SavedPresentation = snapshot.LinePresentations?.ElementAtOrDefault(Lines.Count), Name = item.Description, Price = item.UnitPrice, Quantity = item.Quantity, Discount = item.Discount, DiscountPerUnit = item.DiscountPerUnit, TaxRate = item.TaxRate, PriceIncludesTax = item.PriceIncludesTax, ExtraCost = item.ExtraCost };
            line.Unit = snapshot.LineUnits?.ElementAtOrDefault(Lines.Count) ?? "None";
            Lines.Add(line);
        }
        InitializeInvoiceCustomValues(snapshot);
        NavigateCommand.Execute("New Invoice");
        Status = $"Cloned {record.Name} without payment history. Review and save to create a new invoice.";
        return true;
    }

    // Performs the load document for editing action for this screen or workflow.
    public bool LoadDocumentForEditing(UiRecord record)
    {
        if (dbFactory == null) { Status = "Editing requires saved document storage."; return false; }
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var invoice = db.Invoices.Include(i => i.Items).SingleOrDefault(i => i.Id == record.SourceId);
        if (invoice == null || invoice.DeletedAt != null) { Status = "Document is unavailable or in trash."; return false; }
        if (invoice.PaidAmount != 0 || db.Payments.Any(p => p.InvoiceId == invoice.Id))
        { Status = "Documents with payments cannot be edited yet."; return false; }
        if (invoice.Snapshot is not { Version: 1 } snapshot)
        { Status = "This document lacks a supported historical snapshot and cannot be safely edited."; return false; }
        editingDocument = record;
        editingSnapshot = snapshot;
        editingFingerprint = Fingerprint(invoice);
        historicalLines.Clear();
        Lines.Clear(); AdditionalCosts.Clear();
        string[] customer = [snapshot.Customer.Name, snapshot.Customer.BusinessName, snapshot.Customer.Phone, snapshot.Customer.Email, snapshot.Customer.GstNumber, snapshot.Customer.Address];
        for (var i = 0; i < customer.Length; i++) InvoiceCustomer[i].Value = customer[i];
        InvoiceDetails[0].Value = invoice.Type;
        InvoiceDetails[1].Value = invoice.InvoiceDate.ToString("yyyy-MM-dd");
        InvoiceDetails[2].Value = snapshot.DueDate?.ToString("yyyy-MM-dd") ?? "";
        InvoiceDetails[3].Value = snapshot.DocumentTitle;
        InvoiceDetails[4].Value = snapshot.CustomInvoiceNumber;
        HideInvoiceNumber.IsChecked = snapshot.HideInvoiceNumber;
        InterState.IsChecked = snapshot.IsInterState;
        InvoiceOptions[0].Value = snapshot.DiscountKind;
        InvoiceOptions[1].Value = snapshot.DiscountValue.ToString(CultureInfo.CurrentCulture);
        InvoiceOptions[2].Value = snapshot.Notes;
        InvoiceOptions[3].Value = snapshot.TaxMode;
        InvoiceOptions[4].Value = snapshot.TaxRate.ToString(CultureInfo.CurrentCulture);
        foreach (var cost in snapshot.AdditionalCosts)
            AdditionalCosts.Add([new("Description", cost.Description), new("Amount", cost.Amount.ToString(CultureInfo.CurrentCulture), "number")]);
        foreach (var item in invoice.Items)
        {
            var line = new InvoiceLineViewModel { ProductKey = item.ProductId is int productId ? $"id:{productId}" : "", ProductType = snapshot.LinePresentations?.ElementAtOrDefault(Lines.Count)?.ProductType ?? "Product", SavedPresentation = snapshot.LinePresentations?.ElementAtOrDefault(Lines.Count), Name = item.Description, Price = item.UnitPrice, Quantity = item.Quantity, Discount = item.Discount, DiscountPerUnit = item.DiscountPerUnit, TaxRate = item.TaxRate, PriceIncludesTax = item.PriceIncludesTax, ExtraCost = item.ExtraCost };
            line.Unit = snapshot.LineUnits?.ElementAtOrDefault(Lines.Count) ?? "None";
            historicalLines.Add(line, item);
            Lines.Add(line);
        }
        InitializeInvoiceCustomValues(snapshot);
        NavigateCommand.Execute("New Invoice");
        return true;
    }

    // Performs the save invoice action for this screen or workflow.
    public bool SaveInvoice()
    {
        if (!RequireBusinessLicense()) return false;
        if (Lines.Count == 0) { Status = "Add at least one item before creating an invoice."; return false; }
        if (!InvoiceSetting("Allow Fractional Quantity").IsChecked && Lines.Any(line => decimal.Truncate(line.Quantity) != line.Quantity))
        { Status = "Fractional quantities are disabled in Invoice Settings."; return false; }
        if (!InvoiceSetting("Allow Duplicate Items").IsChecked && Lines.Where(line => line.ProductKey.Length > 0).GroupBy(line => line.ProductKey).Any(group => group.Count() > 1))
        { Status = "Duplicate products are disabled in Invoice Settings."; return false; }
        if (Lines.Any(l => string.IsNullOrWhiteSpace(l.Name) || l.Quantity <= 0 || l.Price < 0 || l.TaxRate < 0 || l.Discount < 0))
        { Status = "Check item names, quantities, prices, tax and discounts."; return false; }
        var values = new Dictionary<string, string> {
            ["Name"] = PeekNextDocumentNumber(InvoiceDetails[0].Value), ["Customer"] = InvoiceCustomer[0].Value,
            ["Type"] = InvoiceDetails[0].Value, ["Date"] = InvoiceDetails[1].Value,
            ["Due Date"] = InvoiceDetails[2].Value,
            ["Tax"] = Totals.Tax.ToString(CultureInfo.InvariantCulture),
            ["Paid"] = "0.00", ["Outstanding"] = Totals.Total.ToString("0.00"),
            ["Items"] = Lines.Count.ToString(), ["Total"] = Totals.Total.ToString("0.00"), ["Status"] = "Unpaid"
        };
        var sourceId = SaveInvoiceToDatabase(values);
        if (sourceId < 0) return false;
        var saved = new UiRecord { SourceId = sourceId, Values = values };
        if (editingDocument == null) Invoices.Add(saved);
        else
        {
            var index = Invoices.IndexOf(editingDocument);
            if (index >= 0) Invoices[index] = saved; else Invoices.Add(saved);
            editingDocument = saved;
        }
        LastSavedDocument = saved;
        InvoiceChanged?.Invoke();
        Status = $"{InvoiceDetails[0].Value} saved.";
        return true;
    }




    // Performs the add product line action for this screen or workflow.
    public void AddProductLine(UiRecord product) => TryAddInvoiceLine(CreateProductLine(product));

    // Performs the create product line action for this screen or workflow.
    public static InvoiceLineViewModel CreateProductLine(UiRecord product) => new()
    {
        ProductKey = ProductKey(product),
        ProductType = product["Type"] is "Service" ? "Service" : "Product",
        Unit = product["Unit"] == "Custom…" ? product["Custom unit"] : product["Unit"],
        Name = product.Name,
        Price = ParseDecimal(product["Sale Price"]),
        TaxRate = ParseDecimal(product["Tax (%)"]),
        Discount = ParseDecimal(product["Default Discount"]),
        DiscountPerUnit = true,
        PriceIncludesTax = bool.TryParse(product["Price includes tax"], out var inclusive) && inclusive
    };

    // Performs the start document action for this screen or workflow.
    public void StartDocument(string type)
    {
        if (type is not ("Invoice" or "Quotation" or "Receipt"))
            throw new ArgumentException("Unknown document type.", nameof(type));
        editingDocument = null; editingSnapshot = null; editingFingerprint = null; historicalLines.Clear();
        Lines.Clear();
        AdditionalCosts.Clear();
        foreach (var field in InvoiceCustomer) field.Value = "";
        ApplyDefaultCustomer();
        InvoiceDetails[0].Value = type;
        InvoiceDetails[1].Value = DateTime.Today.ToString("yyyy-MM-dd");
        InvoiceDetails[2].Value = "";
        InvoiceDetails[3].Value = type == "Invoice" ? InvoiceSetting("Default GST Title").Value : type;
        InvoiceDetails[4].Value = "";
        HideInvoiceNumber.IsChecked = InvoiceSetting("Hide Invoice Number").IsChecked;
        InterState.IsChecked = false;
        InvoiceOptions[0].Value = "None";
        InvoiceOptions[1].Value = "0";
        InvoiceOptions[2].Value = "";
        InvoiceOptions[3].Value = InvoiceSetting("Tax Enabled").IsChecked ? InvoiceSetting("Tax Mode").Value : "No Tax";
        InvoiceOptions[4].Value = InvoiceSetting("Default Tax Rate (%)").Value;
        InitializeInvoiceCustomValues();
        NavigateCommand.Execute("New Invoice");
    }

    // Performs the set default customer action for this screen or workflow.
    public void SetDefaultCustomer(UiRecord? customer)
    {
        if (customer != null && !Customers.Contains(customer)) return;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            SetSetting(db, "invoice.default_customer", customer?.SourceId.ToString(CultureInfo.InvariantCulture) ?? "");
            db.SaveChanges();
        }
        defaultCustomerId = customer?.SourceId;
        Status = customer == null ? "Default customer cleared." : $"Default customer: {customer.Name}.";
    }
    private int? defaultCustomerId;
    // Performs the apply default customer action for this screen or workflow.
    private void ApplyDefaultCustomer()
    {
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            var value = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "invoice.default_customer")?.Value;
            defaultCustomerId = int.TryParse(value, out var id) ? id : null;
        }
        var customer = Customers.FirstOrDefault(c => c.SourceId == defaultCustomerId);
        if (customer != null) foreach (var field in InvoiceCustomer) field.Value = customer[field.Label];
    }

}
