using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop;

namespace LedgerNest.Desktop.Tests.Backup;

[Trait("Category", "Integration")]
public sealed class BackupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"users\":")]
    public void RestoreJsonBackup_InvalidDocument_NoCrashOrDataLoss(string json)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        TestData.CreateUser(model, "keep");
        Assert.False(model.RestoreJsonBackup(json));
        Assert.Contains(fixture.CreateModel().Users, user => user.Name == "keep");
        Assert.NotEmpty(model.Status);
    }

    [Fact]
    public void CreateJsonBackup_ValidDatabase_RestoresRecords()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var fields = FormCatalog.Customer();
        fields.Single(field => field.Label == "Name").Value = "restored";
        fields.Single(field => field.Label == "Phone").Value = "5550100";
        Assert.True(model.SaveRecord("Customer", fields));
        var customer = Assert.Single(model.Customers);
        var backup = model.CreateJsonBackup();
        Assert.True(model.DeleteRecord("Customer", customer));
        Assert.True(model.RestoreJsonBackup(backup), model.Status);
        Assert.Contains(fixture.CreateModel().Customers, item => item.Name == "restored");
    }

    [Fact]
    public void RestoreDatabaseBackup_CorruptBytes_NoCrashOrDataLoss()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.RestoreDatabaseBackup([1, 2, 3]));
        Assert.Single(fixture.CreateModel().Users);
    }
}
