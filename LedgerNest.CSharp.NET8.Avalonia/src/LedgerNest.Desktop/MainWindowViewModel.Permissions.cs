using Microsoft.EntityFrameworkCore;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    private static readonly IReadOnlyDictionary<string, string> RouteResources = new Dictionary<string, string>
    {
        ["Dashboard"] = "Dashboard", ["New Invoice"] = "Invoice", ["Invoices"] = "Invoice", ["Receipts"] = "Invoice",
        ["Quotations"] = "Quotation", ["Customers"] = "Customer", ["Products"] = "Product", ["Units"] = "Unit",
        ["Prices"] = "Price", ["Inventory"] = "Inventory", ["Reports"] = "Reports", ["Settings"] = "Settings"
    };

    public IEnumerable<string> VisibleRoutes => Routes.Where(CanNavigate);
    public long AuthorizationVersion { get; private set; }
    public bool CanNavigate(string route) => RouteResources.TryGetValue(route, out var resource) && CanAccessResource(resource);

    public bool CanAccessResource(string resource) => CanView(resource);
    public bool CanView(string resource) => CanPerformAction(resource, "View");
    public bool CanAdd(string resource) => CanPerformAction(resource, "Add");
    public bool CanUpdate(string resource) => CanPerformAction(resource, "Update");
    public bool CanDelete(string resource) => CanPerformAction(resource, "Delete");

    public bool CanPerformAction(string resource, string action)
    {
        if (dbFactory == null) return true;
        if (sessionAccount == null) return false;
        action = action switch { "Edit" or "Manage" or "Adjust" or "Cancel" or "Refund" => "Update", "Create" => "Add", _ => action };
        return new AuthorizationService(dbFactory).IsAllowedAsync(sessionAccount.Id, resource, action).GetAwaiter().GetResult();
    }

    public bool HasPermission(string resource, string action) => CanPerformAction(resource, action);

    private void RefreshAuthorizationState()
    {
        AuthorizationVersion++;
        OnPropertyChanged(nameof(AuthorizationVersion));
        OnPropertyChanged(nameof(VisibleRoutes));
        UnitMaster.RefreshAuthorizationState();
        ProductPriceMaster.RefreshAuthorizationState();
        Inventory.RefreshAuthorizationState();
        PermissionManagement.RefreshAuthorizationState();
    }
}
