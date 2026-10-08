using CommunityToolkit.Mvvm.ComponentModel;
using LedgerNest.Application;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    [ObservableProperty] private string dashboardLayout = "default";

    public bool SetDashboardLayout(string value)
    {
        var selected = DashboardLayoutRules.Parse(value);
        if (selected == null) { Status = "Choose Default, Classic, Bento or Simple."; return false; }
        if (!HasPermission("Dashboard", "View")) { Status = "Your role cannot access Dashboard."; return false; }
        try
        {
            if (dbFactory != null)
            {
                using var db = dbFactory.CreateDbContext();
                SetSetting(db, DashboardLayoutRules.SettingKey, selected);
                db.SaveChanges();
            }
            DashboardLayout = selected;
            return true;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or DbUpdateException or InvalidOperationException)
        { Status = "Dashboard layout could not save: " + ex.Message; return false; }
    }
}
