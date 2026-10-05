using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.Permissions;

[Trait("Category", "Integration")]
public sealed class PermissionTests
{
    public static IEnumerable<object[]> SupportedActions => RbacCatalog.Permissions.Select(item => new object[] { item.Resource, item.Action });

    [Theory]
    [MemberData(nameof(SupportedActions))]
    public async Task Admin_AllSupportedActions_AlwaysAllowed(string resource, string action)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        TestData.SetPermissions(fixture, "Admin", resource);
        Assert.True(model.CanPerformAction(resource, action));
        Assert.True(await new AuthorizationService(fixture).IsUserAllowedAsync("admin", resource, action));
    }

    [Theory]
    [MemberData(nameof(SupportedActions))]
    public async Task Permission_ActionGrant_IsIndependentAndEnforcedByService(string resource, string action)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        var user = TestData.CreateUser(model, "sales", "Sales");
        TestData.SetPermissions(fixture, "Sales", resource, "View", action);
        model.SignIn("sales", TestData.Password);
        var service = new AuthorizationService(fixture);
        foreach (var supported in RbacCatalog.Permissions.Where(item => item.Resource == resource))
        {
            var expected = supported.Action == "View" || supported.Action == action;
            Assert.Equal(expected, model.CanPerformAction(resource, supported.Action));
            Assert.Equal(expected, await service.IsAllowedAsync(user.SourceId, resource, supported.Action));
        }
        TestData.SetPermissions(fixture, "Sales", resource);
        Assert.False(await service.IsAllowedAsync(user.SourceId, resource, action));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DemandAsync("Sales", resource, action));
    }

    [Fact]
    public async Task PermissionEdit_UnsavedThenSaved_OnlySavedChangesTakeEffect()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        var user = TestData.CreateUser(model, "sales", "Sales");
        model.PermissionManagement.SelectedRole = "Sales";
        var invoice = model.PermissionManagement.ResourcePermissions.Single(row => row.Resource == "Invoice");
        invoice.View.IsAllowed = true;
        invoice.Add.IsAllowed = true;
        var service = new AuthorizationService(fixture);
        Assert.False(await service.IsAllowedAsync(user.SourceId, "Invoice", "Add"));
        await model.PermissionManagement.SaveCommand.ExecuteAsync(null);
        Assert.Empty(model.PermissionManagement.Error);
        Assert.True(await service.IsAllowedAsync(user.SourceId, "Invoice", "Add"));
        Assert.False(await service.IsAllowedAsync(user.SourceId, "Invoice", "Update"));
        var reloaded = fixture.CreateModel().PermissionManagement;
        reloaded.SelectedRole = "Sales";
        Assert.True(reloaded.ResourcePermissions.Single(row => row.Resource == "Invoice").Add.IsAllowed);
    }

    [Fact]
    public async Task PermissionEdit_ViewDisabled_ClearsAndDisablesOtherActions()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        var matrix = model.PermissionManagement.ResourcePermissions.Single(row => row.Resource == "Invoice");
        matrix.View.IsAllowed = true;
        matrix.Add.IsAllowed = matrix.Update.IsAllowed = matrix.Delete.IsAllowed = true;
        matrix.View.IsAllowed = false;
        foreach (var action in new[] { matrix.Add, matrix.Update, matrix.Delete })
        {
            Assert.False(action.CanToggle);
            Assert.False(action.IsAllowed);
        }
        await model.PermissionManagement.SaveCommand.ExecuteAsync(null);
        using var db = fixture.CreateDbContext();
        Assert.All(db.RolePermissions.Where(permission => permission.Role == "Sales" && permission.Resource == "Invoice"), permission => Assert.False(permission.IsAllowed));
    }

    [Fact]
    public void PermissionMatrix_Resources_UniqueWithUnsupportedActionsDisabled()
    {
        using var fixture = new TestDatabaseFixture();
        var rows = fixture.CreateModel().PermissionManagement.ResourcePermissions;
        Assert.Equal(RbacCatalog.Permissions.Select(item => item.Resource).Distinct().Count(), rows.Count);
        Assert.Equal(rows.Count, rows.Select(row => row.Resource).Distinct().Count());
        foreach (var row in rows)
            foreach (var action in new[] { row.View, row.Add, row.Update, row.Delete })
                Assert.Equal(RbacCatalog.Permissions.Any(item => item.Resource == row.Resource && item.Action == action.Action), action.IsSupported);
    }

    [Fact]
    public void PermissionMatrix_NullRoleSelection_DoesNotCrash()
    {
        using var fixture = new TestDatabaseFixture();
        var permissions = fixture.CreateModel().PermissionManagement;
        permissions.SelectedRole = null!;
        Assert.NotEmpty(permissions.ResourcePermissions);
    }
}
