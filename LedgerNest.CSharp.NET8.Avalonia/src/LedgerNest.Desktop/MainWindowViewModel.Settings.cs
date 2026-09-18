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
        Status = "Setup saved.";
        return true;
    }

    // Performs the save settings action for this screen or workflow.
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
}
