using CommunityToolkit.Mvvm.ComponentModel;
using LedgerNest.Application;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    [ObservableProperty] private string dashboardLayout = "default";

    public UiRecord[] GetDashboardStockProducts()
    {
        if (dbFactory == null) return Products.Where(product =>
            !(bool.TryParse(product["Unlimited stock"], out var unlimited) && unlimited)
            && decimal.TryParse(product["Stock"], out var stock) && stock <= 0).ToArray();
        using var db = dbFactory.CreateDbContext();
        return db.Products.AsNoTracking().Where(product => !product.UnlimitedStock && product.StockQuantity <= 0)
            .OrderBy(product => product.Name).ToArray().Select(product => new UiRecord
            {
                SourceId = product.Id,
                Values = new() { ["Name"] = product.Name, ["Type"] = product.Type,
                    ["Sale Price"] = product.SalePrice.ToString("0.##"), ["Stock"] = product.StockQuantity.ToString("0.###") }
            }).ToArray();
    }

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
