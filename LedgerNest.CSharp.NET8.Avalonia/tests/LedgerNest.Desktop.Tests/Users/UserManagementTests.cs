using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Users;

[Trait("Category", "Integration")]
public sealed class UserManagementTests
{
    [Fact]
    public async Task CreateEditAssignDeleteUser_ValidData_PersistedAcrossReload()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Cashier");
        var user = TestData.CreateUser(model, "first");
        Assert.True(model.SaveRecord("User", TestData.UserFields(model, "renamed", "Cashier"), user), model.Status);
        var reloaded = fixture.CreateModel();
        var edited = reloaded.Users.Single(item => item.Name == "renamed");
        Assert.Equal("Cashier", edited["Role"]);
        Assert.True(reloaded.SignIn("renamed", TestData.Password));
        reloaded.SignIn("admin", "admin");
        Assert.True(reloaded.DeleteRecord("User", edited));
        Assert.DoesNotContain(fixture.CreateModel().Users, item => item.Name == "renamed");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void CreateUser_EmptyUsername_RejectedWithoutPartialRecord(string username)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var count = model.Users.Count;
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, username)));
        Assert.Equal(count, fixture.CreateModel().Users.Count);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("TEST")]
    public void CreateUser_DuplicateUsername_Rejected(string username)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        TestData.CreateUser(model, "test");
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, username)));
        Assert.Contains("already in use", model.Status);
        Assert.Equal(2, fixture.CreateModel().Users.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing-role")]
    public void AssignUser_InvalidRole_Rejected(string role)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, "test", role)));
        Assert.Contains("valid user role", model.Status);
        Assert.Single(fixture.CreateModel().Users);
    }

    [Fact]
    public void DeleteUser_CurrentAdmin_Protected()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.DeleteRecord("User", model.Users.Single(user => user.Name == "admin")));
        Assert.NotNull(model.CurrentUsername);
        Assert.Single(fixture.CreateModel().Users);
    }

    [Fact]
    public void EditUser_MissingRecord_RejectedWithoutChangingExistingUsers()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, "missing"), new UiRecord { SourceId = int.MaxValue }));
        Assert.Single(fixture.CreateModel().Users);
    }

    [Fact]
    public void CreateUser_EmptyPassword_Rejected()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, "test", password: "")));
        Assert.Single(fixture.CreateModel().Users);
    }
}
