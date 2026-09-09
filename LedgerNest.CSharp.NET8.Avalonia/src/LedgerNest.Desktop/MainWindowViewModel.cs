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

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? dbFactory;
    private readonly string? databasePath;
    public static readonly string[] Routes = ["Dashboard", "New Invoice", "Invoices", "Quotations", "Receipts", "Customers", "Products", "Reports", "Settings"];
    [ObservableProperty] private string title = "Dashboard";
    [ObservableProperty] private bool sidebarExpanded = true;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string themeMode = "Light";
    [ObservableProperty] private string language = "English";
    public HashSet<Guid> DeletedRecords { get; } = [];
    public ObservableCollection<UiRecord> Customers { get; } = [];
    public ObservableCollection<UiRecord> Products { get; } = [];
    public ObservableCollection<UiRecord> Users { get; } = [];
    public ObservableCollection<UiRecord> Invoices { get; } = [];
    public IEnumerable<UiRecord> ActiveInvoices => Invoices.Where(i => i["Type"] == "Invoice" && !DeletedRecords.Contains(i.Id));
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
    public MainWindowViewModel(IDbContextFactory<LedgerNestDbContext>? dbFactory = null, string? databasePath = null)
    {
        this.dbFactory = dbFactory;
        this.databasePath = databasePath;
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
        LoadThemeMode();
        LoadLanguage();
    }
    private void LineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvoiceChanged?.Invoke();
    [RelayCommand] private void Navigate(string route) { if (Routes.Contains(route)) { Title = route; Status = ""; } }
    [RelayCommand] private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;
    public bool SaveRecord(string kind, FormField[] fields, UiRecord? original = null)
    {
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        var records = kind == "Customer" ? Customers : kind == "Product" ? Products : Users;
        var databaseValues = fields.ToDictionary(f => f.Label, f => f.Kind == "toggle" ? f.IsChecked.ToString() : f.Kind == "password" ? f.Value : f.Value.Trim());
        var values = fields.Where(f => f.Kind != "password").ToDictionary(f => f.Label, f => f.Kind == "toggle" ? f.IsChecked.ToString() : f.Value.Trim());
        if (original != null && !records.Contains(original)) { Status = "Record no longer exists in this view."; return false; }
        if (kind == "User")
        {
            if (values.GetValueOrDefault("Role") is not ("Admin" or "User"))
            { Status = "Select a valid user role."; return false; }
            if (dbFactory == null && Users.Any(u => u != original && u.Name.Equals(values["Username"], StringComparison.OrdinalIgnoreCase)))
            { Status = "Username is already in use."; return false; }
            if (dbFactory == null && original?["Role"] == "Admin" && values["Role"] != "Admin" && Users.Count(u => u["Role"] == "Admin") <= 1)
            { Status = "The last administrator cannot be demoted."; return false; }
        }
        var sourceId = SaveRecordToDatabase(kind, databaseValues, original?.SourceId ?? 0);
        if (sourceId < 0) return false;
        var record = new UiRecord { SourceId = sourceId, Values = values };
        if (original != null) records[records.IndexOf(original)] = record; else records.Add(record);
        if (kind == "User" && original != null && CurrentUsername == original.Name)
        {
            SetSession(null, "", false);
        }
        Status = $"{kind} saved.";
        return true;
    }
    public bool DeleteRecord(string kind, UiRecord record)
    {
        var records = kind switch { "Customer" => Customers, "Product" => Products, "User" => Users, _ => null };
        if (records == null || !records.Contains(record)) return false;
        if (dbFactory == null && kind == "User" && record["Role"] == "Admin" && Users.Count(u => u["Role"] == "Admin") <= 1)
        { Status = "The last administrator cannot be deleted."; return false; }
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var transaction = db.Database.BeginTransaction();
            if (kind == "Customer")
            {
                var customer = db.Customers.Find(record.SourceId);
                if (customer == null) return false;
                foreach (var invoice in db.Invoices.Where(i => i.CustomerId == customer.Id))
                {
                    if (string.IsNullOrEmpty(invoice.CustomerName)) invoice.CustomerName = customer.Name;
                    invoice.CustomerId = null;
                }
                db.Customers.Remove(customer);
            }
            else if (kind == "Product")
            {
                var product = db.Products.Find(record.SourceId);
                if (product == null) return false;
                foreach (var item in db.InvoiceItems.Where(i => i.ProductId == product.Id)) item.ProductId = null;
                db.Products.Remove(product);
            }
            else
            {
                var user = db.Users.Find(record.SourceId);
                if (user == null) return false;
                if (user.Role == "Admin" && db.Users.Count(u => u.Role == "Admin") <= 1)
                { Status = "The last administrator cannot be deleted."; return false; }
                db.Users.Remove(user);
            }
            db.SaveChanges();
            transaction.Commit();
        }
        records.Remove(record);
        DeletedRecords.Remove(record.Id);
        if (kind == "User" && CurrentUsername == record.Name)
        {
            SetSession(null, "", false);
        }
        Status = $"{kind} deleted.";
        return true;
    }

    public bool SetDocumentTrash(UiRecord record, bool trashed)
    {
        if (!Invoices.Contains(record)) return false;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            var invoice = db.Invoices.Find(record.SourceId);
            if (invoice == null) { Status = "Document no longer exists."; return false; }
            invoice.DeletedAt = trashed ? DateTime.UtcNow : null;
            db.SaveChanges();
        }
        if (trashed) DeletedRecords.Add(record.Id); else DeletedRecords.Remove(record.Id);
        Status = trashed ? "Document moved to trash." : "Document restored.";
        return true;
    }

    public bool DeleteDocumentPermanently(UiRecord record)
    {
        if (!Invoices.Contains(record) || !DeletedRecords.Contains(record.Id)) return false;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var transaction = db.Database.BeginTransaction();
            var invoice = db.Invoices.Find(record.SourceId);
            if (invoice == null || invoice.DeletedAt == null) return false;
            db.Payments.RemoveRange(db.Payments.Where(p => p.InvoiceId == record.SourceId));
            db.InvoiceItems.RemoveRange(db.InvoiceItems.Where(i => i.InvoiceId == record.SourceId));
            db.Invoices.Remove(invoice);
            db.SaveChanges();
            transaction.Commit();
        }
        foreach (var payment in Payments.Where(p => p["InvoiceId"] == record.SourceId.ToString()).ToArray()) Payments.Remove(payment);
        DeletedRecords.Remove(record.Id);
        Invoices.Remove(record);
        Status = "Document permanently deleted.";
        return true;
    }

    public string PeekNextDocumentNumber(string type)
    {
        ValidateDocumentType(type);
        if (dbFactory == null)
            return NextDocumentNumber(type, Invoices.Where(i => i["Type"] == type).Select(i => i.Name), InvoiceStartingNumber());
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return NextDocumentNumber(db, type);
    }

    private static void ValidateDocumentType(string type)
    {
        if (type is not ("Invoice" or "Quotation" or "Receipt"))
            throw new ArgumentException("Unknown document type.", nameof(type));
    }

    private long InvoiceStartingNumber()
    {
        var field = Settings["Invoice Settings"].SelectMany(s => s.Fields).Single(f => f.Label == "Starting Number");
        return long.TryParse(field.Value, out var start) && start > 0 ? start : 1;
    }

    private string NextDocumentNumber(LedgerNestDbContext db, string type)
    {
        var key = SettingKey("Invoice Settings", "General", "Starting Number");
        var setting = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == key)?.Value;
        var start = long.TryParse(setting, out var parsed) && parsed > 0 ? parsed : InvoiceStartingNumber();
        return NextDocumentNumber(type, db.Invoices.AsNoTracking().Where(i => i.Type == type).Select(i => i.InvoiceNumber).ToArray(), start);
    }

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

    private static string Fingerprint(Invoice invoice) =>
        JsonSerializer.Serialize(new { Invoice = invoice, Items = invoice.Items.OrderBy(i => i.Id).ToArray() });

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
            var line = new InvoiceLineViewModel { Name = item.Description, Price = item.UnitPrice, Quantity = item.Quantity, Discount = item.Discount, DiscountPerUnit = item.DiscountPerUnit, TaxRate = item.TaxRate, PriceIncludesTax = item.PriceIncludesTax, ExtraCost = item.ExtraCost };
            historicalLines.Add(line, item);
            Lines.Add(line);
        }
        NavigateCommand.Execute("New Invoice");
        return true;
    }

    public bool SaveInvoice()
    {
        if (Lines.Count == 0) { Status = "Add at least one item before creating an invoice."; return false; }
        if (Lines.Any(l => string.IsNullOrWhiteSpace(l.Name) || l.Quantity <= 0 || l.Price < 0 || l.TaxRate < 0 || l.Discount < 0))
        { Status = "Check item names, quantities, prices, tax and discounts."; return false; }
        var values = new Dictionary<string, string> {
            ["Name"] = PeekNextDocumentNumber(InvoiceDetails[0].Value), ["Customer"] = InvoiceCustomer[0].Value,
            ["Type"] = InvoiceDetails[0].Value, ["Date"] = InvoiceDetails[1].Value,
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




    public void SetLanguage(string value)
    {
        Language = string.IsNullOrWhiteSpace(value) ? "English" : value;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            SetSetting(db, "appearance.language", Language);
            db.SaveChanges();
        }
        Status = $"Language set to {Language}.";
    }

    private void LoadLanguage()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        Language = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "appearance.language")?.Value ?? "English";
    }

    public void SetThemeMode(string mode)
    {
        ThemeMode = mode is "Dark" or "System" ? mode : "Light";
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            SetSetting(db, "appearance.theme_mode", ThemeMode);
            db.SaveChanges();
        }
        Status = $"Theme set to {ThemeMode}.";
    }

    private void LoadThemeMode()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var setting = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "appearance.theme_mode")?.Value;
        ThemeMode = setting is "Dark" or "System" ? setting : "Light";
    }

    private AppUser? sessionAccount;

    public bool CanAccessWorkspace => dbFactory == null || (CurrentUsername != null && !RequiresPasswordChange);
    public string? CurrentUsername { get; private set; }
    public string CurrentRole { get; private set; } = "";
    public bool RequiresPasswordChange { get; private set; }

    private void SetSession(string? username, string role, bool requiresPasswordChange)
    {
        if (username == null) sessionAccount = null;
        CurrentUsername = username;
        CurrentRole = role;
        RequiresPasswordChange = requiresPasswordChange;
        OnPropertyChanged(nameof(CurrentUsername));
        OnPropertyChanged(nameof(CurrentRole));
        OnPropertyChanged(nameof(RequiresPasswordChange));
        OnPropertyChanged(nameof(CanAccessWorkspace));
    }

    public void SignOut()
    {
        SetSession(null, "", false);
        Status = "Signed out.";
    }

    public bool SignIn(string username, string password)
    {
        SetSession(null, "", false);
        var user = AuthenticateUser(username, password);
        if (user == null) return false;
        sessionAccount = user;
        SetSession(user.Username, user.Role, !user.PasswordChanged);
        return true;
    }

    public bool ValidateSession()
    {
        if (dbFactory == null) return true;
        if (sessionAccount == null) return false;
        try
        {
            using var db = dbFactory.CreateDbContext();
            var current = db.Users.AsNoTracking().SingleOrDefault(u => u.Id == sessionAccount.Id);
            if (current != null && current.Username == sessionAccount.Username && current.Role == sessionAccount.Role
                && current.PasswordHash == sessionAccount.PasswordHash && current.Salt == sessionAccount.Salt
                && current.PasswordChanged == sessionAccount.PasswordChanged) return true;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException)
        {
            SetSession(null, "", false);
            Status = "Unable to verify your session. Log in again.";
            return false;
        }
        SetSession(null, "", false);
        Status = "Your account changed. Log in again.";
        return false;
    }

    public bool ChangeCurrentPassword(FormField[] fields)
    {
        if (!ValidateSession() || CurrentUsername == null)
        {
            Status = "Log in before changing your password.";
            return false;
        }
        if (!ChangePassword(CurrentUsername, fields)) return false;
        RequiresPasswordChange = false;
        OnPropertyChanged(nameof(RequiresPasswordChange));
        OnPropertyChanged(nameof(CanAccessWorkspace));
        return true;
    }

    public void AddProductLine(UiRecord product)
    {
        Lines.Add(new InvoiceLineViewModel
        {
            Name = product.Name,
            Price = ParseDecimal(product["Sale Price"]),
            TaxRate = ParseDecimal(product["Tax (%)"]),
            Discount = ParseDecimal(product["Default Discount"]),
            DiscountPerUnit = true,
            PriceIncludesTax = bool.TryParse(product["Price includes tax"], out var inclusive) && inclusive
        });
    }

    public void StartDocument(string type)
    {
        if (type is not ("Invoice" or "Quotation" or "Receipt"))
            throw new ArgumentException("Unknown document type.", nameof(type));
        editingDocument = null; editingSnapshot = null; editingFingerprint = null; historicalLines.Clear();
        Lines.Clear();
        AdditionalCosts.Clear();
        foreach (var field in InvoiceCustomer) field.Value = "";
        InvoiceDetails[0].Value = type;
        InvoiceDetails[1].Value = DateTime.Today.ToString("yyyy-MM-dd");
        InvoiceDetails[2].Value = "";
        InvoiceDetails[4].Value = "";
        InvoiceOptions[0].Value = "None";
        InvoiceOptions[1].Value = "0";
        InvoiceOptions[2].Value = "";
        NavigateCommand.Execute("New Invoice");
    }

    public bool VerifyUser(string username, string password) => AuthenticateUser(username, password) != null;

    private AppUser? AuthenticateUser(string username, string password)
    {
        if (dbFactory == null)
        {
            Status = "User storage is not available.";
            return null;
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var user = db.Users.FirstOrDefault(u => u.Username == username.Trim());
        if (user == null || !PasswordCredentials.Verify(password, user.Salt, user.PasswordHash, out var needsUpgrade))
        {
            Status = "Invalid username or password.";
            return null;
        }

        if (needsUpgrade)
        {
            user.Salt = PasswordCredentials.CreateSalt();
            user.PasswordHash = HashPassword(password, user.Salt);
            db.SaveChanges();
        }
        Status = $"Logged in as {user.Username}.";
        return user;
    }

    public bool ChangePassword(string username, FormField[] fields)
    {
        if (dbFactory == null)
        {
            Status = "User storage is not available.";
            return false;
        }
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        if (fields[1].Value.Length < 8)
        {
            fields[1].Error = "Password must be at least 8 characters.";
            return false;
        }
        if (fields[1].Value != fields[2].Value)
        {
            fields[2].Error = "Passwords do not match.";
            return false;
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var user = db.Users.FirstOrDefault(u => u.Username == username.Trim());
        if (user == null || !PasswordCredentials.Verify(fields[0].Value, user.Salt, user.PasswordHash, out _))
        {
            fields[0].Error = "Current password is incorrect.";
            return false;
        }

        user.Salt = PasswordCredentials.CreateSalt();
        user.PasswordHash = HashPassword(fields[1].Value, user.Salt);
        user.PasswordChanged = true;
        db.SaveChanges();
        if (sessionAccount?.Id == user.Id) sessionAccount = user;
        Status = "Password changed.";
        return true;
    }

    public FormField[][] CreateOnboardingFields()
    {
        FormField Copy(string category, string label)
        {
            var source = Settings[category].SelectMany(s => s.Fields).Single(f => f.Label == label);
            return new FormField(source.Label, source.Value, source.Kind, source.Options, source.Required)
                { IsChecked = source.IsChecked };
        }
        return [
            new[] { "Company Name", "Country", "Company logo" }.Select(l => Copy("Company Info", l)).ToArray(),
            new[] { "Currency", "Date Format", "Starting Number", "Leading Zeros", "Default Tax Rate (%)" }.Select(l => Copy("Invoice Settings", l)).ToArray(),
            new[] { "Page Size", "Template" }.Select(l => Copy("PDF Settings", l)).ToArray()
        ];
    }

    public bool CompleteOnboarding(FormField[][] groups)
    {
        if (groups.Length != 3) return false;
        if (!groups.SelectMany(g => g).Select(f => f.Validate()).ToArray().All(v => v)) return false;
        var starting = groups[1].Single(f => f.Label == "Starting Number");
        if (!int.TryParse(starting.Value, out var number) || number < 1 || number > 99999999)
        {
            starting.Error = "Enter a whole number between 1 and 99999999.";
            return false;
        }
        var tax = groups[1].Single(f => f.Label == "Default Tax Rate (%)");
        if (tax.Number > 100) { tax.Error = "Tax rate must be between 0 and 100."; return false; }
        string[] categories = ["Company Info", "Invoice Settings", "PDF Settings"];
        var updates = groups.SelectMany((group, index) => group.Select(field =>
        {
            var section = Settings[categories[index]].Single(s => s.Fields.Any(f => f.Label == field.Label));
            var target = section.Fields.Single(f => f.Label == field.Label);
            var value = field.Kind == "toggle" ? field.IsChecked.ToString() : field.Value.Trim();
            return (Key: SettingKey(categories[index], section.Title, field.Label), Target: target, Value: value);
        })).ToArray();
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var transaction = db.Database.BeginTransaction();
            foreach (var update in updates) SetSetting(db, update.Key, update.Value);
            var company = db.CompanyInfos.OrderBy(c => c.Id).FirstOrDefault() ?? new CompanyInfo();
            company.Name = groups[0].Single(f => f.Label == "Company Name").Value.Trim();
            if (company.Id == 0) db.CompanyInfos.Add(company);
            SetSetting(db, "onboarding.completed", "true");
            db.SaveChanges();
            transaction.Commit();
        }
        foreach (var update in updates)
        {
            update.Target.Value = update.Value;
            update.Target.IsChecked = bool.TryParse(update.Value, out var value) && value;
        }
        Status = "Setup saved.";
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
            db.EnsureCurrentSchema();
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



    public ReportSnapshot BuildReport(string name)
    {
        var invoices = ActiveInvoices.ToArray();
        var billed = invoices.Sum(i => ParseDecimal(i["Total"]));
        var paid = invoices.Sum(i => ParseDecimal(i["Paid"]));
        var outstanding = invoices.Sum(i => ParseDecimal(i["Outstanding"]));
        var rows = name switch
        {
            "Revenue" => new[] {
                new[] { "Metric", "Value" },
                new[] { "Total Billed", Money(billed) },
                new[] { "Total Collected", Money(paid) },
                new[] { "Outstanding", Money(outstanding) },
                new[] { "Average Invoice Value", Money(invoices.Length == 0 ? 0 : billed / invoices.Length) } },
            "Receivables" => invoices.Where(i => ParseDecimal(i["Outstanding"]) > 0).Select(i => new[] { i["Customer"], i.Name, i["Date"], Money(ParseDecimal(i["Outstanding"])) }).Prepend(["Customer", "Invoice ID", "Date", "Outstanding"]).ToArray(),
            "Tax" => invoices.GroupBy(i => i["Date"]).Select(g => new[] { g.Key, Money(g.Sum(i => ParseDecimal(i["Tax"]))) }).OrderBy(r => r[0]).Prepend(["Date", "Tax"]).ToArray(),
            "Customers" => invoices.GroupBy(i => i["Customer"]).Select(g => new[] { string.IsNullOrWhiteSpace(g.Key) ? "Unknown" : g.Key, g.Count().ToString(), Money(g.Sum(i => ParseDecimal(i["Total"]))), Money(g.Sum(i => ParseDecimal(i["Paid"]))), Money(g.Sum(i => ParseDecimal(i["Outstanding"]))) }).OrderByDescending(r => ParseDecimal(r[2].Replace("₹", ""))).Prepend(["Customer", "Invoices", "Billed", "Collected", "Outstanding"]).ToArray(),
            "Products" => ProductReportRows(),
            "Quotations" => new string[][] { ["Metric", "Value"], ["Quotations Issued", Invoices.Count(i => i["Type"] == "Quotation" && !DeletedRecords.Contains(i.Id)).ToString()], ["Invoices in Period", invoices.Length.ToString()] },
            "Invoice Status" => invoices.Select(i => new[] { i.Name, i["Customer"], i["Date"], Money(ParseDecimal(i["Total"])), i["Status"], Money(ParseDecimal(i["Outstanding"])) }).Prepend(["Invoice", "Customer", "Date", "Total", "Status", "Outstanding"]).ToArray(),
            "Daily Report" => invoices.GroupBy(i => i["Date"]).Select(g => new[] { g.Key, g.Count().ToString(), Money(g.Sum(i => ParseDecimal(i["Total"]))), Money(g.Sum(i => ParseDecimal(i["Paid"]))), Money(g.Sum(i => ParseDecimal(i["Outstanding"]))) }).OrderByDescending(r => r[0]).Prepend(["Date", "Invoices", "Sales", "Collected", "Outstanding"]).ToArray(),
            _ => new string[][] { ["Metric", "Value"] }
        };

        return new ReportSnapshot(name, invoices.Length, billed, paid, outstanding, rows);
    }


    private string[][] ProductReportRows()
    {
        IEnumerable<ProductReportLine> lines;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            lines = db.InvoiceItems.AsNoTracking()
                .Join(db.Invoices.AsNoTracking(), item => item.InvoiceId, invoice => invoice.Id, (item, invoice) => new { item, invoice })
                .Where(x => x.invoice.Status != "Draft" && x.invoice.Type == "Invoice" && x.invoice.DeletedAt == null)
                .Select(x => new ProductReportLine(x.item.Description, x.item.Quantity, x.item.UnitPrice, x.item.DiscountPerUnit ? x.item.Discount * x.item.Quantity : x.item.Discount, x.item.PurchasePrice))
                .ToArray();
        }
        else
        {
            lines = Lines.Select(line => new ProductReportLine(line.Name, line.Quantity, line.Price, line.DiscountPerUnit ? line.Discount * line.Quantity : line.Discount, 0)).ToArray();
        }

        return lines.GroupBy(l => l.Name)
            .Select(g => { var sales = g.Sum(l => l.UnitPrice * l.Quantity); var cogs = g.Sum(l => l.PurchasePrice * l.Quantity); var profit = sales - g.Sum(l => l.Discount) - cogs; var margin = sales == 0 ? 0 : profit * 100 / sales; return new[] { g.Key, g.Sum(l => l.Quantity).ToString("0.###"), Money(sales), Money(g.Sum(l => l.Discount)), Money(profit), margin.ToString("0.0") + "%" }; })
            .Prepend(["Product / Service", "Units Sold", "Sales", "Discount Given", "Profit", "Margin"])
            .ToArray();
    }

    private sealed record ProductReportLine(string Name, decimal Quantity, decimal UnitPrice, decimal Discount, decimal PurchasePrice);


    public byte[] ExportDocumentPdf(UiRecord document)
    {
        var lines = new List<string>
        {
            Branding.Name,
            $"{document["Type"]}: {document.Name}",
            $"Customer: {document["Customer"]}",
            $"Date: {document["Date"]}",
            $"Status: {document["Status"]}",
            "",
            "Items"
        };

        foreach (var item in InvoiceItemsFor(document))
        {
            var discount = item.DiscountPerUnit ? item.Discount * item.Quantity : item.Discount;
            lines.Add($"{item.Description}  Qty {item.Quantity:0.###}  Rate {item.UnitPrice:0.00}  Discount {discount:0.00}  Tax {item.TaxRate:0.##}%");
        }

        lines.Add("");
        lines.Add($"Total: {document["Total"]}");
        lines.Add($"Paid: {document["Paid"]}");
        lines.Add($"Outstanding: {document["Outstanding"]}");
        lines.Add($"Generated by {Branding.Name}");
        Status = $"Exported {document.Name} PDF.";
        return SimplePdf.Create(lines);
    }

    private InvoiceItem[] InvoiceItemsFor(UiRecord document)
    {
        if (dbFactory == null || document.SourceId <= 0) return [];
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        return db.InvoiceItems.AsNoTracking().Where(i => i.InvoiceId == document.SourceId).OrderBy(i => i.Id).ToArray();
    }


    public byte[] ExportReportPdf(string name)
    {
        var report = BuildReport(name);
        var lines = new List<string>
        {
            Branding.Name,
            $"{name} Report",
            $"Invoices: {report.InvoiceCount}",
            $"Billed: {Money(report.Billed)}",
            $"Collected: {Money(report.Collected)}",
            $"Outstanding: {Money(report.Outstanding)}",
            ""
        };

        foreach (var row in report.Rows) lines.Add(string.Join("  |  ", row));
        Status = $"Exported {name} report PDF.";
        return SimplePdf.Create(lines);
    }

    public string ExportReportCsv(string name)
    {
        var rows = BuildReport(name).Rows;
        Status = $"Exported {name} report.";
        return string.Join(Environment.NewLine, rows.Select(row => string.Join(",", row.Select(EscapeCsv)))) + Environment.NewLine;
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

    public bool RestoreDatabaseBackup(byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || bytes.Length == 0)
        {
            Status = "Database backup is empty or unsupported.";
            return false;
        }

        try
        {
            DatabaseBackupRestore.Restore(bytes, databasePath);
            SetSession(null, "", false);
            ReloadFromDatabase();
            Status = "Database backup restored successfully.";
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Database restore failed: {ex.Message}";
            return false;
        }
    }

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

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
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

        ReloadFromDatabase();
        Status = "Backup restored successfully.";
        return true;
    }

    private void ReloadFromDatabase()
    {
        Customers.Clear(); Products.Clear(); Users.Clear(); Invoices.Clear(); Payments.Clear(); DeletedRecords.Clear();
        LoadPersistedRecords();
        LoadPersistedSettings();
        LoadThemeMode();
        LoadLanguage();
    }

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
                    ["Date"] = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["Items"] = invoice.Items.Count.ToString(),
                    ["Total"] = invoice.GrandTotal.ToString("0.00"),
                    ["Tax"] = invoice.TaxTotal.ToString(CultureInfo.InvariantCulture),
                    ["Paid"] = invoice.PaidAmount.ToString("0.00"),
                    ["Outstanding"] = invoice.BalanceAmount.ToString("0.00"),
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

    private void LoadPersistedSettings()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();

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
        if (dbFactory == null) return sourceId;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();

        if (kind == "Customer")
        {
            var customer = sourceId > 0 ? db.Customers.Find(sourceId) ?? new Customer() : new Customer();
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
            var product = sourceId > 0 ? db.Products.Find(sourceId) ?? new Product() : new Product();
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
            product.CustomUnit = values.GetValueOrDefault("Custom unit", "");
            product.StorageLocation = values.GetValueOrDefault("Storage Location", "");
            product.ContainerNumber = values.GetValueOrDefault("Container Number", "");
            product.BatchNumber = values.GetValueOrDefault("Batch Number", "");
            product.ExpiryDate = values.GetValueOrDefault("Expiry Date", "");
            product.ManufactureDate = values.GetValueOrDefault("Manufacture Date", "");
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
            transaction.Commit();
            return user.Id;
        }

        return sourceId;
    }

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
            AdditionalCosts.Select(c => new InvoiceAdditionalCost(c[0].Value, c[1].Number)).ToArray());
    }

    private int SaveInvoiceToDatabase(Dictionary<string, string> values)
    {
        if (dbFactory == null) return 0;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        using var transaction = db.Database.BeginTransaction();
        Invoice? existing = null;
        if (editingDocument != null)
        {
            existing = db.Invoices.Include(i => i.Items).SingleOrDefault(i => i.Id == editingDocument.SourceId);
            if (existing == null || existing.DeletedAt != null || Fingerprint(existing) != editingFingerprint)
            { Status = "Document changed since it was opened. Reopen it before saving."; return -1; }
            if (existing.PaidAmount != 0 || db.Payments.Any(p => p.InvoiceId == existing.Id))
            { Status = "Documents with payments cannot be edited yet."; return -1; }
            if (values["Type"] != existing.Type)
            { Status = "Changing document type during editing is not supported."; return -1; }
        }
        values["Name"] = existing?.InvoiceNumber ?? NextDocumentNumber(db, values["Type"]);
        var customerName = InvoiceCustomer[0].Value.Trim();
        var customerId = db.Customers.AsNoTracking().FirstOrDefault(c => c.Name == customerName)?.Id;
        var invoice = new Invoice
        {
            InvoiceNumber = values["Name"],
            Type = values["Type"],
            InvoiceDate = DateTime.TryParse(InvoiceDetails[1].Value, out var date) ? date : DateTime.Today,
            CustomerId = customerId,
            CustomerName = customerName,
            Snapshot = CaptureInvoiceSnapshot(),
            Status = "Unpaid",
            SubTotal = Totals.Subtotal,
            TaxTotal = Totals.Tax,
            DiscountTotal = Totals.ItemDiscount + Totals.InvoiceDiscount,
            GrandTotal = Totals.Total,
            Items = Lines.Select(line =>
            {
                var product = Products.FirstOrDefault(p => p.Name.Equals(line.Name, StringComparison.OrdinalIgnoreCase));
                return new InvoiceItem
                {
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
            invoice = existing;
        }
        db.SaveChanges();
        if (editingDocument != null) editingFingerprint = Fingerprint(db.Invoices.AsNoTracking().Include(i => i.Items).Single(i => i.Id == invoice.Id));
        transaction.Commit();
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
        db.EnsureCurrentSchema();
        var invoice = db.Invoices.Include(i => i.Items).FirstOrDefault(i => i.Id == invoiceRecord.SourceId);
        if (invoice == null || invoice.DeletedAt != null)
        {
            Status = "Invoice was not found or is in trash.";
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

    private static string HashPassword(string password, string salt)
    {
        return PasswordCredentials.Hash(password, salt);
    }

    private static string Money(decimal value) => $"₹ {value:0.00}";

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

    private static void ValidateJsonBackup(JsonObject backup)
    {
        var customers = ReadRows<Customer>(backup, "customers");
        var products = ReadRows<Product>(backup, "products");
        var company = ReadRows<CompanyInfo>(backup, "company_info");
        var settings = ReadRows<AppSetting>(backup, "settings");
        var invoices = ReadRows<InvoiceBackupRow>(backup, "invoices");
        var items = ReadRows<InvoiceItem>(backup, "invoice_items");
        var payments = ReadRows<Payment>(backup, "invoice_payments");
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

    private IEnumerable<UiRecord> RecordsForKind(string kind) => kind switch
    {
        "Customer" => Customers,
        "Product" => Products,
        "User" => Users,
        "Invoice" or "Quotation" or "Receipt" => Invoices.Where(r => r["Type"] == kind && !DeletedRecords.Contains(r.Id)),
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

public sealed record ReportSnapshot(string Name, int InvoiceCount, decimal Billed, decimal Collected, decimal Outstanding, string[][] Rows);

internal static class SimplePdf
{
    public static byte[] Create(IEnumerable<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 14 Tf");
        content.AppendLine("50 790 Td");
        foreach (var line in lines)
        {
            content.Append("(").Append(Escape(line)).AppendLine(") Tj");
            content.AppendLine("0 -20 Td");
        }
        content.AppendLine("ET");

        var stream = content.ToString();
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"
        };

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };
        foreach (var (obj, index) in objects.Select((value, i) => (value, i + 1)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index).AppendLine(" 0 obj");
            pdf.AppendLine(obj);
            pdf.AppendLine("endobj");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.Append("0 ").AppendLine((objects.Length + 1).ToString());
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) pdf.Append(offset.ToString("0000000000")).AppendLine(" 00000 n ");
        pdf.AppendLine("trailer");
        pdf.Append("<< /Size ").Append(objects.Length + 1).AppendLine(" /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xref.ToString());
        pdf.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
