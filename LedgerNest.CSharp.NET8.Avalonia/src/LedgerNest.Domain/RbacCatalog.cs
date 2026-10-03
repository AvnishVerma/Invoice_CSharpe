namespace LedgerNest.Domain;

public sealed record PermissionDefinition(string Resource, string Action, string Description);

public static class RbacCatalog
{
    public static readonly PermissionDefinition[] Permissions =
    [
        new("Dashboard", "View", "Open the dashboard"),
        new("Invoice", "View", "View invoices and receipts"), new("Invoice", "Add", "Create invoices and receipts"), new("Invoice", "Update", "Edit invoices and apply payments"), new("Invoice", "Delete", "Delete invoices and receipts"),
        new("Quotation", "View", "View quotations"), new("Quotation", "Add", "Create quotations"), new("Quotation", "Update", "Edit or cancel quotations"), new("Quotation", "Delete", "Delete quotations"),
        new("Customer", "View", "View customers"), new("Customer", "Add", "Create customers"), new("Customer", "Update", "Edit customers"), new("Customer", "Delete", "Delete customers"),
        new("Product", "View", "View products"), new("Product", "Add", "Create products"), new("Product", "Update", "Edit products"), new("Product", "Delete", "Delete products"),
        new("Unit", "View", "View units"), new("Unit", "Add", "Create units"), new("Unit", "Update", "Edit units"), new("Unit", "Delete", "Delete units"),
        new("Price", "View", "View selling prices"), new("Price", "Add", "Create selling units and prices"), new("Price", "Update", "Update selling prices"),
        new("Inventory", "View", "View inventory"), new("Inventory", "Update", "Post stock adjustments"),
        new("Reports", "View", "Open reports"),
        new("Settings", "View", "Open settings"), new("Settings", "Update", "Change application settings"),
        new("User", "View", "View application users"), new("User", "Add", "Create users"), new("User", "Update", "Edit users"), new("User", "Delete", "Delete users"),
        new("Permission", "View", "View roles and permissions"), new("Permission", "Update", "Manage roles and permissions")
    ];
}
