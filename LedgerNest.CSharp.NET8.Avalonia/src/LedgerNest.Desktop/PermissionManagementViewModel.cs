using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop;

public sealed partial class PermissionRowViewModel : ObservableObject
{
    public string Resource { get; init; } = "";
    public string Action { get; init; } = "";
    public string Description { get; init; } = "";
    [ObservableProperty] private bool isAllowed;
}

public sealed record PermissionUserViewModel(int Id, string Username, string Role)
{
    public string DisplayName => $"{Username}  ·  {Role}";
}

public sealed partial class PermissionManagementViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? factory;
    private readonly Func<string> currentRole;
    public string[] Roles { get; } = ["User", "Sales", "Manager", "Admin"];
    public ObservableCollection<PermissionUserViewModel> Users { get; } = [];
    public ObservableCollection<PermissionRowViewModel> Permissions { get; } = [];
    [ObservableProperty] private PermissionUserViewModel? selectedUser;
    [ObservableProperty] private string selectedRole = "User";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;
    public bool CanManage => currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase);

    private static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Invoice", "Add", "Create invoices and receipts"), ("Invoice", "Edit", "Edit saved invoices"),
        ("Invoice", "Refund", "Create partial or full refunds"), ("Quotation", "Cancel", "Cancel open quotations"),
        ("Inventory", "Adjust", "Post stock adjustments"), ("Product", "Manage", "Add, edit, and delete products"),
        ("Customer", "Manage", "Add, edit, and delete customers"), ("Unit", "Manage", "Manage units of measure"),
        ("Price", "Manage", "Manage selling units and prices"), ("Reports", "View", "Open business reports")
    ];

    public PermissionManagementViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> currentRole)
    { this.factory = factory; this.currentRole = currentRole; RefreshUsers(); }

    partial void OnSelectedRoleChanged(string value) => Load();
    partial void OnSelectedUserChanged(PermissionUserViewModel? value)
    {
        if (value != null) SelectedRole = value.Role;
    }

    [RelayCommand]
    public void RefreshUsers()
    {
        var selectedId = SelectedUser?.Id;
        Users.Clear();
        if (factory != null)
        {
            using var db = factory.CreateDbContext();
            db.EnsureCurrentSchema();
            foreach (var user in db.Users.AsNoTracking().OrderBy(item => item.Username))
                Users.Add(new PermissionUserViewModel(user.Id, user.Username, user.Role));
        }
        SelectedUser = Users.FirstOrDefault(item => item.Id == selectedId) ?? Users.FirstOrDefault();
        if (SelectedUser == null) Load();
    }

    [RelayCommand]
    private void Load()
    {
        Permissions.Clear();
        var saved = new Dictionary<(string, string), bool>();
        if (factory != null)
        {
            using var db = factory.CreateDbContext(); db.EnsureCurrentSchema();
            saved = db.RolePermissions.AsNoTracking().Where(item => item.Role == SelectedRole)
                .ToDictionary(item => (item.Resource, item.Action), item => item.IsAllowed);
        }
        foreach (var item in Catalog) Permissions.Add(new PermissionRowViewModel { Resource = item.Resource, Action = item.Action,
            Description = item.Description, IsAllowed = saved.GetValueOrDefault((item.Resource, item.Action)) });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage) { Error = "Only administrators can manage role permissions."; return; }
        if (factory == null) { Error = "Permission storage is unavailable."; return; }
        if (SelectedUser == null) { Error = "Select a user before saving permissions."; return; }
        IsBusy = true; Error = "";
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var user = await db.Users.SingleOrDefaultAsync(item => item.Id == SelectedUser.Id);
            if (user == null) { Error = "The selected user no longer exists."; return; }
            if (user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                !SelectedRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                await db.Users.CountAsync(item => item.Role == "Admin") <= 1)
            { Error = "The last administrator cannot be assigned another role."; return; }
            user.Role = SelectedRole;
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(item => item.Role == SelectedRole));
            db.RolePermissions.AddRange(Permissions.Select(item => new RolePermission { Role = SelectedRole, Resource = item.Resource, Action = item.Action, IsAllowed = item.IsAllowed }));
            await db.SaveChangesAsync();
            Status = $"{user.Username} is mapped to {SelectedRole}. Permissions saved for that role.";
            RefreshUsers();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
