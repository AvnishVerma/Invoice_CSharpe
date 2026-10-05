using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LedgerNest.Desktop.Tests.Roles;

[Trait("Category", "Integration")]
public sealed class ConcurrentRoleTests
{
    [Fact]
    public async Task CreateRole_ConcurrentDuplicateRequests_OneRoleAndHandledRejection()
    {
        using var fixture = new TestDatabaseFixture();
        fixture.CreateModel();
        var gate = new ConcurrentSaveGate();
        var options = new DbContextOptionsBuilder<LedgerNestDbContext>()
            .UseSqlite($"Data Source={fixture.DatabasePath};Pooling=False;Default Timeout=2")
            .AddInterceptors(gate).Options;
        var factory = new InterceptedFactory(options);
        var first = new PermissionManagementViewModel(factory, () => "Admin") { NewRoleName = "Cashier" };
        var second = new PermissionManagementViewModel(factory, () => "Admin") { NewRoleName = "Cashier" };
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => first.CreateRoleCommand.ExecuteAsync(null)),
            Record.ExceptionAsync(() => second.CreateRoleCommand.ExecuteAsync(null)));
        using var db = fixture.CreateDbContext();
        Assert.Single(db.Roles.Where(role => role.Name == "Cashier"));
        Assert.All(errors, error => Assert.Null(error));
        Assert.True(first.Error.Length > 0 || second.Error.Length > 0, "The losing request must report the duplicate instead of crashing.");
    }

    private sealed class InterceptedFactory(DbContextOptions<LedgerNestDbContext> options) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(options);
        public Task<LedgerNestDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class ConcurrentSaveGate : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AppRole>().Any(entry => entry.State == EntityState.Added && entry.Entity.Name == "Cashier"))
            {
                if (Interlocked.Increment(ref arrivals) == 2) bothReady.TrySetResult();
                await bothReady.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
