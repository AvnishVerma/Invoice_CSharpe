using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    public bool HasPermission(string resource, string action)
    {
        if (CurrentRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) || dbFactory == null) return true;
        using var db = dbFactory.CreateDbContext();
        var configured = db.RolePermissions.AsNoTracking().Where(item => item.Role == CurrentRole).ToArray();
        return configured.Length == 0 || configured.Any(item => item.IsAllowed && item.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) && item.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
    }
}
