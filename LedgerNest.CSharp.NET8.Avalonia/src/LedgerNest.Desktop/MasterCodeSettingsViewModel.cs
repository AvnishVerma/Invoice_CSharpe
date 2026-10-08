using System.Collections.ObjectModel;
using System.Data.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public sealed partial class CodeConfigurationViewModel : ObservableObject
{
    public string Entity { get; }
    public string Title => Entity + " Code";
    [ObservableProperty] private string prefix;
    [ObservableProperty] private decimal nextNumber;
    [ObservableProperty] private decimal leadingZeros;
    [ObservableProperty] private bool autoGenerate;
    public string Preview
    {
        get { try { var settings = Configuration(); return "Preview: " + AutoCodeRules.Format(settings.Prefix, settings.NextNumber, settings.LeadingZeros); }
            catch (ArgumentException) { return "Preview: enter valid code settings"; } }
    }
    public CodeConfigurationViewModel(string entity, AutoCodeSettings settings)
    { Entity = entity; prefix = settings.Prefix; nextNumber = settings.NextNumber; leadingZeros = settings.LeadingZeros; autoGenerate = settings.AutoGenerate; }
    partial void OnPrefixChanged(string value) => OnPropertyChanged(nameof(Preview));
    partial void OnNextNumberChanged(decimal value) => OnPropertyChanged(nameof(Preview));
    partial void OnLeadingZerosChanged(decimal value) => OnPropertyChanged(nameof(Preview));
    public AutoCodeSettings Configuration()
    {
        if (NextNumber != decimal.Truncate(NextNumber) || NextNumber < 1 || NextNumber >= long.MaxValue
            || LeadingZeros != decimal.Truncate(LeadingZeros) || LeadingZeros is < 0 or > 18)
            throw new ArgumentException("Use a positive whole next number and a padding width from 0 to 18.");
        var result = new AutoCodeSettings(Prefix ?? "", (long)NextNumber, (int)LeadingZeros, AutoGenerate);
        AutoCodeRules.Validate(result); return result;
    }
}

public sealed partial class MasterCodeSettingsViewModel : ObservableObject
{
    private readonly MainWindowViewModel workspace;
    private readonly AutoCodeGenerator? generator;
    private readonly bool loadingFailed;
    public ObservableCollection<CodeConfigurationViewModel> Configurations { get; } = [];
    [ObservableProperty] private string error = "";
    public bool CanSave
    {
        get
        {
            try { return generator != null && !loadingFailed && workspace.HasPermission("Settings", "Update"); }
            catch (Exception ex) when (ex is DbException or InvalidOperationException or IOException) { return false; }
        }
    }
    public MasterCodeSettingsViewModel(MainWindowViewModel workspace, AutoCodeGenerator? generator)
    {
        this.workspace = workspace; this.generator = generator;
        foreach (var entity in new[] { "Customer", "Product" })
        {
            try { Configurations.Add(new(entity, generator?.Load(entity) ?? new AutoCodeSettings(entity == "Customer" ? "CUST" : "PROD"))); }
            catch (Exception ex) { loadingFailed = true; Error = "Code settings could not load: " + ex.Message; Configurations.Add(new(entity, new AutoCodeSettings(entity == "Customer" ? "CUST" : "PROD"))); }
        }
    }
    [RelayCommand] private void Save()
    {
        try
        {
            if (!CanSave) throw new UnauthorizedAccessException("Your role cannot change code settings.");
            if (generator == null) throw new InvalidOperationException("Connect a database before saving code settings.");
            generator.SaveSettings(Configurations.ToDictionary(item => item.Entity, item => item.Configuration()), workspace.CurrentUsername ?? "");
            foreach (var item in Configurations) item.NextNumber = generator.Load(item.Entity).NextNumber;
            Error = ""; workspace.Status = "Customer and product code settings saved.";
        }
        catch (Exception ex) { Error = ex.Message; workspace.Status = "Code settings could not save: " + ex.Message; }
    }
}

public partial class MainWindowViewModel
{
    public MasterCodeSettingsViewModel CreateMasterCodeSettings() => new(this, dbFactory == null ? null : new AutoCodeGenerator(dbFactory));
}
