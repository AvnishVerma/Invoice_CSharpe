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
    // Performs the export csv action for this screen or workflow.
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

    // Performs the import csv action for this screen or workflow.
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
        var allowed = kind == "Customer" ? CustomerCsvHeaders : ProductCsvHeaders;
        var required = kind == "Customer" ? new[] { "name" } : ["name", "price"];
        foreach (var column in required)
        {
            if (!header.Contains(column))
            {
                Status = $"CSV is missing required column '{column}'.";
                return 0;
            }
        }
        var unknown = header.FirstOrDefault(column => !allowed.Contains(column));
        if (unknown != null)
        {
            Status = $"Unknown CSV column '{unknown}'. Allowed columns: {string.Join(", ", allowed)}.";
            return 0;
        }

        var dataRows = rows.Skip(1).ToArray();
        var maxRows = kind == "Customer" ? 200 : 500;
        if (dataRows.Length > maxRows)
        {
            Status = $"CSV contains {dataRows.Length} rows. Maximum allowed is {maxRows}.";
            return 0;
        }

        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();
        foreach (var (row, rowIndex) in dataRows.Select((row, index) => (row, index + 2)))
        {
            var fields = kind == "Customer" ? FormCatalog.Customer() : FormCatalog.Product();
            var byLabel = fields.ToDictionary(f => f.Label);
            string Get(string column)
            {
                var index = Array.IndexOf(header, column);
                return index >= 0 && index < row.Length ? row[index].Trim() : "";
            }

            var name = Get("name");
            if (string.IsNullOrWhiteSpace(name)) { errors.Add($"row {rowIndex}: missing name"); skipped++; continue; }
            byLabel["Name"].Value = name;

            UiRecord? duplicate = null;
            if (kind == "Customer")
            {
                duplicate = FindCustomerDuplicate(Get("email"), Get("phone"), name);
                SetImportField(byLabel, "Email", Get("email"));
                SetImportField(byLabel, "Phone", Get("phone"));
                SetImportField(byLabel, "Address", Get("address"));
                SetImportField(byLabel, "Business Name", Get("business_name"));
                SetImportField(byLabel, "GST / VAT Number", FirstNonEmpty(Get("tax_number"), Get("gstin"), Get("gst"), Get("gst_vat_number")));
            }
            else
            {
                var priceText = Get("price");
                if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
                { errors.Add($"row {rowIndex}: invalid price '{priceText}'"); skipped++; continue; }
                duplicate = Products.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                SetImportField(byLabel, "Sale Price", price.ToString("0.##", CultureInfo.InvariantCulture));
                SetImportField(byLabel, "HSN/SAC", FirstNonEmpty(Get("hsn_code"), Get("hsncode"), Get("hsn"), Get("hsn_sac")));
                SetImportField(byLabel, "Description", Get("description"));
                SetImportField(byLabel, "Tax (%)", ClampDecimal(Get("tax_rate"), 0, 100).ToString("0.##", CultureInfo.InvariantCulture));
                SetImportField(byLabel, "Stock", Math.Max(0, ParseDecimalInvariant(Get("stock"))).ToString("0.###", CultureInfo.InvariantCulture));
                SetImportField(byLabel, "Type", NormalizeProductType(Get("type")));
                SetImportField(byLabel, "Default Discount", Math.Max(0, ParseDecimalInvariant(Get("default_discount"))).ToString("0.##", CultureInfo.InvariantCulture));
                SetImportField(byLabel, "Purchase Price", Math.Max(0, ParseDecimalInvariant(Get("purchase_price"))).ToString("0.##", CultureInfo.InvariantCulture));
                SetImportField(byLabel, "Alias Name (for invoice PDF)", Get("alias_name"));
                SetImportField(byLabel, "Unit", Get("unit"));
                SetImportToggle(byLabel, "Unlimited stock", Get("unlimited_stock"));
                SetImportToggle(byLabel, "Price includes tax", Get("price_includes_tax"));
                SetImportField(byLabel, "Storage Location", Get("storage_location"));
                SetImportField(byLabel, "Container Number", Get("container_number"));
                SetImportField(byLabel, "Batch Number", Get("batch_number"));
                SetImportField(byLabel, "Expiry Date", Get("expiry_date"));
                SetImportField(byLabel, "Manufacture Date", Get("manufacture_date"));
                SetImportField(byLabel, "Supplier Name", Get("supplier_name"));
                SetImportField(byLabel, "SKU Code", Get("sku_code"));
                SetImportField(byLabel, "Notes", JoinNotes(Get("notes"), Get("manufacture_name")));
            }

            if (SaveRecord(kind, fields, duplicate))
            {
                if (duplicate == null) imported++; else updated++;
            }
            else skipped++;
        }

        var parts = new List<string>();
        if (imported > 0) parts.Add($"imported {imported}");
        if (updated > 0) parts.Add($"updated {updated}");
        if (skipped > 0) parts.Add($"skipped {skipped}");
        Status = parts.Count == 0 ? $"No {kind.ToLowerInvariant()}s were imported." : $"CSV import complete: {string.Join(", ", parts)} {kind.ToLowerInvariant()} record{(imported + updated == 1 ? "" : "s")}.";
        if (errors.Count > 0) Status += " " + string.Join("; ", errors.Take(3)) + (errors.Count > 3 ? "; …" : "");
        return imported + updated;
    }

    private static readonly string[] CustomerCsvHeaders = ["name", "email", "phone", "address", "business_name", "tax_number", "gstin", "gst", "gst_vat_number"];
    private static readonly string[] ProductCsvHeaders = ["name", "hsn_code", "hsncode", "hsn", "hsn_sac", "description", "price", "tax_rate", "stock", "type", "default_discount", "purchase_price", "alias_name", "unit", "unlimited_stock", "price_includes_tax", "storage_location", "container_number", "batch_number", "expiry_date", "manufacture_date", "manufacture_name", "supplier_name", "sku_code", "notes"];

    // Performs the find customer duplicate action for this screen or workflow.
    private UiRecord? FindCustomerDuplicate(string email, string phone, string name)
    {
        return Customers.FirstOrDefault(c =>
            (!string.IsNullOrWhiteSpace(email) && c["Email"].Equals(email, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(phone) && c["Phone"].Equals(phone, StringComparison.OrdinalIgnoreCase))
            || c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // Performs the set import field action for this screen or workflow.
    private static void SetImportField(Dictionary<string, FormField> fields, string label, string value)
    {
        if (!fields.TryGetValue(label, out var field) || string.IsNullOrWhiteSpace(value)) return;
        field.Value = value.Trim();
        field.IsChecked = ParseCsvBool(field.Value);
    }

    // Performs the set import toggle action for this screen or workflow.
    private static void SetImportToggle(Dictionary<string, FormField> fields, string label, string value)
    {
        if (!fields.TryGetValue(label, out var field) || string.IsNullOrWhiteSpace(value)) return;
        field.IsChecked = ParseCsvBool(value);
        field.Value = field.IsChecked ? "true" : "false";
    }

    // Performs the parse csv bool action for this screen or workflow.
    private static bool ParseCsvBool(string value) => value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    // Performs the parse decimal invariant action for this screen or workflow.
    private static decimal ParseDecimalInvariant(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;
    // Performs the clamp decimal action for this screen or workflow.
    private static decimal ClampDecimal(string value, decimal min, decimal max) => Math.Min(max, Math.Max(min, ParseDecimalInvariant(value)));
    // Performs the normalize product type action for this screen or workflow.
    private static string NormalizeProductType(string value) => value.Trim().Equals("service", StringComparison.OrdinalIgnoreCase) ? "Service" : "Product";
    // Performs the first non empty action for this screen or workflow.
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";
    // Performs the join notes action for this screen or workflow.
    private static string JoinNotes(string notes, string manufactureName) => string.IsNullOrWhiteSpace(manufactureName) ? notes : string.IsNullOrWhiteSpace(notes) ? $"Manufacturer: {manufactureName}" : notes + Environment.NewLine + $"Manufacturer: {manufactureName}";




    // Performs the create database backup action for this screen or workflow.
    public byte[] CreateDatabaseBackup()
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            Status = "Database file backup is not available.";
            return [];
        }

        var backupPath = Path.Combine(Path.GetTempPath(), $"ledgernest-backup-{Guid.NewGuid():N}.invoicedb");
        try
        {
            if (dbFactory != null)
            {
                using var db = dbFactory.CreateDbContext();
                db.EnsureCurrentSchema();
            }

            using var source = new SqliteConnection($"Data Source={databasePath}");
            using var destination = new SqliteConnection($"Data Source={backupPath}");
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            Status = "Database backup created successfully.";
            return File.ReadAllBytes(backupPath);
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    // Performs the restore database backup action for this screen or workflow.
    public bool RestoreDatabaseBackup(byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || bytes.Length == 0)
        {
            Status = "Database backup is empty or unsupported.";
            return false;
        }

        string? cleanupWarning;
        try
        {
            cleanupWarning = DatabaseBackupRestore.Restore(bytes, databasePath);
        }
        catch (Exception ex)
        {
            Status = $"Database restore failed: {ex.Message}";
            return false;
        }

        // The database copy has committed; a presentation failure must not suggest retrying it.
        SetSession(null, "", false);
        try
        {
            ReloadFromDatabase();
            Status = "Database backup restored successfully.";
        }
        catch (Exception)
        {
            Status = "Database backup restored, but the workspace could not reload. Restart LedgerNest before continuing.";
        }
        if (cleanupWarning != null) Status += " " + cleanupWarning;
        return true;
    }

    // Performs the create json backup action for this screen or workflow.
    public string CreateJsonBackup()
    {
        if (dbFactory == null)
        {
            Status = "Backup storage is not available.";
            return "";
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var backup = new JsonObject
        {
            ["customers"] = JsonSerializer.SerializeToNode(db.Customers.AsNoTracking().OrderBy(c => c.Id).ToArray()),
            ["products"] = JsonSerializer.SerializeToNode(db.Products.AsNoTracking().OrderBy(p => p.Id).ToArray()),
            ["company_info"] = JsonSerializer.SerializeToNode(db.CompanyInfos.AsNoTracking().OrderBy(c => c.Id).ToArray()),
            ["settings"] = JsonSerializer.SerializeToNode(db.Settings.AsNoTracking().OrderBy(s => s.Key).ToArray()),
            ["invoices"] = JsonSerializer.SerializeToNode(db.Invoices.AsNoTracking().OrderBy(i => i.Id).Select(i => new InvoiceBackupRow(i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId, i.Status, i.SubTotal, i.TaxTotal, i.DiscountTotal, i.GrandTotal, i.PaidAmount, i.Type, i.DeletedAt, i.CustomerName, i.Snapshot)).ToArray()),
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

    // Performs the restore json backup action for this screen or workflow.
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
            backup = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("Backup must be a JSON object.");
            if (backup.ContainsKey("_metadata"))
            {
                if (backup["_metadata"] is not JsonObject metadata || metadata["version"] is not JsonValue version
                    || !version.TryGetValue<string>(out var value) || value != "1.0")
                    throw new JsonException("Unsupported or missing backup version.");
            }
            ValidateJsonBackup(backup);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException)
        {
            Status = $"Backup file is corrupted or invalid: {ex.Message}";
            return false;
        }

        try
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var tx = db.Database.BeginTransaction();
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
                    Type = row.Type ?? "Invoice",
                    DeletedAt = row.DeletedAt,
                    CustomerName = row.CustomerName ?? "",
                    Snapshot = row.Snapshot,
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

        try
        {
            ReloadFromDatabase();
            Status = "Backup restored successfully.";
        }
        catch (Exception)
        {
            SetSession(null, "", false);
            Status = "Backup restored, but the workspace could not reload. Restart LedgerNest before continuing.";
        }
        return true;
    }

}
