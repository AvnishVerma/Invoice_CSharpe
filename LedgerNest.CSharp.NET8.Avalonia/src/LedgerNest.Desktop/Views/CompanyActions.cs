using CommunityToolkit.Mvvm.Input;
using LedgerNest.Infrastructure;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop;

public partial class MainWindow
{
    internal void ConfirmCompanySwitch(CompanyProfile company, Action switchCompany) =>
        Confirm("Switch Company", $"Switch to {company.Name}? You will be signed out. Unsaved invoice changes will be discarded.", () =>
        {
            try { switchCompany(); }
            catch (Exception ex)
            {
                AppErrorLog.Write(ex, "Switching companies");
                Model.Status = "Company switch failed: " + ex.Message;
                ShowOverlay("Company Switch Failed", new DialogMessage(Model.Status), new DialogActions([new DialogAction("Close", new RelayCommand(CloseOverlay), true)]));
            }
        });

    private CompanyManagementView CompaniesView()
    {
        var manager = Model.CompanyManagement;
        if (manager != null)
            manager.RequestDeleteConfirmation = company => Confirm("Delete Company", $"Remove {company.Name} from this device's company list? Its database will be retained for recovery.", () =>
            { manager.DeleteCompany(company); });
        return new CompanyManagementView { DataContext = manager };
    }
}
