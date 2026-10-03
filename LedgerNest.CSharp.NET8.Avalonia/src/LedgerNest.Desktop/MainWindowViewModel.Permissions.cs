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
    public bool CanNavigate(string route) => RouteResources.TryGetValue(route, out var resource) && HasPermission(resource, "View");

    public bool HasPermission(string resource, string action)
    {
        if (dbFactory == null) return true;
        if (sessionAccount == null) return false;
        action = action switch { "Edit" or "Manage" or "Adjust" or "Cancel" or "Refund" => "Update", "Create" => "Add", _ => action };
        return new AuthorizationService(dbFactory).IsAllowedAsync(sessionAccount.Id, resource, action).GetAwaiter().GetResult();
    }
}
