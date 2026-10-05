using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.Roles;

[Trait("Category", "Integration")]
public sealed class RoleFailureTests
{
    [Fact]
    public async Task DeleteRole_DatabaseFailure_RolePreservedAndBusyCleared()
    {
        using var fixture = new TestDatabaseFixture();
        await TestData.CreateRole(fixture.CreateModel(), "Cashier");
        var factory = new FailingDatabaseFactory(fixture);
        var permissions = new PermissionManagementViewModel(factory, () => "Admin") { SelectedRole = "Cashier" };
        factory.Fail = true;
        var exception = await Record.ExceptionAsync(() => permissions.DeleteRoleCommand.ExecuteAsync(null));
        Assert.Null(exception);
        Assert.False(permissions.IsBusy);
        Assert.Contains("unavailable", permissions.Error);
        using var db = fixture.CreateDbContext();
        Assert.Single(db.Roles.Where(role => role.Name == "Cashier"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoleWrite_RefreshFailsAfterCommit_ReportsCommittedOutcome(bool delete)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        if (delete) await TestData.CreateRole(model, "Cashier");
        var factory = new FailAfterCommitFactory(fixture.DatabasePath);
        var permissions = new PermissionManagementViewModel(factory, () => "Admin") { NewRoleName = "Cashier" };
        if (delete) permissions.SelectedRole = "Cashier";
        factory.Armed = true;
        var exception = await Record.ExceptionAsync(() => delete
            ? permissions.DeleteRoleCommand.ExecuteAsync(null)
            : permissions.CreateRoleCommand.ExecuteAsync(null));
        Assert.Null(exception);
        Assert.False(permissions.IsBusy);
        Assert.Contains(delete ? "was deleted" : "was created", permissions.Error);
        Assert.Contains("could not refresh", permissions.Error);
        using var db = fixture.CreateDbContext();
        Assert.Equal(!delete, db.Roles.Any(role => role.Name == "Cashier"));
    }

    private sealed class FailAfterCommitFactory : IDbContextFactory<LedgerNestDbContext>
    {
        private readonly DbContextOptions<LedgerNestDbContext> options;
        private bool fail;
        public bool Armed { get; set; }
        public FailAfterCommitFactory(string path)
        {
            options = new DbContextOptionsBuilder<LedgerNestDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False")
                .AddInterceptors(new AfterCommit(() => { if (Armed) fail = true; })).Options;
        }
        public LedgerNestDbContext CreateDbContext() => fail
            ? throw new InvalidOperationException("Test refresh unavailable") : new(options);
        public Task<LedgerNestDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class AfterCommit(Action committed) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            committed();
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task SavePermissions_InsertFailsAfterReplacement_OldPermissionsRolledBack()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        await TestData.CreateRole(model, "Sales");
        TestData.SetPermissions(fixture, "Sales", "Invoice", "View");
        model.PermissionManagement.SelectedRole = "Sales";
        model.PermissionManagement.LoadCommand.Execute(null);
        model.PermissionManagement.ResourcePermissions.Single(row => row.Resource == "Invoice").Add.IsAllowed = true;
        using (var db = fixture.CreateDbContext())
            db.Database.ExecuteSqlRaw("CREATE TRIGGER fail_permission_insert BEFORE INSERT ON role_permissions WHEN NEW.Role = 'Sales' BEGIN SELECT RAISE(ABORT, 'test persistence failure'); END;");
        await model.PermissionManagement.SaveCommand.ExecuteAsync(null);
        Assert.NotEmpty(model.PermissionManagement.Error);
        Assert.False(model.PermissionManagement.IsBusy);
        using var persisted = fixture.CreateDbContext();
        Assert.True(persisted.RolePermissions.Single(permission => permission.Role == "Sales" && permission.Resource == "Invoice" && permission.Action == "View").IsAllowed);
        Assert.False(persisted.RolePermissions.Single(permission => permission.Role == "Sales" && permission.Resource == "Invoice" && permission.Action == "Add").IsAllowed);
    }

    [Fact]
    public async Task CreateRole_DatabaseFailure_ErrorHandledWithoutPartialRecord()
    {
        using var fixture = new TestDatabaseFixture();
        fixture.CreateModel();
        var factory = new FailingDatabaseFactory(fixture);
        var permissions = new PermissionManagementViewModel(factory, () => "Admin") { NewRoleName = "Cashier" };
        factory.Fail = true;
        var exception = await Record.ExceptionAsync(() => permissions.CreateRoleCommand.ExecuteAsync(null));
        using var db = fixture.CreateDbContext();
        Assert.Empty(db.Roles.Where(role => role.Name == "Cashier"));
        Assert.False(permissions.IsBusy);
        Assert.Null(exception);
        Assert.Contains("unavailable", permissions.Error);
    }

    [Fact]
    public async Task SavePermissions_DatabaseFailure_PreviousPermissionsPreservedAndBusyCleared()
    {
        using var fixture = new TestDatabaseFixture();
        fixture.CreateModel();
        var factory = new FailingDatabaseFactory(fixture);
        var permissions = new PermissionManagementViewModel(factory, () => "Admin");
        permissions.SelectedRole = "User";
        using var beforeDb = fixture.CreateDbContext();
        var before = beforeDb.RolePermissions.Where(permission => permission.Role == "User").OrderBy(permission => permission.Id)
            .Select(permission => new { permission.Resource, permission.Action, permission.IsAllowed }).ToArray();
        permissions.ResourcePermissions.Single(row => row.Resource == "Invoice").View.IsAllowed = false;
        factory.Fail = true;
        await permissions.SaveCommand.ExecuteAsync(null);
        Assert.Contains("unavailable", permissions.Error);
        Assert.False(permissions.IsBusy);
        using var afterDb = fixture.CreateDbContext();
        Assert.Equal(before, afterDb.RolePermissions.Where(permission => permission.Role == "User").OrderBy(permission => permission.Id)
            .Select(permission => new { permission.Resource, permission.Action, permission.IsAllowed }).ToArray());
    }

    [Fact]
    public async Task CreateRole_NullName_RejectedWithoutCrash()
    {
        using var fixture = new TestDatabaseFixture();
        var permissions = fixture.CreateModel().PermissionManagement;
        permissions.NewRoleName = null!;
        var exception = await Record.ExceptionAsync(() => permissions.CreateRoleCommand.ExecuteAsync(null));
        Assert.Null(exception);
        Assert.NotEmpty(permissions.Error);
    }
}
