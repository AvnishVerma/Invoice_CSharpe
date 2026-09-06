using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? dbFactory;
    public static readonly string[] Routes = ["Dashboard", "New Invoice", "Invoices", "Quotations", "Receipts", "Customers", "Products", "Reports", "Settings"];
    [ObservableProperty] private string title = "Dashboard";
    [ObservableProperty] private bool sidebarExpanded = true;
    [ObservableProperty] private string status = "";
    public HashSet<Guid> DeletedRecords { get; } = [];
    public ObservableCollection<UiRecord> Customers { get; } = [];
    public ObservableCollection<UiRecord> Products { get; } = [];
    public ObservableCollection<UiRecord> Users { get; } = [];
    public ObservableCollection<UiRecord> Invoices { get; } = [];
    public ObservableCollection<InvoiceLineViewModel> Lines { get; } = [];
    public Dictionary<string, FormSection[]> Settings { get; } = FormCatalog.Settings();
    public FormField[] InvoiceCustomer { get; } = FormCatalog.Customer();
    public FormField[] InvoiceDetails { get; } = [new("Type", "Invoice", "choice", ["Invoice", "Quotation", "Receipt"]), new("Order Date", DateTime.Today.ToString("yyyy-MM-dd"), "date"), new("Due Date", "", "date"), new("GST title", "Invoice", "choice", ["Invoice", "Tax Invoice", "Bill of Supply", "Invoice-cum-Bill of Supply", "Cash Bill", "Credit Note", "Debit Note", "Revised Invoice"]), new("PDF invoice number")];
    public FormField HideInvoiceNumber { get; } = new("Hide invoice number in PDF", "false", "toggle");
    public FormField InterState { get; } = new("Inter-state", "false", "toggle");
    public FormField[] InvoiceOptions { get; } = [new("Discount Type", "None", "choice", ["None", "Amount", "Percentage"]), new("Discount Value", "0", "number"), new("Notes", kind: "multiline"), new("Tax Mode", "Per Item", "choice", ["Per Item", "Global", "No Tax"]), new("Tax Rate (%)", "18", "number")];
    public ObservableCollection<FormField[]> AdditionalCosts { get; } = [];
    public CalculatedInvoiceTotals Totals => InvoiceTotalsCalculator.Calculate(
        Lines.Select(l => new InvoiceLineInput(l.Price, l.Quantity, l.Discount, l.DiscountPerUnit, l.ExtraCost, l.TaxRate, l.PriceIncludesTax)),
        InvoiceOptions[3].Value switch { "Global" => InvoiceTaxMode.Global, "No Tax" => InvoiceTaxMode.None, _ => InvoiceTaxMode.PerItem },
        InvoiceOptions[4].Number, AdditionalCosts.Sum(c => c[1].Number),
        InvoiceOptions[0].Value switch { "Amount" => InvoiceDiscountKind.Amount, "Percentage" => InvoiceDiscountKind.Percent, _ => InvoiceDiscountKind.None }, InvoiceOptions[1].Number);
    public event Action? InvoiceChanged;
    public MainWindowViewModel(IDbContextFactory<LedgerNestDbContext>? dbFactory = null)
    {
        this.dbFactory = dbFactory;
        foreach (var option in InvoiceOptions) option.PropertyChanged += (_, _) => InvoiceChanged?.Invoke();
        AdditionalCosts.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (FormField[] fields in e.NewItems) fields[1].PropertyChanged += (_, _) => InvoiceChanged?.Invoke();
            InvoiceChanged?.Invoke();
        };
        Lines.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (InvoiceLineViewModel line in e.NewItems) line.PropertyChanged += LineChanged;
            if (e.OldItems != null) foreach (InvoiceLineViewModel line in e.OldItems) line.PropertyChanged -= LineChanged;
            InvoiceChanged?.Invoke();
        };
        LoadPersistedRecords();
    }
    private void LineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvoiceChanged?.Invoke();
    [RelayCommand] private void Navigate(string route) { if (Routes.Contains(route)) { Title = route; Status = ""; } }
    [RelayCommand] private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;
    public bool SaveRecord(string kind, FormField[] fields, UiRecord? original = null)
    {
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        var records = kind == "Customer" ? Customers : kind == "Product" ? Products : Users;
        var values = fields.Where(f => f.Kind != "password").ToDictionary(f => f.Label, f => f.Kind == "toggle" ? f.IsChecked.ToString() : f.Value.Trim());
        var record = new UiRecord { SourceId = SaveRecordToDatabase(kind, values, original?.SourceId ?? 0), Values = values };
        if (original != null) records[records.IndexOf(original)] = record; else records.Add(record);
        Status = $"{kind} saved.";
        return true;
    }
    public bool SaveInvoice()
    {
        if (Lines.Count == 0) { Status = "Add at least one item before creating an invoice."; return false; }
        if (Lines.Any(l => string.IsNullOrWhiteSpace(l.Name) || l.Quantity <= 0 || l.Price < 0 || l.TaxRate < 0 || l.Discount < 0))
        { Status = "Check item names, quantities, prices, tax and discounts."; return false; }
        var values = new Dictionary<string, string> {
            ["Name"] = $"INV-{Invoices.Count + 1:0000}", ["Customer"] = InvoiceCustomer[0].Value,
            ["Type"] = InvoiceDetails[0].Value, ["Date"] = InvoiceDetails[1].Value,
            ["Items"] = Lines.Count.ToString(), ["Total"] = Totals.Total.ToString("0.00"), ["Status"] = "Unpaid"
        };
        Invoices.Add(new UiRecord { SourceId = SaveInvoiceToDatabase(values), Values = values });
        Status = $"{InvoiceDetails[0].Value} saved.";
        return true;
    }

    private void LoadPersistedRecords()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        foreach (var customer in db.Customers.AsNoTracking().OrderBy(c => c.Name))
        {
            Customers.Add(new UiRecord
            {
                SourceId = customer.Id,
                Values = new()
                {
                    ["Name"] = customer.Name,
                    ["Business Name"] = "",
                    ["Phone"] = customer.Phone ?? "",
                    ["Email"] = customer.Email ?? "",
                    ["GST / VAT Number"] = customer.GstNumber ?? "",
                    ["Address"] = customer.Address ?? ""
                }
            });
        }

        foreach (var product in db.Products.AsNoTracking().OrderBy(p => p.Name))
        {
            Products.Add(new UiRecord
            {
                SourceId = product.Id,
                Values = new()
                {
                    ["Type"] = "Product",
                    ["Name"] = product.Name,
                    ["SKU Code"] = product.Code ?? "",
                    ["Sale Price"] = product.SalePrice.ToString("0.##"),
                    ["Purchase Price"] = product.PurchasePrice.ToString("0.##"),
                    ["Tax (%)"] = product.TaxRate.ToString("0.##"),
                    ["Stock"] = product.StockQuantity.ToString("0.###")
                }
            });
        }

        foreach (var invoice in db.Invoices.AsNoTracking().Include(i => i.Items).OrderByDescending(i => i.InvoiceDate))
        {
            Invoices.Add(new UiRecord
            {
                SourceId = invoice.Id,
                Values = new()
                {
                    ["Name"] = invoice.InvoiceNumber,
                    ["Customer"] = Customers.FirstOrDefault(c => c.SourceId == invoice.CustomerId)?.Name ?? "",
                    ["Type"] = "Invoice",
                    ["Date"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["Items"] = invoice.Items.Count.ToString(),
                    ["Total"] = invoice.GrandTotal.ToString("0.00"),
                    ["Status"] = invoice.Status
                }
            });
        }
    }

    private int SaveRecordToDatabase(string kind, Dictionary<string, string> values, int sourceId)
    {
        if (dbFactory == null || kind == "User") return sourceId;
        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        if (kind == "Customer")
        {
            var customer = sourceId > 0 ? db.Customers.Find(sourceId) ?? new Customer() : new Customer();
            customer.Name = values.GetValueOrDefault("Name", "");
            customer.Phone = values.GetValueOrDefault("Phone");
            customer.Email = values.GetValueOrDefault("Email");
            customer.GstNumber = values.GetValueOrDefault("GST / VAT Number");
            customer.Address = values.GetValueOrDefault("Address");
            if (customer.Id == 0) db.Customers.Add(customer);
            db.SaveChanges();
            return customer.Id;
        }

        if (kind == "Product")
        {
            var product = sourceId > 0 ? db.Products.Find(sourceId) ?? new Product() : new Product();
            product.Name = values.GetValueOrDefault("Name", "");
            product.Code = values.GetValueOrDefault("SKU Code");
            product.SalePrice = ParseDecimal(values.GetValueOrDefault("Sale Price"));
            product.PurchasePrice = ParseDecimal(values.GetValueOrDefault("Purchase Price"));
            product.TaxRate = ParseDecimal(values.GetValueOrDefault("Tax (%)"));
            product.StockQuantity = ParseDecimal(values.GetValueOrDefault("Stock"));
            if (product.Id == 0) db.Products.Add(product);
            db.SaveChanges();
            return product.Id;
        }

        return sourceId;
    }

    private int SaveInvoiceToDatabase(Dictionary<string, string> values)
    {
        if (dbFactory == null) return 0;
        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
        var customerName = InvoiceCustomer[0].Value.Trim();
        var customerId = db.Customers.AsNoTracking().FirstOrDefault(c => c.Name == customerName)?.Id;
        var invoice = new Invoice
        {
            InvoiceNumber = values["Name"],
            InvoiceDate = DateTime.TryParse(InvoiceDetails[1].Value, out var date) ? date : DateTime.Today,
            CustomerId = customerId,
            Status = "Unpaid",
            SubTotal = Totals.Subtotal,
            TaxTotal = Totals.Tax,
            DiscountTotal = Totals.ItemDiscount + Totals.InvoiceDiscount,
            GrandTotal = Totals.Total,
            Items = Lines.Select(line => new InvoiceItem
            {
                Description = line.Name,
                Quantity = line.Quantity,
                UnitPrice = line.Price,
                TaxRate = line.TaxRate,
                Discount = line.DiscountPerUnit ? line.Discount * line.Quantity : line.Discount
            }).ToList()
        };
        db.Invoices.Add(invoice);
        db.SaveChanges();
        return invoice.Id;
    }

    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, out var number) ? number : 0m;
}
