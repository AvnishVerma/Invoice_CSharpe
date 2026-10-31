using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Authentication;

public sealed class LocalSessionRenewalTests
{
    [Fact]
    public async Task ContinueRevalidatesExistingAccountAndExtendsLocalExpiry()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.SessionWarning.Start(DateTimeOffset.UtcNow.AddMinutes(4));
        var previous = model.SessionWarning.ExpiresAt;
        await model.SessionWarning.ContinueAsync();
        Assert.True(model.SessionWarning.IsSuccess);
        Assert.True(model.SessionWarning.ExpiresAt > previous);
        Assert.NotNull(model.CurrentUsername);
        Assert.True(model.ValidateSession());
        model.SignOut(); Assert.False(model.SessionWarning.IsActive);
    }

    [Fact]
    public async Task ChangedAccountRejectsRenewalAndClearsLocalAccess()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        using (var db = fixture.CreateDbContext()) { db.Users.Single().Role = "Changed"; db.SaveChanges(); }
        model.SessionWarning.Start(DateTimeOffset.UtcNow.AddMinutes(4));
        await model.SessionWarning.ContinueAsync();
        Assert.Null(model.CurrentUsername); Assert.False(model.SessionWarning.IsActive); Assert.False(model.ValidateSession());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExitAndExpiryBypassDirtyInvoicePromptAndInvalidateOperationVersion(bool expire)
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.Title = "New Invoice"; model.InvoiceCustomer[0].Value = "Unsaved customer";
        Assert.True(model.HasUnsavedInvoiceChanges);
        var version = model.SessionVersion; var prompts = 0; model.InvoiceLeaveRequested += () => prompts++;
        model.SessionWarning.Start(DateTimeOffset.UtcNow.AddMinutes(1));
        if (expire) model.SessionWarning.Start(DateTimeOffset.UtcNow.AddSeconds(-1));
        else model.SessionWarning.ExitCommand.Execute(null);
        Assert.Null(model.CurrentUsername); Assert.False(model.CanContinueWorkspaceOperation(version));
        Assert.Equal(0, prompts); Assert.False(model.HasPendingInvoiceNavigation); Assert.Equal("", model.InvoiceCustomer[0].Value);
    }
    [Fact]
    public async Task ExpiredDeadlineDeniesSubviewRolesBeforeAnyTimerTick()
    {
        using var fixture = new TestDatabaseFixture(); var model = fixture.CreateModel();
        model.SessionWarning.Start(DateTimeOffset.UtcNow.AddSeconds(1));
        await Task.Delay(1100);
        Assert.False(model.CanAccessWorkspace);
        Assert.Equal("", model.CurrentRole);
        Assert.False(model.Inventory.CanManage);
        Assert.False(model.ProductPriceMaster.CanManage);
        Assert.False(model.ValidateSession());
        Assert.Null(model.CurrentUsername);
    }
}
