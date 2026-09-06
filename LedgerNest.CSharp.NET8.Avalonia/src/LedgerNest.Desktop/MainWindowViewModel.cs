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
    public ObservableCollection<UiRecord> Payments { get; } = [];
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
        LoadPersistedSettings();
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

    public bool SaveSettings(string name)
    {
        if (!Settings.TryGetValue(name, out var sections)) return false;
        var fields = sections.SelectMany(s => s.Fields).ToArray();
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;

        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.Database.EnsureCreated();
            foreach (var section in sections)
            {
                foreach (var field in section.Fields)
                {
                    SetSetting(db, SettingKey(name, section.Title, field.Label), field.Kind == "toggle" ? field.IsChecked.ToString() : field.Value.Trim());
                }
            }

            if (name == "Company Info") SaveCompanyInfo(db, sections);
            db.SaveChanges();
        }

        Status = $"{name} saved.";
        return true;
    }


    public string ExportCsv(string kind, IEnumerable<UiRecord>? records = null)
    {
        var headers = CsvHeaders(kind);
        var source = (records ?? RecordsForKind(kind)).ToArray();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var record in source) builder.AppendLine(string.Join(",", headers.Select(header => EscapeCsv(CsvValue(kind, record, header)))));
        Status = $"Exported {source.Length} {kind.ToLowerInvariant()} record{(source.Length == 1 ? "" : "s")}.";
        return builder.ToString();
    }

    public int ImportCsv(string kind, string csvText)
    {
        if (kind is not ("Customer" or "Product"))
        {
            Status = $"CSV import is not available for {kind.ToLowerInvariant()}s yet.";
            return 0;
        }

        var rows = ParseCsv(csvText).Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell))).ToArray();
        if (rows.Length == 0)
        {
            Status = "The CSV file is empty.";
            return 0;
        }

        var header = rows[0].Select(NormalizeCsvHeader).ToArray();
        var imported = 0;
        foreach (var row in rows.Skip(1))
        {
            var fields = kind == "Customer" ? FormCatalog.Customer() : FormCatalog.Product();
            foreach (var field in fields)
            {
                var value = CsvFieldValue(kind, field.Label, header, row);
                if (value == null) continue;
                field.Value = value;
                field.IsChecked = bool.TryParse(value, out var checkedValue) && checkedValue;
            }

            if (SaveRecord(kind, fields)) imported++;
        }

        Status = imported == 0 ? $"No {kind.ToLowerInvariant()}s were imported." : $"Imported {imported} {kind.ToLowerInvariant()} record{(imported == 1 ? "" : "s")}.";
        return imported;
    }


    public string CreateJsonBackup()
    {
        if (dbFactory == null)
        {
            Status = "Backup storage is not available.";
            return "";
        }

        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
        var backup = new JsonObject
        {
            ["customers"] = JsonSerializer.SerializeToNode(db.Customers.AsNoTracking().OrderBy(c => c.Id).ToArray()),
            ["products"] = JsonSerializer.SerializeToNode(db.Products.AsNoTracking().OrderBy(p => p.Id).ToArray()),
            ["company_info"] = JsonSerializer.SerializeToNode(db.CompanyInfos.AsNoTracking().OrderBy(c => c.Id).ToArray()),
            ["settings"] = JsonSerializer.SerializeToNode(db.Settings.AsNoTracking().OrderBy(s => s.Key).ToArray()),
            ["invoices"] = JsonSerializer.SerializeToNode(db.Invoices.AsNoTracking().OrderBy(i => i.Id).Select(i => new InvoiceBackupRow(i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId, i.Status, i.SubTotal, i.TaxTotal, i.DiscountTotal, i.GrandTotal, i.PaidAmount)).ToArray()),
            ["invoice_items"] = JsonSerializer.SerializeToNode(db.InvoiceItems.AsNoTracking().OrderBy(i => i.Id).ToArray()),
            ["invoice_payments"] = JsonSerializer.SerializeToNode(db.Payments.AsNoTracking().OrderBy(p => p.Id).ToArray()),
            ["_metadata"] = new JsonObject
            {
                ["created_at"] = DateTime.UtcNow.ToString("O"),
                ["version"] = "1.0",
                ["app_name"] = Branding.Name,
                ["backup_type"] = "json_export",
                ["record_count"] = 7
            }
        };
        Status = "Backup created successfully.";
        return backup.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public bool RestoreJsonBackup(string json)
    {
        if (dbFactory == null)
        {
            Status = "Backup storage is not available.";
            return false;
        }

        JsonObject backup;
        try
        {
            backup = JsonNode.Parse(json)?.AsObject() ?? throw new JsonException("Backup is empty.");
            var version = backup["_metadata"]?["version"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(version) && version != "1.0")
            {
                Status = $"Incompatible backup version: {version}.";
                return false;
            }
        }
        catch (JsonException ex)
        {
            Status = $"Backup file is corrupted or invalid: {ex.Message}";
            return false;
        }

        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
        using var tx = db.Database.BeginTransaction();
        try
        {
            db.Payments.RemoveRange(db.Payments);
            db.InvoiceItems.RemoveRange(db.InvoiceItems);
            db.Invoices.RemoveRange(db.Invoices);
            db.Settings.RemoveRange(db.Settings);
            db.CompanyInfos.RemoveRange(db.CompanyInfos);
            db.Products.RemoveRange(db.Products);
            db.Customers.RemoveRange(db.Customers);
            db.SaveChanges();

            AddRange(db.Customers, backup, "customers");
            AddRange(db.Products, backup, "products");
            AddRange(db.CompanyInfos, backup, "company_info");
            AddRange(db.Settings, backup, "settings");
            foreach (var row in ReadRows<InvoiceBackupRow>(backup, "invoices"))
            {
                db.Invoices.Add(new Invoice
                {
                    Id = row.Id,
                    InvoiceNumber = row.InvoiceNumber,
                    InvoiceDate = row.InvoiceDate,
                    CustomerId = row.CustomerId,
                    Status = row.Status,
                    SubTotal = row.SubTotal,
                    TaxTotal = row.TaxTotal,
                    DiscountTotal = row.DiscountTotal,
                    GrandTotal = row.GrandTotal,
                    PaidAmount = row.PaidAmount
                });
            }
            AddRange(db.InvoiceItems, backup, "invoice_items");
            AddRange(db.Payments, backup, "invoice_payments");
            db.SaveChanges();
            tx.Commit();
        }
        catch (Exception ex)
        {
            Status = $"Restore failed: {ex.Message}";
            return false;
        }

        ReloadFromDatabase();
        Status = "Backup restored successfully.";
        return true;
    }

    private void ReloadFromDatabase()
    {
        Customers.Clear(); Products.Clear(); Users.Clear(); Invoices.Clear(); Payments.Clear();
        LoadPersistedRecords();
        LoadPersistedSettings();
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
                    ["HSN/SAC"] = product.HsnCode ?? "",
                    ["Description"] = product.Description ?? "",
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
                    ["Paid"] = invoice.PaidAmount.ToString("0.00"),
                    ["Outstanding"] = invoice.BalanceAmount.ToString("0.00"),
                    ["Status"] = invoice.Status
                }
            });
        }

        foreach (var payment in db.Payments.AsNoTracking().OrderBy(p => p.PaymentDate).ThenBy(p => p.Id))
        {
            var invoice = Invoices.FirstOrDefault(i => i.SourceId == payment.InvoiceId);
            Payments.Add(new UiRecord
            {
                SourceId = payment.Id,
                Values = new()
                {
                    ["Name"] = NextReceiptNumber(invoice?.Name ?? payment.InvoiceId.ToString(), Payments.Where(p => p["InvoiceId"] == payment.InvoiceId.ToString()).Select(p => p.Name).ToArray()),
                    ["InvoiceId"] = payment.InvoiceId.ToString(),
                    ["Invoice"] = invoice?.Name ?? payment.InvoiceId.ToString(),
                    ["Date"] = payment.PaymentDate.ToString("yyyy-MM-dd"),
                    ["Amount"] = payment.Amount.ToString("0.00"),
                    ["Method"] = payment.Method,
                    ["Reference"] = payment.Reference ?? ""
                }
            });
        }
    }

    private void LoadPersistedSettings()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();

        var settings = db.Settings.AsNoTracking().ToDictionary(s => s.Key, s => s.Value);
        foreach (var (name, sections) in Settings)
        {
            foreach (var section in sections)
            {
                foreach (var field in section.Fields)
                {
                    if (!settings.TryGetValue(SettingKey(name, section.Title, field.Label), out var value)) continue;
                    field.Value = value;
                    field.IsChecked = bool.TryParse(value, out var checkedValue) && checkedValue;
                }
            }
        }

        var company = db.CompanyInfos.AsNoTracking().OrderBy(c => c.Id).FirstOrDefault();
        if (company != null && Settings.TryGetValue("Company Info", out var companySections))
        {
            var companyFields = companySections[1].Fields.ToDictionary(f => f.Label);
            SetField(companyFields, "Company Name", company.Name);
            SetField(companyFields, "Phone", company.Phone);
            SetField(companyFields, "Email", company.Email);
            SetField(companyFields, "GSTIN", company.GstNumber);
            SetField(companyFields, "Address", company.Address);
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
            product.HsnCode = values.GetValueOrDefault("HSN/SAC");
            product.Description = values.GetValueOrDefault("Description");
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

    public bool ApplyPayment(UiRecord invoiceRecord, FormField[] fields)
    {
        if (dbFactory == null)
        {
            Status = "Payment storage is not available.";
            return false;
        }
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        var amount = fields[0].Number;
        if (amount <= 0)
        {
            fields[0].Error = "Amount must be greater than zero.";
            return false;
        }

        using var db = dbFactory.CreateDbContext();
        db.Database.EnsureCreated();
        var invoice = db.Invoices.Include(i => i.Items).FirstOrDefault(i => i.Id == invoiceRecord.SourceId);
        if (invoice == null)
        {
            Status = "Invoice was not found in the database.";
            return false;
        }

        var previousPaid = db.Payments
            .Where(p => p.InvoiceId == invoice.Id)
            .Select(p => p.Amount)
            .AsEnumerable()
            .Sum();
        var outstanding = Math.Max(0m, invoice.GrandTotal - previousPaid);
        if (amount > outstanding) amount = outstanding;
        if (amount <= 0)
        {
            fields[0].Error = "Invoice is already paid.";
            return false;
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            PaymentDate = DateTime.TryParse(fields[1].Value, out var date) ? date : DateTime.Today,
            Amount = amount,
            Method = fields[2].Value,
            Reference = fields[4].Value
        };
        db.Payments.Add(payment);
        invoice.PaidAmount = previousPaid + amount;
        invoice.Status = invoice.PaidAmount >= invoice.GrandTotal ? "Paid" : invoice.PaidAmount > 0 ? "Partial" : "Unpaid";
        db.SaveChanges();

        var receiptNumber = NextReceiptNumber(invoice.InvoiceNumber, Payments.Where(p => p["InvoiceId"] == invoice.Id.ToString()).Select(p => p.Name).ToArray());
        Payments.Add(new UiRecord
        {
            SourceId = payment.Id,
            Values = new()
            {
                ["Name"] = receiptNumber,
                ["InvoiceId"] = invoice.Id.ToString(),
                ["Invoice"] = invoice.InvoiceNumber,
                ["Date"] = payment.PaymentDate.ToString("yyyy-MM-dd"),
                ["Amount"] = payment.Amount.ToString("0.00"),
                ["Method"] = payment.Method,
                ["Reference"] = payment.Reference ?? ""
            }
        });

        invoiceRecord.Values["Paid"] = invoice.PaidAmount.ToString("0.00");
        invoiceRecord.Values["Outstanding"] = invoice.BalanceAmount.ToString("0.00");
        invoiceRecord.Values["Status"] = invoice.Status;
        Status = $"Payment saved: {receiptNumber}.";
        return true;
    }

    public IEnumerable<UiRecord> PaymentsFor(UiRecord invoice) => Payments.Where(p => p["InvoiceId"] == invoice.SourceId.ToString());

    private static string NextReceiptNumber(string invoiceNumber, IEnumerable<string?> existingReceiptNumbers)
    {
        var maxSuffix = 0;
        foreach (var receiptNumber in existingReceiptNumbers)
        {
            var marker = receiptNumber?.LastIndexOf("-R", StringComparison.Ordinal);
            if (marker is null or < 0) continue;
            if (int.TryParse(receiptNumber![(marker.Value + 2)..], out var suffix) && suffix > maxSuffix) maxSuffix = suffix;
        }
        return $"{invoiceNumber}-R{maxSuffix + 1}";
    }



    private static void AddRange<T>(DbSet<T> set, JsonObject backup, string table) where T : class
    {
        foreach (var row in ReadRows<T>(backup, table)) set.Add(row);
    }

    private static T[] ReadRows<T>(JsonObject backup, string table)
    {
        if (backup[table] is not { } rows) return [];
        return rows.Deserialize<T[]>() ?? [];
    }

    private sealed record InvoiceBackupRow(int Id, string InvoiceNumber, DateTime InvoiceDate, int? CustomerId, string Status, decimal SubTotal, decimal TaxTotal, decimal DiscountTotal, decimal GrandTotal, decimal PaidAmount);

    private IEnumerable<UiRecord> RecordsForKind(string kind) => kind switch
    {
        "Customer" => Customers,
        "Product" => Products,
        "User" => Users,
        "Invoice" or "Quotation" or "Receipt" => Invoices.Where(r => r["Type"] == kind),
        _ => []
    };

    private static string[] CsvHeaders(string kind) => kind switch
    {
        "Customer" => ["name", "phone", "business_name", "email", "gstin", "address"],
        "Product" => ["name", "price", "stock", "tax_rate", "hsncode", "description"],
        "User" => ["username", "role"],
        _ => ["name", "customer", "date", "items", "total", "status"]
    };

    private static string CsvValue(string kind, UiRecord record, string header) => (kind, header) switch
    {
        ("Customer", "name") => record.Name,
        ("Customer", "phone") => record["Phone"],
        ("Customer", "business_name") => record["Business Name"],
        ("Customer", "email") => record["Email"],
        ("Customer", "gstin") => record["GST / VAT Number"],
        ("Customer", "address") => record["Address"],
        ("Product", "name") => record.Name,
        ("Product", "price") => record["Sale Price"],
        ("Product", "stock") => record["Stock"],
        ("Product", "tax_rate") => record["Tax (%)"],
        ("Product", "hsncode") => record["HSN/SAC"].Length > 0 ? record["HSN/SAC"] : record["SKU Code"],
        ("Product", "description") => record["Description"],
        ("User", "username") => record.Name,
        ("User", "role") => record["Role"],
        (_, "name") => record.Name,
        (_, "customer") => record["Customer"],
        (_, "date") => record["Date"],
        (_, "items") => record["Items"],
        (_, "total") => record["Total"],
        (_, "status") => record["Status"],
        _ => ""
    };

    private static string? CsvFieldValue(string kind, string label, string[] header, string[] row)
    {
        string[] candidates = kind == "Customer" ? label switch
        {
            "Name" => ["name", "customer_name"],
            "Phone" => ["phone", "phone_number", "mobile"],
            "Business Name" => ["business_name", "company", "company_name"],
            "Email" => ["email"],
            "GST / VAT Number" => ["gstin", "gst", "gst_vat_number", "tax_number"],
            "Address" => ["address"],
            _ => []
        } : label switch
        {
            "Name" => ["name", "product_name"],
            "Sale Price" => ["price", "sale_price", "selling_price"],
            "Stock" => ["stock", "quantity", "stock_quantity"],
            "Tax (%)" => ["tax_rate", "tax", "gst", "tax_percent"],
            "HSN/SAC" => ["hsncode", "hsn", "hsn_sac", "sku"],
            "SKU Code" => ["sku", "code"],
            "Description" => ["description"],
            _ => []
        };

        foreach (var candidate in candidates)
        {
            var index = Array.IndexOf(header, candidate);
            if (index >= 0 && index < row.Length) return row[index].Trim();
        }

        return null;
    }

    private static string NormalizeCsvHeader(string value) => value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace(".", "");

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static IReadOnlyList<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n') { row.Add(cell.ToString()); rows.Add(row.ToArray()); row.Clear(); cell.Clear(); }
            else cell.Append(ch);
        }

        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }

    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0m;

    private static string SettingKey(string page, string section, string label) =>
        string.Join(".", page, section, label).ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("%", "percent");

    private static void SetSetting(LedgerNestDbContext db, string key, string value)
    {
        var setting = db.Settings.Find(key);
        if (setting == null) db.Settings.Add(new AppSetting { Key = key, Value = value });
        else setting.Value = value;
    }

    private static void SaveCompanyInfo(LedgerNestDbContext db, FormSection[] sections)
    {
        var fields = sections[1].Fields.ToDictionary(f => f.Label);
        var company = db.CompanyInfos.OrderBy(c => c.Id).FirstOrDefault() ?? new CompanyInfo();
        company.Name = fields.GetValueOrDefault("Company Name")?.Value.Trim() ?? "";
        company.Phone = fields.GetValueOrDefault("Phone")?.Value.Trim();
        company.Email = fields.GetValueOrDefault("Email")?.Value.Trim();
        company.GstNumber = fields.GetValueOrDefault("GSTIN")?.Value.Trim();
        company.Address = fields.GetValueOrDefault("Address")?.Value.Trim();
        if (company.Id == 0) db.CompanyInfos.Add(company);
    }

    private static void SetField(Dictionary<string, FormField> fields, string label, string? value)
    {
        if (!fields.TryGetValue(label, out var field)) return;
        field.Value = value ?? "";
        field.IsChecked = bool.TryParse(field.Value, out var checkedValue) && checkedValue;
    }
}
