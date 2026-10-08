using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;
using LedgerNest.Infrastructure;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
[Trait("Category", "UI")]
public sealed class CodeSettingsLocationTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData("Customer")]
    [InlineData("Product")]
    public Task CodeConfiguration_UsesRequestedScreenAndSavesOnlyItsEntity(string entity) => headless.Run(() =>
    {
        using var fixture = new TestDatabaseFixture(); var workspace = fixture.CreateModel();
        var customer = new InvoiceCustomerSettingsViewModel(workspace);
        var product = new ProductDetailsSettingsViewModel(workspace);
        Control view = entity == "Customer" ? new InvoiceCustomerSettingsView { DataContext = customer }
            : new ProductDetailsSettingsView { DataContext = product };
        var window = new Window { Content = view, Width = 1100, Height = 900 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var codeView = Assert.Single(view.GetVisualDescendants().OfType<MasterCodeSettingsView>());
            var state = Assert.IsType<MasterCodeSettingsViewModel>(codeView.DataContext);
            var configuration = Assert.Single(state.Configurations);
            Assert.Equal(entity, configuration.Entity);
            Assert.Contains(codeView.GetVisualDescendants().OfType<TextBox>(), input => Avalonia.Automation.AutomationProperties.GetName(input) == entity + " Code Prefix");
            configuration.Prefix = entity == "Customer" ? "CLIENT-" : "ITEM-";
            configuration.NextNumber = 25; configuration.LeadingZeros = 4;
            state.SaveCommand.Execute(null); Assert.Empty(state.Error);
            var generator = new AutoCodeGenerator(fixture);
            Assert.Equal(configuration.Prefix, generator.Load(entity).Prefix);
            var other = entity == "Customer" ? "Product" : "Customer";
            Assert.Equal(other == "Customer" ? "CUST" : "PROD", generator.Load(other).Prefix);
            Assert.Equal(1, generator.Load(other).NextNumber);
            if (entity == "Customer")
            {
                Assert.Equal(5, view.GetVisualDescendants().OfType<InvoiceToggleSettingView>().Count());
                Assert.Same(workspace.InvoiceSetting("Show Customer Phone"), customer.Phone);
                customer.Phone.IsChecked = false;
                Assert.False(workspace.InvoiceSetting("Show Customer Phone").IsChecked);
            }
        }
        finally { window.Close(); }
        using var companyState = new CompanySettingsViewModel(workspace);
        var companyView = new CompanySettingsView { DataContext = companyState };
        var companyWindow = new Window { Content = companyView, Width = 1100, Height = 900 };
        try
        {
            companyWindow.Show(); Dispatcher.UIThread.RunJobs(); companyWindow.UpdateLayout();
            Assert.Empty(companyView.GetVisualDescendants().OfType<MasterCodeSettingsView>());
        }
        finally { companyWindow.Close(); }
    });
}
