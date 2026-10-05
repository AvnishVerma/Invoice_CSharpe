using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.Authorization;

[Trait("Category", "EndToEnd")]
public sealed class SessionIsolationTests
{
    [Fact]
    public async Task Login_RoleCreationAssignmentPermissionSaveSwitch_NoPermissionLeakage()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        await TestData.CreateRole(model, "Warehouse");
        TestData.CreateUser(model, "sales", "Sales");
        TestData.CreateUser(model, "warehouse", "Warehouse");
        model.PermissionManagement.SelectedRole = "Sales";
        var invoice = model.PermissionManagement.ResourcePermissions.Single(row => row.Resource == "Invoice");
        invoice.View.IsAllowed = invoice.Add.IsAllowed = true;
        await model.PermissionManagement.SaveCommand.ExecuteAsync(null);
        TestData.SetPermissions(fixture, "Warehouse", "Product", "View", "Update");
        Assert.Contains("Settings", model.VisibleRoutes);
        model.SignOut();
        Assert.Empty(model.VisibleRoutes);
        Assert.True(model.SignIn("sales", TestData.Password));
        Assert.Contains("Invoices", model.VisibleRoutes);
        Assert.DoesNotContain("Products", model.VisibleRoutes);
        Assert.DoesNotContain("Settings", model.VisibleRoutes);
        Assert.True(model.CanAdd("Invoice"));
        Assert.False(model.CanUpdate("Invoice"));
        model.SignOut();
        Assert.True(model.SignIn("warehouse", TestData.Password));
        Assert.DoesNotContain("Invoices", model.VisibleRoutes);
        Assert.Contains("Products", model.VisibleRoutes);
        Assert.True(model.CanUpdate("Product"));
        Assert.False(model.CanAdd("Product"));
        model.SignOut();
        Assert.True(model.SignIn("admin", "admin"));
        Assert.Equal(LedgerNest.Desktop.MainWindowViewModel.Routes, model.VisibleRoutes);
        Assert.True(model.CanDelete("User"));
    }

    [Fact]
    public async Task MultipleRoles_PermissionsUnion_DoesNotAffectOtherUser()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Viewer");
        await TestData.CreateRole(model, "Editor");
        var user = TestData.CreateUser(model, "both", "Viewer");
        var other = TestData.CreateUser(model, "viewer", "Viewer");
        TestData.SetPermissions(fixture, "Viewer", "Invoice", "View");
        TestData.SetPermissions(fixture, "Editor", "Invoice", "Update");
        using (var db = fixture.CreateDbContext())
        {
            db.UserRoles.Add(new AppUserRole { UserId = user.SourceId, RoleId = db.Roles.Single(role => role.Name == "Editor").Id });
            db.SaveChanges();
        }
        var service = new AuthorizationService(fixture);
        Assert.Equal(2, (await service.GetUserRolesAsync(user.SourceId)).Length);
        Assert.True(await service.IsAllowedAsync(user.SourceId, "Invoice", "View"));
        Assert.True(await service.IsAllowedAsync(user.SourceId, "Invoice", "Update"));
        Assert.False(await service.IsAllowedAsync(other.SourceId, "Invoice", "Update"));
        Assert.False(await service.IsAllowedAsync(user.SourceId, "Invoice", "Delete"));
    }
}
