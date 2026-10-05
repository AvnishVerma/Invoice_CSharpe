using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Products;

[Trait("Category", "Integration")]
public sealed class ProductWorkflowTests
{
    [Fact]
    public void Product_CreateReloadDelete_PreservesPriceAndUnit()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var fields = FormCatalog.Product();
        fields.Single(field => field.Label == "Name").Value = "Test product";
        fields.Single(field => field.Label == "Sale Price").Value = "12.50";
        Assert.True(model.SaveRecord("Product", fields), model.Status);
        var reloaded = fixture.CreateModel();
        var product = Assert.Single(reloaded.Products);
        Assert.Equal("Test product", product.Name);
        using (var db = fixture.CreateDbContext()) Assert.Equal(12.50m, db.Products.Find(product.SourceId)!.SalePrice);
        Assert.True(reloaded.DeleteRecord("Product", product));
        Assert.Empty(fixture.CreateModel().Products);
    }

    [Theory]
    [InlineData("Purchase Price")]
    [InlineData("Stock")]
    [InlineData("Sale Price")]
    [InlineData("Tax (%)")]
    public void Product_NegativeNumericField_RejectedWithoutPartialRecord(string label)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var fields = FormCatalog.Product();
        fields.Single(field => field.Label == "Name").Value = "Test product";
        fields.Single(field => field.Label == label).Value = "-1";
        Assert.False(model.SaveRecord("Product", fields));
        Assert.Empty(fixture.CreateModel().Products);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProductFieldVisibility_SavedToggle_ReloadReflectsConfiguration(bool visible)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Settings["Product Details"].SelectMany(section => section.Fields).Single(field => field.Label == "HSN/SAC").IsChecked = visible;
        Assert.True(model.SaveSettings("Product Details"));
        Assert.Equal(visible, fixture.CreateModel().ProductFieldVisible("HSN/SAC"));
    }
}
