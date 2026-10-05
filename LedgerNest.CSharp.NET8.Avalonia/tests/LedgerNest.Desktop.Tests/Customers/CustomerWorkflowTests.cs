using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Customers;

[Trait("Category", "Integration")]
public sealed class CustomerWorkflowTests
{
    [Fact]
    public void CreateEditDeleteCustomer_ValidData_PersistsAndReloads()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.True(model.SaveRecord("Customer", Fields("First")), model.Status);
        var customer = Assert.Single(model.Customers);
        Assert.True(model.SaveRecord("Customer", Fields("Updated"), customer), model.Status);
        var reloaded = fixture.CreateModel();
        Assert.Equal("Updated", Assert.Single(reloaded.Customers).Name);
        Assert.True(reloaded.DeleteRecord("Customer", reloaded.Customers.Single()));
        Assert.Empty(fixture.CreateModel().Customers);
    }

    [Theory]
    [InlineData("Name", "")]
    [InlineData("Phone", "")]
    [InlineData("Email", "not-email")]
    public void SaveCustomer_InvalidField_DoesNotPartiallyUpdate(string label, string value)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.True(model.SaveRecord("Customer", Fields("Original")));
        var fields = Fields("Must not save");
        fields.Single(field => field.Label == label).Value = value;
        Assert.False(model.SaveRecord("Customer", fields, model.Customers.Single()));
        Assert.Equal("Original", Assert.Single(fixture.CreateModel().Customers).Name);
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("Update")]
    [InlineData("Delete")]
    public async Task Customer_UnauthorizedServiceCall_DoesNotChangeData(string action)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.True(model.SaveRecord("Customer", Fields("Original")));
        var record = model.Customers.Single();
        await TestData.CreateRole(model, "Viewer");
        TestData.CreateUser(model, "viewer", "Viewer");
        TestData.SetPermissions(fixture, "Viewer", "Customer", "View");
        model.SignIn("viewer", TestData.Password);
        Assert.False(action switch
        {
            "Add" => model.SaveRecord("Customer", Fields("New")),
            "Update" => model.SaveRecord("Customer", Fields("Updated"), record),
            _ => model.DeleteRecord("Customer", record)
        });
        Assert.Equal("Original", Assert.Single(fixture.CreateModel().Customers).Name);
    }

    private static FormField[] Fields(string name)
    {
        var fields = FormCatalog.Customer();
        fields.Single(field => field.Label == "Name").Value = name;
        fields.Single(field => field.Label == "Phone").Value = "5550100";
        return fields;
    }
}
