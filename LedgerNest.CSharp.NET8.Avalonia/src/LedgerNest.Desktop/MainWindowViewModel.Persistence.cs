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
    private bool isRefreshingPersistedData;

    // Reloads persisted lists once while leaving the current route, search text and filters intact.
    public void RefreshPersistedData()
    {
        if (isRefreshingPersistedData || dbFactory == null) return;
        try
        {
            isRefreshingPersistedData = true;
            Customers.Clear(); Products.Clear(); Users.Clear(); Invoices.Clear(); Payments.Clear(); DeletedRecords.Clear();
            LoadPersistedRecords();
            Status = "Data refreshed.";
        }
        finally
        {
            isRefreshingPersistedData = false;
        }
    }

    // Performs the reload from database action for this screen or workflow.
    private void ReloadFromDatabase()
    {
        Customers.Clear(); Products.Clear(); Users.Clear(); Invoices.Clear(); Payments.Clear(); DeletedRecords.Clear();
        LoadPersistedRecords();
        LoadPersistedSettings();
        LoadThemeMode();
        LoadLanguage();
    }

    // Performs the load persisted records action for this screen or workflow.
    private void LoadPersistedRecords()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();

        foreach (var customer in db.Customers.AsNoTracking().OrderBy(c => c.Name))
        {
            Customers.Add(new UiRecord
            {
                SourceId = customer.Id,
                Values = new()
                {
                    ["Name"] = customer.Name,
                    ["Business Name"] = customer.BusinessName,
                    ["Phone"] = customer.Phone ?? "",
                    ["Email"] = customer.Email ?? "",
                    ["GST / VAT Number"] = customer.GstNumber ?? "",
                    ["Address"] = customer.Address ?? ""
                    ,["Customer ID"] = customer.CustomerCode ?? ""
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
                    ["Type"] = product.Type,
                    ["Alias Name (for invoice PDF)"] = product.AliasName,
                    ["Default Discount"] = product.DefaultDiscount.ToString(),
                    ["Price includes tax"] = product.PriceIncludesTax.ToString(),
                    ["Unlimited stock"] = product.UnlimitedStock.ToString(),
                    ["Unit"] = product.Unit,
                    ["Custom unit"] = product.CustomUnit,
                    ["Storage Location"] = product.StorageLocation,
                    ["Container Number"] = product.ContainerNumber,
                    ["Batch Number"] = product.BatchNumber,
                    ["Expiry Date"] = product.ExpiryDate,
                    ["Manufacture Date"] = product.ManufactureDate,
                    ["Manufacturer Name"] = product.ManufacturerName,
                    ["Supplier Name"] = product.SupplierName,
                    ["Notes"] = product.Notes,
                    ["Name"] = product.Name,
                    ["SKU Code"] = product.Code ?? "",
                    ["HSN/SAC"] = product.HsnCode ?? "",
                    ["Description"] = product.Description ?? "",
                    ["Sale Price"] = product.SalePrice.ToString("0.##"),
                    ["Purchase Price"] = product.PurchasePrice.ToString("0.##"),
                    ["Tax (%)"] = product.TaxRate.ToString("0.##"),
                    ["Stock"] = product.StockQuantity.ToString("0.###")
                    ,["Product ID"] = product.ProductCode ?? ""
                    ,["Barcode"] = product.Barcode ?? ""
                }
            });
        }

        EnsureDefaultAdmin(db);

        foreach (var user in db.Users.AsNoTracking().OrderBy(u => u.Username))
        {
            Users.Add(new UiRecord
            {
                SourceId = user.Id,
                Values = new()
                {
                    ["Username"] = user.Username,
                    ["Role"] = user.Role
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
                    ["Customer"] = string.IsNullOrEmpty(invoice.CustomerName) ? Customers.FirstOrDefault(c => c.SourceId == invoice.CustomerId)?.Name ?? "" : invoice.CustomerName,
                    ["Type"] = invoice.Type,
                    ["Currency"] = invoice.Snapshot?.Currency ?? InvoiceSetting("Currency").Value,
                    ["Date"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["Due Date"] = invoice.Snapshot?.DueDate?.ToString("yyyy-MM-dd") ?? "",
                    ["Items"] = invoice.Items.Count.ToString(),
                    ["Total"] = invoice.GrandTotal.ToString("0.00"),
                    ["Tax"] = invoice.TaxTotal.ToString(CultureInfo.InvariantCulture),
                    ["Paid"] = invoice.PaidAmount.ToString("0.00"),
                    ["Outstanding"] = invoice.Status == "Voided" ? "0.00" : invoice.BalanceAmount.ToString("0.00"),
                    ["Status"] = invoice.Status
                }
            });
        }

        var deletedIds = db.Invoices.AsNoTracking().Where(i => i.DeletedAt != null).Select(i => i.Id).ToHashSet();
        foreach (var record in Invoices.Where(i => deletedIds.Contains(i.SourceId))) DeletedRecords.Add(record.Id);

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

    // Performs the load persisted settings action for this screen or workflow.
    private void LoadPersistedSettings()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();

        var settings = db.Settings.AsNoTracking().ToDictionary(s => s.Key, s => s.Value);
        UpdateManifestUrl = settings.GetValueOrDefault("updates.manifest_url", "");
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

        LoadInvoiceCustomDefinitions();
        var businessType = Settings["Company Info"].Single(section => section.Title == "BUSINESS TYPE").Fields[0];
        businessType.Value = businessType.Value switch
        {
            "Products & Services" => "Both",
            "Products" => "Product",
            "Services" => "Service",
            _ => businessType.Value
        };

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

    // Performs the save record to database action for this screen or workflow.
    private int SaveRecordToDatabase(string kind, Dictionary<string, string> values, int sourceId)
    {
        if (dbFactory == null) return sourceId;
        if (kind is "Customer" or "Product")
        {
            var codeKey = kind + " ID";
            try
            {
                return new AutoCodeGenerator(dbFactory).SaveRecord(kind, values.GetValueOrDefault(codeKey, ""), sourceId, CurrentUsername ?? "", (context, code) =>
                {
                    values[codeKey] = code;
                    return SaveRecordInContext(context, kind, values, sourceId);
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or DbUpdateException or SqliteException or UnauthorizedAccessException)
            { Status = ex.Message; return -1; }
        }
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return SaveRecordInContext(db, kind, values, sourceId);
    }

    private int SaveRecordInContext(LedgerNestDbContext db, string kind, Dictionary<string, string> values, int sourceId)
    {

        if (kind == "Customer")
        {
            var customer = sourceId > 0 ? db.Customers.Find(sourceId) : new Customer();
            if (customer == null) { Status = "Customer no longer exists."; return -1; }
            var phone = values.GetValueOrDefault("Phone", "").Trim();
            if (phone.Length > 0 && db.Customers.AsNoTracking().Where(item => item.Id != sourceId).Select(item => item.Phone).AsEnumerable().Any(value => CustomerIdentityRules.Equal(value, phone)))
            { Status = "Phone number is already assigned to another customer. Select that customer instead."; return -1; }
            var customerCode = values.GetValueOrDefault("Customer ID", "").Trim();
            if (customer.Id > 0 && customerCode.Length == 0) customerCode = customer.CustomerCode ?? "";
            if (customerCode.Length > 0 && db.Customers.Any(item => item.Id != sourceId && item.CustomerCode == customerCode)) { Status = "Customer ID is already in use."; return -1; }
            if (customerCode.Length > 0) customer.CustomerCode = customerCode;
            customer.Name = values.GetValueOrDefault("Name", "");
            customer.BusinessName = values.GetValueOrDefault("Business Name", "");
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
            var product = sourceId > 0 ? db.Products.Find(sourceId) : new Product();
            if (product == null) { Status = "Product no longer exists."; return -1; }
            var productCode = values.GetValueOrDefault("Product ID", "").Trim();
            if (productCode.Length > 0 && db.Products.Any(item => item.Id != sourceId && item.ProductCode == productCode)) { Status = "Product ID is already in use."; return -1; }
            if (productCode.Length > 0) product.ProductCode = productCode;
            product.Barcode = values.GetValueOrDefault("Barcode");
            product.Name = values.GetValueOrDefault("Name", "");
            product.Code = values.GetValueOrDefault("SKU Code");
            product.HsnCode = values.GetValueOrDefault("HSN/SAC");
            product.Description = values.GetValueOrDefault("Description");
            product.SalePrice = ParseDecimal(values.GetValueOrDefault("Sale Price"));
            product.PurchasePrice = ParseDecimal(values.GetValueOrDefault("Purchase Price"));
            product.TaxRate = ParseDecimal(values.GetValueOrDefault("Tax (%)"));
            product.StockQuantity = ParseDecimal(values.GetValueOrDefault("Stock"));
            product.Type = values.GetValueOrDefault("Type", "Product");
            product.AliasName = values.GetValueOrDefault("Alias Name (for invoice PDF)", "");
            product.DefaultDiscount = ParseDecimal(values.GetValueOrDefault("Default Discount"));
            product.PriceIncludesTax = bool.TryParse(values.GetValueOrDefault("Price includes tax"), out var priceIncludesTax) && priceIncludesTax;
            product.UnlimitedStock = bool.TryParse(values.GetValueOrDefault("Unlimited stock"), out var unlimitedStock) && unlimitedStock;
            product.Unit = values.GetValueOrDefault("Unit", "None");
            product.BaseUnitId = db.Units.Where(unit => unit.Code == product.Unit).Select(unit => (int?)unit.Id).SingleOrDefault();
            product.CustomUnit = values.GetValueOrDefault("Custom unit", "");
            product.StorageLocation = values.GetValueOrDefault("Storage Location", "");
            product.ContainerNumber = values.GetValueOrDefault("Container Number", "");
            product.BatchNumber = values.GetValueOrDefault("Batch Number", "");
            product.ExpiryDate = values.GetValueOrDefault("Expiry Date", "");
            product.ManufactureDate = values.GetValueOrDefault("Manufacture Date", "");
            product.ManufacturerName = values.GetValueOrDefault("Manufacturer Name", "");
            product.SupplierName = values.GetValueOrDefault("Supplier Name", "");
            product.Notes = values.GetValueOrDefault("Notes", "");
            if (product.Id == 0) db.Products.Add(product);
            db.SaveChanges();
            return product.Id;
        }


        if (kind == "User")
        {
            using var transaction = db.Database.BeginTransaction();
            var user = sourceId > 0 ? db.Users.Find(sourceId) : new AppUser();
            if (user == null) { Status = "User no longer exists."; return -1; }
            var username = values.GetValueOrDefault("Username", "");
            if (db.Users.AsNoTracking().AsEnumerable().Any(u => u.Id != sourceId && u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            { Status = "Username is already in use."; return -1; }
            if (user.Role == "Admin" && values.GetValueOrDefault("Role") != "Admin" && db.Users.Count(u => u.Role == "Admin") <= 1)
            { Status = "The last administrator cannot be demoted."; return -1; }
            user.Username = values.GetValueOrDefault("Username", "");
            user.Role = values.GetValueOrDefault("Role", "User");
            var password = values.GetValueOrDefault("Password", "");
            if (user.Id == 0 || !string.IsNullOrWhiteSpace(password))
            {
                user.Salt = PasswordCredentials.CreateSalt();
                user.PasswordHash = HashPassword(password, user.Salt);
                user.PasswordChanged = true;
            }
            if (user.Id == 0) db.Users.Add(user);
            db.SaveChanges();
            var role = db.Roles.SingleOrDefault(item => item.Name == user.Role);
            if (role == null)
            {
                role = new AppRole { Name = user.Role, IsSystem = user.Role == "Admin" };
                db.Roles.Add(role);
                db.SaveChanges();
            }
            db.UserRoles.RemoveRange(db.UserRoles.Where(item => item.UserId == user.Id));
            db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = role.Id });
            db.SaveChanges();
            transaction.Commit();
            return user.Id;
        }

        return sourceId;
    }

    // Performs the capture invoice snapshot action for this screen or workflow.
    private InvoiceSnapshot CaptureInvoiceSnapshot()
    {
        string Setting(string label) => Settings["Invoice Settings"].SelectMany(s => s.Fields).Single(f => f.Label == label).Value;
        return new InvoiceSnapshot(
            1,
            new InvoiceCustomerSnapshot(InvoiceCustomer[0].Value.Trim(), InvoiceCustomer[1].Value.Trim(),
                InvoiceCustomer[2].Value.Trim(), InvoiceCustomer[3].Value.Trim(),
                InvoiceCustomer[4].Value.Trim(), InvoiceCustomer[5].Value.Trim()),
            DateTime.TryParse(InvoiceDetails[2].Value, out var due) ? due.Date : null,
            InvoiceDetails[3].Value, InvoiceDetails[4].Value.Trim(), HideInvoiceNumber.IsChecked,
            InterState.IsChecked, editingSnapshot?.Currency ?? Setting("Currency"), editingSnapshot?.QuantityLabel ?? Setting("Quantity Column"),
            InvoiceOptions[3].Value, InvoiceOptions[4].Number, InvoiceOptions[0].Value,
            InvoiceOptions[1].Number, InvoiceOptions[2].Value,
            AdditionalCosts.Select(c => new InvoiceAdditionalCost(c[0].Value, c[1].Number)).ToArray())
        {
            LineUnits = Lines.Select(line => line.Unit).ToArray(),
            BankAccount = SelectedInvoiceBank?.Account,
            LinePresentations = Lines.Select(CaptureLinePresentation).ToArray(),
            CustomFields = InvoiceCustomFields.Select(field => new InvoiceCustomFieldValue(field.Id, field.Field.Label, field.Field.Value)).ToArray(),
            LineHsnCodes = Lines.Select(line => historicalLines.TryGetValue(line, out var original)
                ? editingSnapshot?.LineHsnCodes?.ElementAtOrDefault(historicalLines.Keys.ToList().IndexOf(line)) ?? ""
                : Products.FirstOrDefault(product => product.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase))?["HSN/SAC"] ?? "").ToArray()
        };
    }

    // Performs the save invoice to database action for this screen or workflow.
    private int SaveInvoiceToDatabase(Dictionary<string, string> values)
    {
        if (dbFactory == null) return 0;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        using var transaction = db.Database.IsSqlite() ? db.Database.BeginTransaction() : null;
        Invoice? existing = null;
        if (editingDocument != null)
        {
            existing = db.Invoices.Include(i => i.Items).SingleOrDefault(i => i.Id == editingDocument.SourceId);
            if (existing == null || existing.DeletedAt != null || Fingerprint(existing) != editingFingerprint)
            { Status = "Document changed since it was opened. Reopen it before saving."; return -1; }
            InvoiceEditRules.Validate(existing, Totals.Total, db.Payments.Where(payment => payment.InvoiceId == existing.Id).AsEnumerable().Sum(payment => payment.Amount));
            if (values["Type"] != existing.Type)
            { Status = "Changing document type during editing is not supported."; return -1; }
        }
        values["Name"] = existing?.InvoiceNumber ?? ReserveDocumentNumber(db, values["Type"]);
        var customerName = InvoiceCustomer[0].Value.Trim();
        var customerId = ResolveInvoiceCustomerId(db);
        var invoice = new Invoice
        {
            InvoiceNumber = values["Name"],
            Type = values["Type"],
            InvoiceDate = InvoiceEditRules.ResolveDate(InvoiceDetails[1].Value, OrderTime.Value, existing?.InvoiceDate.Kind ?? DateTimeKind.Unspecified),
            CustomerId = customerId,
            CustomerName = customerName,
            Snapshot = CaptureInvoiceSnapshot(),
            Status = values["Status"],
            SubTotal = Totals.Subtotal,
            TaxTotal = Totals.Tax,
            DiscountTotal = Totals.ItemDiscount + Totals.InvoiceDiscount,
            GrandTotal = Totals.Total,
            PaidAmount = existing?.PaidAmount ?? 0,
            Items = Lines.Select(line =>
            {
                var product = Products.FirstOrDefault(p => p.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                return new InvoiceItem
                {
                    ProductId = line.ProductKey.StartsWith("id:", StringComparison.Ordinal) && int.TryParse(line.ProductKey[3..], out var productId) ? productId : null,
                    SellingUnitId = line.SellingUnitId,
                    SellingUnitCode = line.Unit,
                    UnitConversionFactor = line.UnitConversionFactor <= 0 ? 1 : line.UnitConversionFactor,
                    BaseQuantity = line.Quantity * (line.UnitConversionFactor <= 0 ? 1 : line.UnitConversionFactor),
                    Description = line.Name,
                    ProductDescription = historicalLines.TryGetValue(line, out var original) ? original.ProductDescription : product?["Description"] ?? line.Name,
                    Quantity = line.Quantity,
                    UnitPrice = line.Price,
                    ProductPrice = line.Price,
                    PurchasePrice = historicalLines.TryGetValue(line, out var historical) ? historical.PurchasePrice : product == null ? 0m : ParseDecimal(product["Purchase Price"]),
                    TaxRate = line.TaxRate,
                    Discount = line.Discount,
                    ExtraCost = line.ExtraCost,
                    DiscountPerUnit = line.DiscountPerUnit,
                    PriceIncludesTax = line.PriceIncludesTax
                };
            }).ToList()
        };
        if (existing == null) db.Invoices.Add(invoice);
        else
        {
            db.InvoiceItems.RemoveRange(existing.Items);
            existing.Items = invoice.Items;
            existing.CustomerId = invoice.CustomerId;
            existing.CustomerName = invoice.CustomerName;
            existing.InvoiceDate = invoice.InvoiceDate;
            existing.Snapshot = invoice.Snapshot;
            existing.SubTotal = invoice.SubTotal;
            existing.TaxTotal = invoice.TaxTotal;
            existing.DiscountTotal = invoice.DiscountTotal;
            existing.GrandTotal = invoice.GrandTotal;
            if (existing.Type == "Invoice") existing.Status = InvoiceEditRules.PaymentStatus(existing.GrandTotal, existing.PaidAmount);
            invoice = existing;
        }
        if (invoice.Type == "Invoice")
        {
            var priorMovements = db.InventoryTransactions
                .Where(movement => movement.SourceType == "Invoice" && movement.Reference == invoice.InvoiceNumber).ToArray();
            foreach (var movement in priorMovements)
            {
                var priorProduct = db.Products.SingleOrDefault(product => product.Id == movement.ProductId);
                if (priorProduct != null && !priorProduct.UnlimitedStock)
                    priorProduct.StockQuantity -= movement.BaseQuantityChange;
            }
            db.InventoryTransactions.RemoveRange(priorMovements);

            foreach (var item in invoice.Items.Where(item => item.ProductId != null && item.BaseQuantity > 0))
            {
                var product = db.Products.Single(product => product.Id == item.ProductId);
                if (product.Type == "Service" || product.UnlimitedStock) continue;
                // Invoice entry records the real movement even when stock is already
                // negative; replenishment and inventory review can then reconcile it.
                product.StockQuantity = InventoryRules.Apply(product.StockQuantity, -item.BaseQuantity, allowNegative: true);
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = product.Id,
                    TransactionType = "Sale",
                    BaseQuantityChange = -item.BaseQuantity,
                    SourceType = "Invoice",
                    Reference = invoice.InvoiceNumber,
                    Notes = $"{item.Quantity:0.###} {item.SellingUnitCode}",
                    CreatedBy = CurrentUsername ?? "system"
                });
            }
        }
        db.SaveChanges();
        // Compare the normalized persisted graph on the next save, rather than
        // EF's tracked graph after line replacement and relationship fixups.
        var savedFingerprint = editingDocument == null ? null
            : Fingerprint(db.Invoices.AsNoTracking().Include(item => item.Items).Single(item => item.Id == invoice.Id));
        transaction?.Commit();
        values["Paid"] = invoice.PaidAmount.ToString("0.00", CultureInfo.InvariantCulture);
        values["Outstanding"] = invoice.BalanceAmount.ToString("0.00", CultureInfo.InvariantCulture);
        values["Status"] = invoice.Status;
        if (editingDocument != null) editingFingerprint = savedFingerprint;
        return invoice.Id;
    }

    // Performs the apply payment action for this screen or workflow.
    public bool ApplyPayment(UiRecord invoiceRecord, FormField[] fields)
    {
        if (!HasPermission("Invoice", "Update")) { Status = "You do not have permission to record payments."; return false; }
        if (!RequireBusinessLicense()) return false;
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
        db.EnsureCurrentSchema();
        using var paymentTransaction = db.Database.BeginTransaction();
        var invoice = db.Invoices.Include(i => i.Items).FirstOrDefault(i => i.Id == invoiceRecord.SourceId);
        if (invoice == null || invoice.DeletedAt != null)
        {
            Status = "Invoice was not found or is in trash.";
            return false;
        }

        if (InvoiceStatusRules.IsVoided(invoice.Status))
        {
            Status = "Payments cannot be added to a voided invoice.";
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
        invoice.Status = invoice.BalanceAmount == 0 ? "Paid" : invoice.PaidAmount > 0.005m ? "Partial" : "Unpaid";
        db.SaveChanges();
        paymentTransaction.Commit();

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

    // Performs the payments for action for this screen or workflow.
    public IEnumerable<UiRecord> PaymentsFor(UiRecord invoice) => Payments.Where(p => p["InvoiceId"] == invoice.SourceId.ToString());

    // Performs the next receipt number action for this screen or workflow.
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






    // Performs the ensure default admin action for this screen or workflow.
    private static void EnsureDefaultAdmin(LedgerNestDbContext db)
    {
        if (db.Settings.Any(s => s.Key == "auth.initialized")) return;
        SetSetting(db, "auth.initialized", "true");
        if (db.Users.Any()) { db.SaveChanges(); return; }
        var salt = PasswordCredentials.CreateSalt();
        db.Users.Add(new AppUser
        {
            Username = "admin",
            Role = "Admin",
            Salt = salt,
            PasswordHash = HashPassword("admin", salt),
            PasswordChanged = false
        });
        db.SaveChanges();
    }

    // Performs the hash password action for this screen or workflow.
    private static string HashPassword(string password, string salt)
    {
        return PasswordCredentials.Hash(password, salt);
    }

    // Performs the money action for this screen or workflow.
    private string Money(decimal value) => CurrencyDisplay.Format(value, currency: InvoiceSetting("Currency").Value);

    private static void AddRange<T>(DbSet<T> set, JsonObject backup, string table) where T : class
    {
        foreach (var row in ReadRows<T>(backup, table)) set.Add(row);
    }

    private static T[] ReadRows<T>(JsonObject backup, string table)
    {
        if (backup[table] is not JsonArray rows)
            throw new JsonException($"Backup table '{table}' must be an array.");
        var result = rows.Deserialize<T[]>() ?? throw new JsonException($"Backup table '{table}' is invalid.");
        if (result.Any(row => row is null)) throw new JsonException($"Backup table '{table}' contains a null record.");
        return result;
    }

    // Performs the validate json backup action for this screen or workflow.
    private static void ValidateJsonBackup(JsonObject backup)
    {
        var customers = ReadRows<Customer>(backup, "customers");
        var products = ReadRows<Product>(backup, "products");
        var company = ReadRows<CompanyInfo>(backup, "company_info");
        var settings = ReadRows<AppSetting>(backup, "settings");
        var invoices = ReadRows<InvoiceBackupRow>(backup, "invoices");
        var items = ReadRows<InvoiceItem>(backup, "invoice_items");
        var payments = ReadRows<Payment>(backup, "invoice_payments");
        // Performs the ids action for this screen or workflow.
        static HashSet<int> Ids(IEnumerable<int> values)
        {
            var ids = new HashSet<int>();
            foreach (var id in values)
                if (id <= 0 || !ids.Add(id)) throw new InvalidDataException("Backup contains missing or duplicate record IDs.");
            return ids;
        }
        var customerIds = Ids(customers.Select(row => row.Id));
        var productIds = Ids(products.Select(row => row.Id));
        var invoiceIds = Ids(invoices.Select(row => row.Id));
        Ids(company.Select(row => row.Id)); Ids(items.Select(row => row.Id)); Ids(payments.Select(row => row.Id));
        if (settings.Any(row => string.IsNullOrWhiteSpace(row.Key)) || settings.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count() != settings.Length)
            throw new InvalidDataException("Backup contains missing or duplicate setting keys.");
        if (invoices.Any(row => row.CustomerId is int id && !customerIds.Contains(id))
            || items.Any(row => !invoiceIds.Contains(row.InvoiceId) || (row.ProductId is int id && !productIds.Contains(id)))
            || payments.Any(row => !invoiceIds.Contains(row.InvoiceId)))
            throw new InvalidDataException("Backup contains broken record references.");
    }

    private sealed record InvoiceBackupRow(int Id, string InvoiceNumber, DateTime InvoiceDate, int? CustomerId, string Status, decimal SubTotal, decimal TaxTotal, decimal DiscountTotal, decimal GrandTotal, decimal PaidAmount, string? Type = "Invoice", DateTime? DeletedAt = null, string? CustomerName = null, InvoiceSnapshot? Snapshot = null);

    // Performs the records for kind action for this screen or workflow.
    private IEnumerable<UiRecord> RecordsForKind(string kind) => kind switch
    {
        "Customer" => Customers,
        "Product" => Products,
        "User" => Users,
        "Invoice" or "Quotation" or "Receipt" => Invoices.Where(r => r["Type"] == kind && !DeletedRecords.Contains(r.Id)),
        _ => []
    };

    // Performs the csv headers action for this screen or workflow.
    private static string[] CsvHeaders(string kind) => kind switch
    {
        "Customer" => ["name", "phone", "business_name", "email", "gstin", "address"],
        "Product" => ["name", "price", "stock", "tax_rate", "hsncode", "description"],
        "User" => ["username", "role"],
        _ => ["name", "customer", "date", "items", "total", "status"]
    };

    // Performs the csv value action for this screen or workflow.
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

    // Performs the normalize csv header action for this screen or workflow.
    private static string NormalizeCsvHeader(string value) => value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace(".", "");

    // Performs the escape csv action for this screen or workflow.
    private static string EscapeCsv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // Performs the parse csv action for this screen or workflow.
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

    // Performs the parse decimal action for this screen or workflow.
    private static decimal ParseDecimal(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : 0m;

    // Performs the setting key action for this screen or workflow.
    private static string SettingKey(string page, string section, string label) =>
        string.Join(".", page, section, label).ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("%", "percent");

    // Performs the set setting action for this screen or workflow.
    private static void SetSetting(LedgerNestDbContext db, string key, string value)
    {
        var setting = db.Settings.Find(key);
        if (setting == null) db.Settings.Add(new AppSetting { Key = key, Value = value });
        else setting.Value = value;
    }

    // Performs the save company info action for this screen or workflow.
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

    // Performs the set field action for this screen or workflow.
    private static void SetField(Dictionary<string, FormField> fields, string label, string? value)
    {
        if (!fields.TryGetValue(label, out var field)) return;
        field.Value = value ?? "";
        field.IsChecked = bool.TryParse(field.Value, out var checkedValue) && checkedValue;
    }}
