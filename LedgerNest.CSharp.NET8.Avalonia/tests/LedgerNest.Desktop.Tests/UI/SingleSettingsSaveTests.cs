using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.UI;

public sealed class SingleSettingsSaveTests
{
    [Theory]
    [InlineData("Product", "Product Details")]
    [InlineData("Customer", "Invoice Settings")]
    public void PageSavePersistsCodeConfiguration(string entity, string page)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var codes = model.CreateMasterCodeSettings(entity);
        codes.Configurations[0].Prefix = "TEST-";
        Assert.True(model.SaveSettings(page, codes));
        Assert.Equal("TEST-", new AutoCodeGenerator(fixture).Load(entity).Prefix);
        codes.Configurations[0].NextNumber = 0;
        Assert.False(model.SaveSettings(page, codes));
        Assert.NotEmpty(codes.Error);
        Assert.Equal("TEST-", new AutoCodeGenerator(fixture).Load(entity).Prefix);
    }

    [Fact]
    public void RelatedSettingsFailureRollsBackCodeConfiguration()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var codes = model.CreateMasterCodeSettings("Product");
        codes.Configurations[0].Prefix = "FAILED-";
        Assert.False(codes.TrySave(_ => throw new InvalidOperationException("Related save failed")));
        Assert.Equal("PROD", new AutoCodeGenerator(fixture).Load("Product").Prefix);
    }
}
