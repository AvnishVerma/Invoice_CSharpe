using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Fixtures;

public sealed class FailingDatabaseFactory(TestDatabaseFixture inner) : IDbContextFactory<LedgerNestDbContext>
{
    public bool Fail { get; set; }
    public LedgerNestDbContext CreateDbContext() => Fail ? throw new InvalidOperationException("Test database unavailable") : inner.CreateDbContext();
    public Task<LedgerNestDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
