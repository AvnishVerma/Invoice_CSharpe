using System.Text.Json.Nodes;
using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerNest.Desktop.Tests.Companies;

[Trait("Category", "Integration")]
public sealed class CompanyParityTests
{
    [Fact]
    public void Registry_AdoptsOriginalInPlace_AndPersistsCreateRenameDelete()
    {
        using var fixture = new CompanyFixture();
        var original = fixture.Original;
        Assert.True(original.IsOriginal);
        Assert.Equal(fixture.OriginalDatabase.DatabasePath, new SqliteConnectionStringBuilder(original.Database.ConnectionString).DataSource);
        var second = fixture.Registry.Create("Cashier business");
        fixture.Registry.Rename(second.Id, "Renamed business");
        var reopened = new CompanyRegistryService(fixture.DirectoryPath);
        Assert.Equal("Renamed business", reopened.Read().Companies.Single(company => company.Id == second.Id).Name);
        Assert.Equal(original.Id, reopened.Read().ActiveCompanyId);
        Assert.Throws<InvalidOperationException>(() => reopened.Delete(original.Id));
        reopened.Activate(second.Id);
        Assert.Throws<InvalidOperationException>(() => reopened.Delete(second.Id));
        reopened.Activate(original.Id);
        reopened.Delete(second.Id);
        Assert.Single(reopened.Read().Companies);
        Assert.True(File.Exists(new SqliteConnectionStringBuilder(second.Database.ConnectionString).DataSource));
        Assert.True(File.Exists(Path.Combine(fixture.DirectoryPath, "DeletedCompanies", second.Id + ".json")));
        Assert.Throws<InvalidOperationException>(() => reopened.Activate(second.Id));
        Assert.Throws<InvalidOperationException>(() => reopened.Delete(original.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Registry_RejectsInvalidNames(string? name)
    {
        using var fixture = new CompanyFixture();
        Assert.Throws<ArgumentException>(() => fixture.Registry.Create(name!));
        Assert.Single(fixture.Registry.Read().Companies);
    }

    [Fact]
    public void Registry_RejectsLongDuplicateAndCorruptedDataWithoutOverwriting()
    {
        using var fixture = new CompanyFixture();
        fixture.Registry.Create("Cashier");
        Assert.Throws<InvalidOperationException>(() => fixture.Registry.Create(" cashier "));
        Assert.Throws<ArgumentException>(() => fixture.Registry.Create(new string('X', 101)));
        var path = Path.Combine(fixture.DirectoryPath, "companies.json");
        File.WriteAllText(path, "{invalid");
        Assert.Throws<InvalidDataException>(() => fixture.Registry.Initialize(fixture.Original.Database));
        Assert.Equal("{invalid", File.ReadAllText(path));
    }

    [Fact]
    public async Task Registry_ConcurrentCreatesAcrossInstances_DoNotLoseOrDuplicateCompanies()
    {
        using var fixture = new CompanyFixture();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() => new CompanyRegistryService(fixture.DirectoryPath).Create("Business " + index))));
        Assert.Equal(5, fixture.Registry.Read().Companies.Length);
        Assert.Equal(5, fixture.Registry.Read().Companies.Select(company => company.Id).Distinct().Count());
    }

    [Fact]
    public void Backups_UseCompanySpecificFoldersAndSafeNames()
    {
        using var fixture = new CompanyFixture();
        var second = fixture.Registry.Create("../../Business: A");
        var name = fixture.Registry.BackupFileName(second.Id, "json", new DateTime(2026, 10, 5));
        Assert.Contains(second.Id, name);
        Assert.Equal(name, Path.GetFileName(name));
        Assert.DoesNotContain(':', name);
        Assert.NotEqual(fixture.Registry.BackupDirectory(second.Id), fixture.Registry.BackupDirectory(fixture.Original.Id));
        Assert.Throws<ArgumentException>(() => fixture.Registry.BackupFileName(second.Id, "../file", DateTime.Now));
    }

    [Fact]
    public void Create_DatabaseInitializationFailureDoesNotChangeRegistryOrOriginalData()
    {
        using var fixture = new CompanyFixture();
        File.WriteAllText(Path.Combine(fixture.DirectoryPath, "Companies"), "Blocks directory creation");
        Assert.Throws<IOException>(() => fixture.Registry.Create("Cannot initialize"));
        Assert.Single(fixture.Registry.Read().Companies);
        Assert.Equal(fixture.Original.Id, fixture.Registry.Read().ActiveCompanyId);
        Assert.True(File.Exists(fixture.OriginalDatabase.DatabasePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalCompany_RestoresLegacyBackupAndAssignsCompanyIdentity(bool sqlite)
    {
        using var fixture = new CompanyFixture();
        var legacy = fixture.OriginalDatabase.CreateModel();
        var bytes = legacy.CreateDatabaseBackup();
        var json = legacy.CreateJsonBackup();
        var current = fixture.Model(fixture.Original, true);
        Assert.True(sqlite ? current.RestoreDatabaseBackup(bytes) : current.RestoreJsonBackup(json), current.Status);
        using var db = fixture.OriginalDatabase.CreateDbContext();
        Assert.Equal(fixture.Original.Id, db.Settings.Find(CompanyRegistryService.IdentitySetting)!.Value);
    }

    [Fact]
    public void Switching_ClearsOldSessionAndPermissions_AndIsolatesRecordsAndCredentials()
    {
        using var fixture = new CompanyFixture();
        var old = fixture.Model(fixture.Original, true);
        TestData.CreateUser(old, "only-company-A-user");
        using (var db = fixture.OriginalDatabase.CreateDbContext())
        {
            db.Products.Add(new Product { Name = "Only company A", StockQuantity = 10 });
            db.Customers.Add(new Customer { Name = "Private customer A" });
            db.SaveChanges();
        }
        var second = fixture.Registry.Create("Business B");
        var next = fixture.Model(second);
        var version = old.SessionVersion;
        Assert.True(old.CanNavigate("Settings"));
        CompanySessionTransition.Switch(fixture.Registry, old, next, () => { });
        Assert.Null(old.CurrentUsername);
        Assert.False(old.CanContinueWorkspaceOperation(version));
        Assert.Empty(old.VisibleRoutes);
        Assert.Null(next.CurrentUsername);
        Assert.Empty(next.VisibleRoutes);
        Assert.Equal(second.Id, fixture.Registry.Read().ActiveCompanyId);
        using var isolated = fixture.Factory(second).CreateDbContext();
        Assert.Empty(isolated.Products);
        Assert.Empty(isolated.Customers);
        Assert.Equal(second.Id, isolated.Settings.Find(CompanyRegistryService.IdentitySetting)!.Value);
        Assert.False(next.SignIn("only-company-A-user", TestData.Password));
        Assert.True(next.SignIn("admin", "admin"));
        Assert.True(next.RequiresPasswordChange);
        Assert.False(next.CanAccessWorkspace);
    }

    [Fact]
    public void Switch_PresentationFailureRollsBackRegistryAndKeepsOldSessionSignedOut()
    {
        using var fixture = new CompanyFixture();
        var old = fixture.Model(fixture.Original, true);
        var next = fixture.Model(fixture.Registry.Create("B"));
        Assert.Throws<IOException>(() => CompanySessionTransition.Switch(fixture.Registry, old, next, () => throw new IOException("Presentation failed")));
        Assert.Equal(fixture.Original.Id, fixture.Registry.Read().ActiveCompanyId);
        Assert.Null(old.CurrentUsername);
        Assert.Empty(old.VisibleRoutes);
    }

    [Fact]
    public void Switch_RejectsSignedInTargetStaleWorkspaceAndMismatchedDatabaseIdentity()
    {
        using var fixture = new CompanyFixture();
        var old = fixture.Model(fixture.Original, true);
        var second = fixture.Registry.Create("B");
        var next = fixture.Model(second, true);
        Assert.Throws<InvalidOperationException>(() => CompanySessionTransition.Switch(fixture.Registry, old, next, () => { }));
        Assert.Equal("admin", old.CurrentUsername);
        next.SignOut();
        fixture.Registry.Activate(second.Id);
        Assert.Throws<InvalidOperationException>(() => CompanySessionTransition.Switch(fixture.Registry, old, next, () => { }));
        Assert.Throws<InvalidOperationException>(() => fixture.Model(second with { Database = fixture.Original.Database }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restore_RejectsOtherCompanyBeforeChangingDestination(bool sqlite)
    {
        using var fixture = new CompanyFixture();
        var original = fixture.Model(fixture.Original, true);
        using (var db = fixture.OriginalDatabase.CreateDbContext())
        { db.Products.Add(new Product { Name = "Preserve original" }); db.SaveChanges(); }
        var second = fixture.Registry.Create("B");
        fixture.Registry.Activate(second.Id);
        var other = fixture.Model(second, true);
        var bytes = other.CreateDatabaseBackup();
        var json = other.CreateJsonBackup();
        fixture.Registry.Activate(fixture.Original.Id);
        var restored = sqlite ? original.RestoreDatabaseBackup(bytes) : original.RestoreJsonBackup(json);
        Assert.False(restored);
        Assert.Contains("another company", original.Status);
        using var intact = fixture.OriginalDatabase.CreateDbContext();
        Assert.Equal("Preserve original", Assert.Single(intact.Products).Name);
        Assert.Equal(fixture.Original.Id, intact.Settings.Find(CompanyRegistryService.IdentitySetting)!.Value);
        Assert.Equal("admin", original.CurrentUsername);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restore_SameCompanyRestoresAndKeepsIdentity(bool sqlite)
    {
        using var fixture = new CompanyFixture();
        var model = fixture.Model(fixture.Original, true);
        using (var db = fixture.OriginalDatabase.CreateDbContext())
        { db.Products.Add(new Product { Name = "Backed up" }); db.SaveChanges(); }
        var bytes = model.CreateDatabaseBackup();
        var json = model.CreateJsonBackup();
        using (var db = fixture.OriginalDatabase.CreateDbContext())
        { db.Products.Single().Name = "Edited later"; db.SaveChanges(); }
        Assert.True(sqlite ? model.RestoreDatabaseBackup(bytes) : model.RestoreJsonBackup(json), model.Status);
        using var restored = fixture.OriginalDatabase.CreateDbContext();
        Assert.Equal("Backed up", restored.Products.Single().Name);
        Assert.Equal(fixture.Original.Id, restored.Settings.Find(CompanyRegistryService.IdentitySetting)!.Value);
    }

    [Fact]
    public void JsonRestore_RejectsTamperedCompanyMetadataAndUnsignedLegacyForNewCompany()
    {
        using var fixture = new CompanyFixture();
        var second = fixture.Registry.Create("B");
        fixture.Registry.Activate(second.Id);
        var model = fixture.Model(second, true);
        var json = JsonNode.Parse(model.CreateJsonBackup())!.AsObject();
        json["_metadata"]!["company_id"] = fixture.Original.Id;
        Assert.False(model.RestoreJsonBackup(json.ToJsonString()));
        json["_metadata"]!.AsObject().Remove("company_id");
        var settings = json["settings"]!.AsArray();
        settings.Remove(settings.Single(setting => setting!["Key"]!.GetValue<string>() == CompanyRegistryService.IdentitySetting));
        Assert.False(model.RestoreJsonBackup(json.ToJsonString()));
        Assert.Contains("no company identity", model.Status);
    }

    [Fact]
    public void CompanyCommands_RejectSignedOutAndStaleUsersAtBehaviorLayer()
    {
        using var fixture = new CompanyFixture();
        var model = fixture.Model(fixture.Original);
        var manager = model.CompanyManagement!;
        manager.NewCompanyName = "Rejected";
        manager.CreateCompany();
        Assert.Single(fixture.Registry.Read().Companies);
        Assert.False(model.RestoreJsonBackup("{}"));
        Assert.Empty(model.CreateDatabaseBackup());
        fixture.SignInAdmin(model, fixture.Original);
        manager.NewCompanyName = "Allowed";
        manager.CreateCompany();
        Assert.Equal(2, manager.Companies.Count);
        model.SignOut();
        manager.RenameCompanyName = "Unauthorized rename";
        manager.RenameCompany();
        Assert.DoesNotContain(fixture.Registry.Read().Companies, company => company.Name == "Unauthorized rename");
        Assert.False(manager.CanManage);
    }

    [Fact]
    public void ExternalCompanySwitch_RevokesStaleMenusSessionAndBackupOperations()
    {
        using var fixture = new CompanyFixture();
        var model = fixture.Model(fixture.Original, true);
        var version = model.SessionVersion;
        fixture.Registry.Activate(fixture.Registry.Create("B").Id);
        Assert.Empty(model.VisibleRoutes);
        Assert.False(model.HasPermission("Invoice", "Add"));
        Assert.False(model.CanContinueWorkspaceOperation(version));
        Assert.Null(model.CurrentUsername);
        Assert.Empty(model.CreateJsonBackup());
    }
}

internal sealed class CompanyFixture : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "ledgernest-company-tests", Guid.NewGuid().ToString("N"));
    public TestDatabaseFixture OriginalDatabase { get; } = new();
    public CompanyRegistryService Registry { get; }
    public CompanyProfile Original { get; }
    public CompanyFixture()
    {
        Registry = new CompanyRegistryService(DirectoryPath);
        Original = Registry.Initialize(DatabaseProfile.LocalSqlite(OriginalDatabase.DatabasePath)).Companies.Single();
    }
    public IDbContextFactory<LedgerNestDbContext> Factory(CompanyProfile company) => new CompanyFactory(company.Database.ConnectionString);
    public MainWindowViewModel Model(CompanyProfile company, bool signIn = false)
    {
        var model = new MainWindowViewModel(Factory(company), new SqliteConnectionStringBuilder(company.Database.ConnectionString).DataSource,
            new TestLicenseService(), new CompanyWorkspaceContext(Registry, company, _ => { }));
        if (signIn) SignInAdmin(model, company);
        return model;
    }
    public void SignInAdmin(MainWindowViewModel model, CompanyProfile company)
    {
        using var db = Factory(company).CreateDbContext();
        db.Users.Single(user => user.Username == "admin").PasswordChanged = true;
        db.SaveChanges();
        Assert.True(model.SignIn("admin", "admin"));
    }
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        OriginalDatabase.Dispose();
        if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
    }
    private sealed class CompanyFactory(string connection) : IDbContextFactory<LedgerNestDbContext>
    {
        public LedgerNestDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LedgerNestDbContext>().UseSqlite(
            new SqliteConnectionStringBuilder(connection) { Pooling = false }.ToString()).Options);
    }
}
