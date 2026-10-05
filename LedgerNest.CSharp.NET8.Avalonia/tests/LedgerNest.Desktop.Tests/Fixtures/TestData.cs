using LedgerNest.Desktop;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.Fixtures;

public static class TestData
{
    public const string Password = "Test-password-123";

    public static FormField[] UserFields(MainWindowViewModel model, string username, string role = "User", string password = Password)
    {
        var fields = FormCatalog.User(model.PermissionManagement.Roles);
        fields.Single(field => field.Label == "Username").Value = username;
        fields.Single(field => field.Label == "Password").Value = password;
        fields.Single(field => field.Label == "Role").Value = role;
        return fields;
    }

    public static async Task CreateRole(MainWindowViewModel model, string name)
    {
        model.PermissionManagement.NewRoleName = name;
        await model.PermissionManagement.CreateRoleCommand.ExecuteAsync(null);
        Assert.Contains(name, model.PermissionManagement.Roles);
    }

    public static UiRecord CreateUser(MainWindowViewModel model, string username, string role = "User")
    {
        Assert.True(model.SaveRecord("User", UserFields(model, username, role)), model.Status);
        return model.Users.Single(user => user.Name == username);
    }

    public static void SetPermissions(TestDatabaseFixture fixture, string role, string resource, params string[] actions)
    {
        using var db = fixture.CreateDbContext();
        db.RolePermissions.RemoveRange(db.RolePermissions.Where(permission => permission.Role == role && permission.Resource == resource));
        db.RolePermissions.AddRange(RbacCatalog.Permissions.Where(permission => permission.Resource == resource)
            .Select(permission => new RolePermission { Role = role, Resource = resource, Action = permission.Action, IsAllowed = actions.Contains(permission.Action) }));
        db.SaveChanges();
    }
}
