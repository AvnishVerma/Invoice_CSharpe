using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public sealed record CompanyWorkspaceContext(CompanyRegistryService Registry, CompanyProfile ActiveCompany, Action<CompanyProfile> RequestSwitch);

public sealed partial class CompanyManagementViewModel : ObservableObject
{
    private readonly MainWindowViewModel workspace;
    private readonly CompanyWorkspaceContext context;
    public ObservableCollection<CompanyProfile> Companies { get; } = [];
    public string ActiveCompanyId => context.ActiveCompany.Id;
    public string ActiveCompanyName => Companies.FirstOrDefault(company => company.Id == ActiveCompanyId)?.Name ?? context.ActiveCompany.Name;
    public bool CanManage => workspace.IsAdministrator && workspace.HasPermission("Settings", "Update");
    public bool CanSwitch => SelectedCompany != null && SelectedCompany.Id != ActiveCompanyId;
    public bool CanDelete => CanManage && CanSwitch && Companies.Count > 1;
    public Action<CompanyProfile>? RequestDeleteConfirmation { get; set; }
    [ObservableProperty] private CompanyProfile? selectedCompany;
    [ObservableProperty] private string newCompanyName = "";
    [ObservableProperty] private string renameCompanyName = "";
    [ObservableProperty] private string status = "";

    public CompanyManagementViewModel(MainWindowViewModel workspace, CompanyWorkspaceContext context)
    {
        this.workspace = workspace;
        this.context = context;
        Refresh();
    }

    partial void OnSelectedCompanyChanged(CompanyProfile? value)
    {
        RenameCompanyName = value?.Name ?? "";
        OnPropertyChanged(nameof(CanSwitch)); OnPropertyChanged(nameof(CanDelete));
    }

    public void RefreshAuthorizationState()
    { OnPropertyChanged(nameof(CanManage)); OnPropertyChanged(nameof(CanDelete)); }

    [RelayCommand]
    public void Refresh()
    {
        var selected = SelectedCompany?.Id ?? ActiveCompanyId;
        Companies.Clear();
        foreach (var company in context.Registry.Read().Companies) Companies.Add(company);
        SelectedCompany = Companies.FirstOrDefault(company => company.Id == selected) ?? Companies.First(company => company.Id == ActiveCompanyId);
        OnPropertyChanged(nameof(ActiveCompanyName));
    }

    private bool DemandManage()
    {
        if (!workspace.CanContinueWorkspaceOperation(workspace.SessionVersion) || !CanManage)
        { Status = "Administrator access is required to manage companies."; return false; }
        return true;
    }

    [RelayCommand]
    public void CreateCompany()
    {
        if (!DemandManage() || !workspace.RequireCompanyWriteLicense()) return;
        Run(() =>
        {
            var company = context.Registry.Create(NewCompanyName);
            Refresh(); SelectedCompany = Companies.Single(item => item.Id == company.Id);
            NewCompanyName = ""; Status = "Company created. Switch to it to sign in.";
        });
    }

    [RelayCommand]
    public void RenameCompany()
    {
        if (!DemandManage() || SelectedCompany == null || !workspace.RequireCompanyWriteLicense()) return;
        Run(() => { context.Registry.Rename(SelectedCompany.Id, RenameCompanyName); Refresh(); Status = "Company renamed."; });
    }

    [RelayCommand]
    public void SwitchCompany()
    {
        if (!CanSwitch || SelectedCompany == null) return;
        Run(() => context.RequestSwitch(SelectedCompany));
    }

    [RelayCommand]
    private void RequestDelete()
    {
        if (!DemandManage() || !CanDelete || SelectedCompany == null) return;
        RequestDeleteConfirmation?.Invoke(SelectedCompany);
    }

    public void DeleteCompany(CompanyProfile company)
    {
        if (!DemandManage() || !workspace.RequireCompanyWriteLicense()) return;
        Run(() => { context.Registry.Delete(company.Id); SelectedCompany = null; Refresh(); Status = "Company removed. Its database is retained for recovery."; });
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or TimeoutException or Microsoft.EntityFrameworkCore.DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        { Status = "Company operation failed: " + ex.Message; }
    }
}
