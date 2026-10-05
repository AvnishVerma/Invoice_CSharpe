using LedgerNest.Desktop;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Fixtures;

public sealed class TestDatabaseFixture : IDbContextFactory<LedgerNestDbContext>, IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ledgernest-tests", Guid.NewGuid().ToString("N"));
    private readonly DbContextOptions<LedgerNestDbContext> options;
    public string DatabasePath { get; }
    public TestDatabaseFixture()
    {
        Directory.CreateDirectory(directory);
        DatabasePath = Path.Combine(directory, "fixture.db");
        options = new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite($"Data Source={DatabasePath};Pooling=False").Options;
        using var db = CreateDbContext();
        db.EnsureCurrentSchema();
    }
    public LedgerNestDbContext CreateDbContext() => new(options);
    public Task<LedgerNestDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDbContext());
    }
    public MainWindowViewModel CreateModel(bool authenticate = true)
    {
        var model = new MainWindowViewModel(this, DatabasePath, new TestLicenseService());
        if (authenticate && !model.SignIn("admin", "admin")) throw new InvalidOperationException("Test Admin could not sign in.");
        return model;
    }
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

public sealed class TestLicenseService : ILicenseService
{
    public string DeviceId => new('A', 64);
    public LicenseStatus GetStatus() => new(LicenseState.Active, "Isolated test entitlement", new LicenseClaims { Features = [LicenseFeatures.BusinessWrite] });
    public LicenseStatus Activate(string document) => GetStatus();
}
