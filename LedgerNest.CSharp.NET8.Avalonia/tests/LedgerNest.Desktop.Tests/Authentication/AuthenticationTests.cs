using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Authentication;

[Trait("Category", "Integration")]
public sealed class AuthenticationTests
{
    [Theory]
    [InlineData("admin", "admin", true)]
    [InlineData("ADMIN", "admin", true)]
    [InlineData(" admin ", "admin", true)]
    [InlineData("unknown", "admin", false)]
    [InlineData("admin", "wrong", false)]
    [InlineData("", "admin", false)]
    [InlineData(" ", "admin", false)]
    [InlineData("admin", "", false)]
    public void Login_Credentials_ExpectedSession(string username, string password, bool allowed)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel(false);
        Assert.Equal(allowed, model.SignIn(username, password));
        Assert.Equal(allowed, model.CurrentUsername != null);
        Assert.Equal(allowed, model.ValidateSession());
    }

    [Fact]
    public void Logout_AuthenticatedUser_ClearsSessionAndPermissions()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var version = model.SessionVersion;
        model.SignOut();
        Assert.Null(model.CurrentUsername);
        Assert.Empty(model.CurrentRole);
        Assert.Empty(model.VisibleRoutes);
        Assert.False(model.CanAdd("Invoice"));
        Assert.False(model.CanContinueWorkspaceOperation(version));
    }

    [Fact]
    public void Login_InvalidCredentialsAfterAdmin_DoesNotRetainAdminSession()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.SignIn("admin", "wrong"));
        Assert.Null(model.CurrentUsername);
        Assert.Empty(model.VisibleRoutes);
    }

    [Fact]
    public void ValidateSession_AccountChanged_RequiresAuthenticationAgain()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext())
        {
            db.Users.Single(user => user.Username == "admin").PasswordHash = "invalidated";
            db.SaveChanges();
        }
        Assert.False(model.ValidateSession());
        Assert.Null(model.CurrentUsername);
        Assert.False(model.CanView("Settings"));
    }
}
