using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.Desktop;

public sealed partial class PermissionRowViewModel : ObservableObject
{
    public string Resource { get; init; } = "";
    public string Action { get; init; } = "";
    public string Description { get; init; } = "";
    [ObservableProperty] private bool isAllowed;
    public bool CanEdit { get; init; } = true;
    public bool IsSupported { get; init; } = true;
    [ObservableProperty] private bool isActionEnabled = true;
    public bool CanToggle => CanEdit && IsActionEnabled;
    partial void OnIsActionEnabledChanged(bool value) => OnPropertyChanged(nameof(CanToggle));
}

public sealed class PermissionResourceRowViewModel
{
    public string Resource { get; init; } = "";
    public PermissionRowViewModel View { get; init; } = Unsupported("View");
    public PermissionRowViewModel Add { get; init; } = Unsupported("Add");
    public PermissionRowViewModel Update { get; init; } = Unsupported("Update");
    public PermissionRowViewModel Delete { get; init; } = Unsupported("Delete");

    private static PermissionRowViewModel Unsupported(string action) => new()
    { Action = action, Description = $"{action} is not applicable to this resource", CanEdit = false, IsSupported = false };

    public void ConfigureViewDependency()
    {
        void Apply()
        {
            foreach (var action in new[] { Add, Update, Delete })
            {
                action.IsActionEnabled = View.IsAllowed;
                if (!View.IsAllowed) action.IsAllowed = false;
            }
        }
        View.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PermissionRowViewModel.IsAllowed)) Apply();
        };
        Apply();
    }
}

public sealed partial class UserRoleAssignmentViewModel : ObservableObject
{
    public int RoleId { get; init; }
    public string RoleName { get; init; } = "";
    public bool IsAdmin => RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    [ObservableProperty] private bool isAssigned;
}

public sealed record PermissionUserViewModel(int Id, string Username, string Role)
{
    public string DisplayName => $"{Username}  ·  {Role}";
}

public sealed partial class PermissionManagementViewModel : ObservableObject
{
    private readonly IDbContextFactory<LedgerNestDbContext>? factory;
    private readonly Func<string> currentRole;
    public ObservableCollection<string> Roles { get; } = [];
    public ObservableCollection<PermissionUserViewModel> Users { get; } = [];
    public ObservableCollection<UserRoleAssignmentViewModel> UserRoleAssignments { get; } = [];
    public ObservableCollection<PermissionRowViewModel> Permissions { get; } = [];
    public ObservableCollection<PermissionResourceRowViewModel> ResourcePermissions { get; } = [];
    [ObservableProperty] private PermissionUserViewModel? selectedUser;
    [ObservableProperty] private string selectedRole = "User";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string newRoleName = "";
    public bool CanManage => currentRole().Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public event EventHandler? AuthorizationChanged;
    private string EffectiveSelectedRole => string.IsNullOrWhiteSpace(SelectedRole)
        ? Roles.FirstOrDefault() ?? "User"
        : SelectedRole;

    public void RefreshAuthorizationState() => OnPropertyChanged(nameof(CanManage));

    private static readonly PermissionDefinition[] Catalog = RbacCatalog.Permissions;

    public PermissionManagementViewModel(IDbContextFactory<LedgerNestDbContext>? factory, Func<string> currentRole)
    { this.factory = factory; this.currentRole = currentRole; RefreshUsers(); }

    partial void OnSelectedRoleChanged(string value) => Load();
    partial void OnSelectedUserChanged(PermissionUserViewModel? value)
    {
        if (value != null) { SelectedRole = value.Role; LoadUserRoleAssignments(); }
    }

    [RelayCommand]
    public void RefreshUsers()
    {
        var selectedId = SelectedUser?.Id;
        Users.Clear();
        if (factory != null)
        {
            new AuthorizationService(factory).EnsureRolesAsync().GetAwaiter().GetResult();
            using var db = factory.CreateDbContext();
            db.EnsureCurrentSchema();
            Roles.Clear();
            foreach (var role in db.Roles.AsNoTracking().Select(item => item.Name).AsEnumerable()
                         .OrderBy(item => item.Equals("User", StringComparison.OrdinalIgnoreCase) ? 0 : item.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? 2 : 1).ThenBy(item => item)) Roles.Add(role);
            foreach (var user in db.Users.AsNoTracking().OrderBy(item => item.Username))
                Users.Add(new PermissionUserViewModel(user.Id, user.Username, user.Role));
        }
        SelectedUser = Users.FirstOrDefault(item => item.Id == selectedId) ?? Users.FirstOrDefault();
        if (SelectedUser == null) Load();
    }

    private void NotifyAuthorizationChanged() => AuthorizationChanged?.Invoke(this, EventArgs.Empty);

    private void LoadUserRoleAssignments()
    {
        UserRoleAssignments.Clear();
        if (factory == null || SelectedUser == null) return;
        using var db = factory.CreateDbContext();
        var assigned = db.UserRoles.AsNoTracking().Where(item => item.UserId == SelectedUser.Id).Select(item => item.RoleId).ToHashSet();
        foreach (var role in db.Roles.AsNoTracking().OrderBy(item => item.Name))
            UserRoleAssignments.Add(new UserRoleAssignmentViewModel { RoleId = role.Id, RoleName = role.Name, IsAssigned = assigned.Contains(role.Id) });
    }

    [RelayCommand]
    private async Task CreateRoleAsync()
    {
        if (!CanManage || factory == null) { Error = "Only administrators can create roles."; return; }
        var name = NewRoleName?.Trim() ?? "";
        if (name.Length < 2) { Error = "Enter a role name."; return; }
        IsBusy = true; Error = ""; Status = "";
        var created = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            if (await db.Roles.AnyAsync(item => item.Name.ToLower() == name.ToLower())) { Error = "Role already exists."; return; }
            db.Roles.Add(new AppRole { Name = name });
            await db.SaveChangesAsync();
            created = true;
            NewRoleName = ""; RefreshUsers(); SelectedRole = name; LoadUserRoleAssignments();
            NotifyAuthorizationChanged();
            Status = $"Role {name} created.";
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            Error = "Role already exists.";
        }
        catch (Exception exception)
        {
            Error = created ? $"Role {name} was created, but the role list could not refresh: {exception.Message}"
                : $"Could not create role: {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteRoleAsync()
    {
        if (!CanManage || factory == null) { Error = "Only administrators can delete roles."; return; }
        var selectedRoleName = EffectiveSelectedRole;
        if (selectedRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase)) { Error = "The Admin role cannot be deleted."; return; }
        IsBusy = true; Error = ""; Status = "";
        var deleted = false;
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == selectedRoleName);
            if (role == null) return;
            var fallbackRole = await db.Roles.SingleAsync(item => item.Name == "User");
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(item => item.Role == role.Name));
            db.UserRoles.RemoveRange(db.UserRoles.Where(item => item.RoleId == role.Id));
            foreach (var user in await db.Users.Where(item => item.Role == role.Name).ToListAsync())
            {
                user.Role = fallbackRole.Name;
                if (!await db.UserRoles.AnyAsync(item => item.UserId == user.Id && item.RoleId == fallbackRole.Id))
                    db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = fallbackRole.Id });
            }
            db.Roles.Remove(role);
            await db.SaveChangesAsync();
            deleted = true;
            RefreshUsers(); NotifyAuthorizationChanged(); Status = $"Role {role.Name} deleted.";
        }
        catch (Exception exception)
        {
            Error = deleted ? $"Role {selectedRoleName} was deleted, but the role list could not refresh: {exception.Message}"
                : $"Could not delete role: {exception.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Load()
    {
        Permissions.Clear();
        ResourcePermissions.Clear();
        var selectedRoleName = EffectiveSelectedRole;
        var saved = new Dictionary<(string, string), bool>();
        if (factory != null)
        {
            using var db = factory.CreateDbContext(); db.EnsureCurrentSchema();
            saved = db.RolePermissions.AsNoTracking().Where(item => item.Role == selectedRoleName)
                .ToDictionary(item => (item.Resource, item.Action), item => item.IsAllowed);
        }
        var admin = selectedRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        foreach (var item in Catalog) Permissions.Add(new PermissionRowViewModel { Resource = item.Resource, Action = item.Action,
            Description = item.Description, IsAllowed = admin || saved.GetValueOrDefault((item.Resource, item.Action)), CanEdit = !admin });
        foreach (var group in Permissions.GroupBy(item => item.Resource))
        {
            PermissionRowViewModel Cell(string action) => group.FirstOrDefault(item => item.Action == action)
                ?? new PermissionRowViewModel { Action = action, Description = $"{action} is not applicable to {group.Key}", CanEdit = false, IsSupported = false };
            var resourceRow = new PermissionResourceRowViewModel
            {
                Resource = group.Key,
                View = Cell("View"),
                Add = Cell("Add"),
                Update = Cell("Update"),
                Delete = Cell("Delete")
            };
            resourceRow.ConfigureViewDependency();
            ResourcePermissions.Add(resourceRow);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanManage) { Error = "Only administrators can manage role permissions."; return; }
        if (factory == null) { Error = "Permission storage is unavailable."; return; }
        IsBusy = true; Error = "";
        try
        {
            var selectedRoleName = EffectiveSelectedRole;
            await using var db = await factory.CreateDbContextAsync();
            if (!selectedRoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var resource in ResourcePermissions.Where(item => !item.View.IsAllowed))
                {
                    resource.Add.IsAllowed = false;
                    resource.Update.IsAllowed = false;
                    resource.Delete.IsAllowed = false;
                }
                db.RolePermissions.RemoveRange(db.RolePermissions.Where(item => item.Role == selectedRoleName));
                db.RolePermissions.AddRange(Permissions.Select(item => new RolePermission { Role = selectedRoleName, Resource = item.Resource, Action = item.Action, IsAllowed = item.IsAllowed }));
            }
            await db.SaveChangesAsync();
            Status = $"{selectedRoleName} permissions saved.";
            RefreshUsers();
            NotifyAuthorizationChanged();
        }
        catch (Exception exception) { Error = exception.Message; }
        finally { IsBusy = false; }
    }
}
