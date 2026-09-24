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
    private string savedStartingNumber = "1";
    public bool SetInvoiceBrandingImage(string label, byte[] bytes)
    {
        if (label is not ("Signature Image" or "Watermark Image")) throw new ArgumentException("Unknown branding image.", nameof(label));
        var field = InvoiceSetting(label);
        field.Error = "";
        if (bytes.Length > 2 * 1024 * 1024) { field.Error = "Choose an image up to 2 MB."; return false; }
        using var data = SkiaSharp.SKData.CreateCopy(bytes);
        using var codec = SkiaSharp.SKCodec.Create(data);
        if (codec == null || codec.EncodedFormat is not (SkiaSharp.SKEncodedImageFormat.Png or SkiaSharp.SKEncodedImageFormat.Jpeg))
        { field.Error = "Choose a valid PNG or JPEG image."; return false; }
        using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);
        if (bitmap == null) { field.Error = "The image could not be decoded."; return false; }
        field.Value = "base64:" + Convert.ToBase64String(bytes);
        return true;
    }
    public FormField InvoiceSetting(string label) => Settings["Invoice Settings"].SelectMany(section => section.Fields).Single(field => field.Label == label);

    public bool CanChangeInvoiceStartingNumber
    {
        get
        {
            if (dbFactory == null) return !Invoices.Any();
            using var db = dbFactory.CreateDbContext();
            return !db.Invoices.Any(); // Includes documents in trash.
        }
    }

    private bool ValidateInvoiceSettings()
    {
        var start = InvoiceSetting("Starting Number");
        if (!int.TryParse(start.Value, out var number) || number < 1 || number > 99999999)
            start.Error = "Enter a whole number between 1 and 99999999.";
        else if (!CanChangeInvoiceStartingNumber)
        {
            var persistedStart = savedStartingNumber;
            if (dbFactory != null)
            {
                using var db = dbFactory.CreateDbContext();
                var key = SettingKey("Invoice Settings", "General", "Starting Number");
                persistedStart = db.Settings.AsNoTracking().FirstOrDefault(setting => setting.Key == key)?.Value ?? "1";
            }
            if (start.Value != persistedStart) start.Error = "Invoice starting number cannot be changed while documents exist, including trash.";
        }
        var tax = InvoiceSetting("Default Tax Rate (%)");
        if (tax.Number > 100) tax.Error = "Tax rate must be between 0 and 100.";
        var prefix = InvoiceSetting("Invoice Prefix");
        if (prefix.Value.Length > 25) prefix.Error = "Invoice prefix must be 25 characters or fewer.";
        if (InvoiceSetting("Enable Custom Fields").IsChecked && InvoiceCustomFieldDefinitions.Any(field => string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > 100))
        { Status = "Every custom field needs a label of 1 to 100 characters."; return false; }
        var invalid = Settings["Invoice Settings"].SelectMany(section => section.Fields).FirstOrDefault(field => field.Error.Length > 0);
        if (invalid != null) Status = invalid.Error;
        return invalid == null;
    }
    // Performs the set language action for this screen or workflow.
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

    // Performs the load language action for this screen or workflow.
    private void LoadLanguage()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        Language = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "appearance.language")?.Value ?? "English";
    }

    // Performs the set theme mode action for this screen or workflow.
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

    // Performs the load theme mode action for this screen or workflow.
    private void LoadThemeMode()
    {
        if (dbFactory == null) return;
        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var setting = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == "appearance.theme_mode")?.Value;
        ThemeMode = setting is "Dark" or "System" ? setting : "Light";
    }

    public bool NeedsFirstTimeSetup
    {
        get
        {
            if (dbFactory == null || CurrentRole != "Admin") return false;
            using var db = dbFactory.CreateDbContext();
            return !db.Settings.Any(s => s.Key == "onboarding.completed" && s.Value == "true");
        }
    }

    // Performs the create onboarding fields action for this screen or workflow.
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

    // Performs the complete onboarding action for this screen or workflow.
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
        savedStartingNumber = InvoiceSetting("Starting Number").Value;
        Status = "Setup saved.";
        return true;
    }

    // Performs the save settings action for this screen or workflow.
    public bool SaveSettings(string name)
    {
        if (!Settings.TryGetValue(name, out var sections)) return false;
        var fields = sections.SelectMany(s => s.Fields).ToArray();
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        if (name == "Invoice Settings" && !ValidateInvoiceSettings()) return false;

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
            if (name == "Company Info") SavePaymentAccounts(db);
            if (name == "Invoice Settings") SetSetting(db, "invoice.custom_field_definitions", JsonSerializer.Serialize(InvoiceCustomFieldDefinitions));
            db.SaveChanges();
        }

        if (name == "Invoice Settings") savedStartingNumber = InvoiceSetting("Starting Number").Value;
        Status = $"{name} saved.";
        return true;
    }

    // Loads repeatable UPI and bank accounts from settings and seeds one editable row when none have been saved.
    private void LoadPaymentAccounts()
    {
        UpiAccounts.Clear();
        BankAccounts.Clear();
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            LoadAccounts(db.Settings.AsNoTracking().FirstOrDefault(setting => setting.Key == "company.upi_accounts")?.Value, UpiAccounts, CreateUpiAccount);
            LoadAccounts(db.Settings.AsNoTracking().FirstOrDefault(setting => setting.Key == "company.bank_accounts")?.Value, BankAccounts, CreateBankAccount);
        }
    }

    // Adds a blank UPI account row to the company payment settings.
    public void AddUpiAccount() => UpiAccounts.Add(CreateUpiAccount());

    // Adds a blank bank account row to the company payment settings.
    public void AddBankAccount() => BankAccounts.Add(CreateBankAccount());

    // Removes a UPI account while keeping at least one editable row visible.
    public void RemoveUpiAccount(FormField[] account)
    {
        UpiAccounts.Remove(account);
        if (UpiAccounts.Count == 0) AddUpiAccount();
    }

    // Removes a bank account while keeping at least one editable row visible.
    public void RemoveBankAccount(FormField[] account)
    {
        BankAccounts.Remove(account);
        if (BankAccounts.Count == 0) AddBankAccount();
    }

    // Creates an editable UPI account row from persisted values.
    private static FormField[] CreateUpiAccount(Dictionary<string, string>? values = null) =>
    [
        new("Label", values?.GetValueOrDefault("Label", "") ?? ""),
        new("UPI ID", values?.GetValueOrDefault("UPI ID", "") ?? "")
    ];

    // Creates an editable bank account row from persisted values.
    private static FormField[] CreateBankAccount(Dictionary<string, string>? values = null) =>
    [
        new("Label", values?.GetValueOrDefault("Label", "") ?? ""),
        new("Bank Name", values?.GetValueOrDefault("Bank Name", "") ?? ""),
        new("Account Number", values?.GetValueOrDefault("Account Number", "") ?? ""),
        new("IFSC Code", values?.GetValueOrDefault("IFSC Code", "") ?? "")
    ];

    // Deserializes account rows without preventing Settings from opening when stored data is malformed.
    private static void LoadAccounts(string? json, ObservableCollection<FormField[]> target, Func<Dictionary<string, string>?, FormField[]> factory)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            foreach (var values in JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json) ?? []) target.Add(factory(values));
        }
        catch (JsonException)
        {
            target.Clear();
        }
    }

    // Persists all repeatable payment account rows as settings JSON in the active database.
    private void SavePaymentAccounts(LedgerNestDbContext db)
    {
        static Dictionary<string, string> Values(FormField[] fields) => fields.ToDictionary(field => field.Label, field => field.Value.Trim());
        SetSetting(db, "company.upi_accounts", JsonSerializer.Serialize(UpiAccounts.Select(Values)));
        SetSetting(db, "company.bank_accounts", JsonSerializer.Serialize(BankAccounts.Select(Values)));
    }
}
