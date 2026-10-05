using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Settings;

[Trait("Category", "Integration")]
public sealed class SettingsPersistenceTests
{
    [Theory]
    [InlineData("Company Info", "Company Name", "Test Company")]
    [InlineData("PDF Settings", "Template", "Modern")]
    [InlineData("PDF Settings", "Theme Color", "#047857")]
    [InlineData("Invoice Settings", "Invoice Prefix", "TEST-")]
    [InlineData("Invoice Settings", "Quantity Column", "Units")]
    public void SaveSettings_ValidData_PersistsAcrossRestart(string section, string label, string value)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Settings[section].SelectMany(group => group.Fields).Single(field => field.Label == label).Value = value;
        Assert.True(model.SaveSettings(section), model.Status);
        Assert.Equal(value, fixture.CreateModel().Settings[section].SelectMany(group => group.Fields).Single(field => field.Label == label).Value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("100000000")]
    [InlineData("invalid")]
    public void SaveInvoiceSettings_InvalidStartingNumber_RejectedWithoutPartialUpdate(string value)
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        var fields = model.Settings["Invoice Settings"].SelectMany(group => group.Fields).ToArray();
        var original = fields.Single(field => field.Label == "Starting Number").Value;
        fields.Single(field => field.Label == "Starting Number").Value = value;
        fields.Single(field => field.Label == "Invoice Prefix").Value = "MUST-NOT-SAVE";
        Assert.False(model.SaveSettings("Invoice Settings"));
        var persisted = fixture.CreateModel().Settings["Invoice Settings"].SelectMany(group => group.Fields).ToArray();
        Assert.Equal(original, persisted.Single(field => field.Label == "Starting Number").Value);
        Assert.NotEqual("MUST-NOT-SAVE", persisted.Single(field => field.Label == "Invoice Prefix").Value);
    }

    [Fact]
    public void SaveCompanySettings_MissingName_Rejected()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        model.Settings["Company Info"].SelectMany(group => group.Fields).Single(field => field.Label == "Company Name").Value = " ";
        Assert.False(model.SaveSettings("Company Info"));
    }

    [Fact]
    public void RefreshPersistedData_RepeatedReload_PreservesRouteAndQueuedContextWithoutDuplicates()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        TestData.CreateUser(model, "test");
        model.NavigateCommand.Execute("Customers");
        model.QueueInvoiceCustomerFilter("search-context");
        model.RefreshPersistedData();
        model.RefreshPersistedData();
        Assert.Equal("Customers", model.Title);
        Assert.Equal("search-context", model.PendingInvoiceCustomerFilter);
        Assert.Equal(2, model.Users.Count);
        Assert.Equal(model.Users.Count, model.Users.Select(user => user.SourceId).Distinct().Count());
    }
}
