using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Roles;

[Trait("Category", "Integration")]
public sealed class RoleManagementTests
{
    [Fact]
    public async Task CreateRole_ValidRole_PersistedAndAvailableWithoutRestart()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Cashier");
        Assert.Contains("Cashier", FormCatalog.User(model.PermissionManagement.Roles).Single(field => field.Label == "Role").Options);
        using var db = fixture.CreateDbContext();
        Assert.Single(db.Roles.Where(role => role.Name == "Cashier"));
        Assert.Contains("Cashier", fixture.CreateModel().PermissionManagement.Roles);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("A")]
    public async Task CreateRole_InvalidName_RejectedWithoutPartialRecord(string name)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var before = model.PermissionManagement.Roles.ToArray();
        model.PermissionManagement.NewRoleName = name;
        await model.PermissionManagement.CreateRoleCommand.ExecuteAsync(null);
        Assert.NotEmpty(model.PermissionManagement.Error);
        Assert.Equal(before, model.PermissionManagement.Roles);
    }

    [Theory]
    [InlineData("Cashier")]
    [InlineData("cashier")]
    [InlineData(" CASHIER ")]
    public async Task CreateRole_DuplicateName_RejectedCaseInsensitively(string duplicate)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Cashier");
        model.PermissionManagement.NewRoleName = duplicate;
        await model.PermissionManagement.CreateRoleCommand.ExecuteAsync(null);
        Assert.Contains("already exists", model.PermissionManagement.Error);
        using var db = fixture.CreateDbContext();
        Assert.Single(db.Roles.Where(role => role.Name.ToLower() == "cashier"));
    }

    [Fact]
    public async Task DeleteRole_AssignedRole_UsersFallBackWithoutOrphanMappings()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Cashier");
        var user = TestData.CreateUser(model, "cashier", "Cashier");
        model.PermissionManagement.SelectedRole = "Cashier";
        await model.PermissionManagement.DeleteRoleCommand.ExecuteAsync(null);
        Assert.DoesNotContain("Cashier", model.PermissionManagement.Roles);
        Assert.DoesNotContain("Cashier", FormCatalog.User(model.PermissionManagement.Roles).Single(field => field.Label == "Role").Options);
        using var db = fixture.CreateDbContext();
        Assert.Equal("User", db.Users.Find(user.SourceId)!.Role);
        Assert.Empty(db.UserRoles.Where(mapping => !db.Roles.Any(role => role.Id == mapping.RoleId)));
        Assert.DoesNotContain("Cashier", fixture.CreateModel().PermissionManagement.Roles);
    }

    [Fact]
    public async Task DeleteRole_AdminRole_AlwaysProtected()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.PermissionManagement.SelectedRole = "Admin";
        await model.PermissionManagement.DeleteRoleCommand.ExecuteAsync(null);
        Assert.Contains("cannot be deleted", model.PermissionManagement.Error);
        Assert.Contains("Admin", model.PermissionManagement.Roles);
    }

    [Fact]
    public async Task DeleteRole_UnknownRole_NoCrashOrDataLoss()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var before = model.PermissionManagement.Roles.ToArray();
        model.PermissionManagement.SelectedRole = "Missing";
        await model.PermissionManagement.DeleteRoleCommand.ExecuteAsync(null);
        Assert.Equal(before, model.PermissionManagement.Roles);
    }

    [Fact]
    public async Task CreateRole_RestrictedUser_Denied()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        TestData.CreateUser(model, "restricted");
        Assert.True(model.SignIn("restricted", TestData.Password));
        model.PermissionManagement.NewRoleName = "Cashier";
        await model.PermissionManagement.CreateRoleCommand.ExecuteAsync(null);
        Assert.Contains("Only administrators", model.PermissionManagement.Error);
        Assert.DoesNotContain("Cashier", model.PermissionManagement.Roles);
    }
}
