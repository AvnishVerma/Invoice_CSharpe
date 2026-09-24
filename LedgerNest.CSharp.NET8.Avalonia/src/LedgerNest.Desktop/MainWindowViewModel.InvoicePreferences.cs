using System.Collections.ObjectModel;
using System.Text.Json;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public static readonly string[] InvoiceMetadataLabels = ["Storage Location", "Container Number", "Batch Number", "Expiry Date", "Manufacture Date", "Manufacturer Name", "Supplier Name", "SKU Code", "Notes"];
    private static readonly string[] DefaultCustomLabels = ["Delivery Note", "Mode/Terms of Payment", "Reference No. & Date", "Other References", "Buyer's Order No.", "Buyer's Order Date", "Dispatch Doc No.", "Delivery Note Date", "Dispatched Through", "Destination", "Bill of Lading/LR-RR No.", "Motor Vehicle No.", "Terms of Delivery"];
    public ObservableCollection<InvoiceCustomFieldDefinition> InvoiceCustomFieldDefinitions { get; } = new(DefaultCustomLabels.Select((label, index) => new InvoiceCustomFieldDefinition { Id = $"cf-{index + 1}", Label = label }));
    public ObservableCollection<InvoiceCustomFieldEntry> InvoiceCustomFields { get; } = [];

    public bool AddInvoiceCustomField(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) { Status = "Enter a custom field label."; return false; }
        if (label.Trim().Length > 100) { Status = "Custom field labels must be 100 characters or fewer."; return false; }
        InvoiceCustomFieldDefinitions.Add(new InvoiceCustomFieldDefinition { Label = label.Trim() });
        return true;
    }

    public void MoveInvoiceCustomField(InvoiceCustomFieldDefinition field, int offset)
    {
        var index = InvoiceCustomFieldDefinitions.IndexOf(field);
        var target = index + offset;
        if (index >= 0 && target >= 0 && target < InvoiceCustomFieldDefinitions.Count) InvoiceCustomFieldDefinitions.Move(index, target);
    }

    public void RemoveInvoiceCustomField(InvoiceCustomFieldDefinition field) => InvoiceCustomFieldDefinitions.Remove(field);

    private void LoadInvoiceCustomDefinitions()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        var json = db.Settings.AsNoTracking().FirstOrDefault(setting => setting.Key == "invoice.custom_field_definitions")?.Value;
        if (json == null) return; // An explicitly saved empty list must stay empty.
        try
        {
            var definitions = JsonSerializer.Deserialize<InvoiceCustomFieldDefinition[]>(json) ?? [];
            InvoiceCustomFieldDefinitions.Clear();
            foreach (var definition in definitions.Where(field => !string.IsNullOrWhiteSpace(field.Id)).DistinctBy(field => field.Id)) InvoiceCustomFieldDefinitions.Add(definition);
        }
        catch (JsonException) { Status = "Custom field definitions could not be read."; }
    }

    private void InitializeInvoiceCustomValues(InvoiceSnapshot? snapshot = null)
    {
        InvoiceCustomFields.Clear();
        if (snapshot != null)
        {
            foreach (var field in snapshot.CustomFields ?? []) InvoiceCustomFields.Add(new(field.Id, new FormField(field.Label, field.Value)));
        }
        else if (InvoiceSetting("Enable Custom Fields").IsChecked)
            foreach (var field in InvoiceCustomFieldDefinitions) InvoiceCustomFields.Add(new(field.Id, new FormField(field.Label)));
    }

    public bool TryAddInvoiceLine(InvoiceLineViewModel line)
    {
        if (!InvoiceSetting("Allow Fractional Quantity").IsChecked && decimal.Truncate(line.Quantity) != line.Quantity)
        { Status = "Fractional quantities are disabled in Invoice Settings."; return false; }
        if (!InvoiceSetting("Allow Duplicate Items").IsChecked && line.ProductKey.Length > 0 && Lines.Any(existing => existing.ProductKey == line.ProductKey))
        { Status = "This product is already on the invoice. Enable duplicate invoice items to add it again."; return false; }
        Lines.Add(line);
        return true;
    }

    private InvoiceLinePresentation CaptureLinePresentation(InvoiceLineViewModel line)
    {
        if (line.SavedPresentation != null) return line.SavedPresentation;
        var product = Products.FirstOrDefault(product => ProductKey(product) == line.ProductKey)
            ?? Products.FirstOrDefault(product => product.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
        return new InvoiceLinePresentation(product?["Alias Name (for invoice PDF)"] ?? "", product?["Type"] is { Length: > 0 } type ? type : line.ProductType,
            InvoiceMetadataLabels.ToDictionary(label => label, label => product?[label] ?? ""));
    }

    private static string ProductKey(UiRecord product) => product.SourceId > 0 ? $"id:{product.SourceId}" : $"local:{product.Id}";

    private decimal PreviousBalanceFor(Invoice invoice)
    {
        if (dbFactory == null || invoice.Type != "Invoice" || invoice.CustomerId == null || !InvoiceSetting("Show Previous Balance").IsChecked) return 0;
        using var db = dbFactory.CreateDbContext();
        var currency = invoice.Snapshot?.Currency;
        return db.Invoices.AsNoTracking().Where(other => other.CustomerId == invoice.CustomerId && other.Type == "Invoice" && other.DeletedAt == null && other.Id != invoice.Id)
            .AsEnumerable().Where(other => (other.InvoiceDate.Date < invoice.InvoiceDate.Date || other.InvoiceDate.Date == invoice.InvoiceDate.Date && other.Id < invoice.Id)
                && other.Snapshot?.Currency == currency).Sum(other => Math.Max(0, other.GrandTotal - other.PaidAmount));
    }
}
